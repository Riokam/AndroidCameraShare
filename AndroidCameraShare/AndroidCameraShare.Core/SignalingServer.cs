using System.Net;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AndroidCameraShare.Core
{
    /// <summary>
    /// HTTP на порту из настроек. Камеру открывает только через IOfferHandler.
    /// </summary>
    public sealed class SignalingServer : IAsyncDisposable
    {
        private static readonly TimeSpan WrongPinDelay = TimeSpan.FromSeconds(1);
        private readonly AppSettings _settings;
        private readonly SignalingRouter _router;
        private readonly ILogger<SignalingServer> _logger;
        private readonly IOfferHandler? _offers;
        private readonly string _listenHost;
        private readonly object _gate = new object();
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _acceptTask;

        /// <summary>
        /// Порт bound listener. Настройки могли уже сменить Port, пока дежурство не перезапустили.
        /// </summary>
        public int ListeningPort { get; private set; }

        /// <summary>
        /// Хост для URL и QR. Может быть LAN-IP, даже если bind ушёл на «+».
        /// </summary>
        public string? ListeningHost { get; private set; }

        public SignalingServer(
            AppSettings settings,
            ViewerCounter viewers,
            ILogger<SignalingServer> logger,
            IOfferHandler? offers = null,
            IBatteryStatus? battery = null,
            string listenHost = "127.0.0.1")
        {
            _settings = settings;
            _router = new SignalingRouter(settings, viewers, battery);
            _logger = logger;
            _offers = offers;
            _listenHost = listenHost;
        }

        /// <summary>
        /// Текст для главного экрана. Null, если последняя операция старта прошла.
        /// </summary>
        public string? LastError { get; private set; }
        public bool IsRunning { get; private set; }

        public bool TryStart(string? listenHost = null)
        {
            lock (_gate)
            {
                if (IsRunning)
                {
                    return true;
                }

                string host = listenHost ?? _listenHost;
                int port = _settings.Port;
                HttpListener? listener = TryCreateStartedListener(host, port, out bool isAddressInUse);

                // Конкретный IP HttpListener на Android часто не берёт — слушаем все, в URL всё равно Wi‑Fi IP.
                if (listener is null && host != "+" && !isAddressInUse)
                {
                    listener = TryCreateStartedListener("+", port, out _);
                }

                if (listener is null)
                {
                    return false;
                }

                CancellationTokenSource cts = new CancellationTokenSource();
                _listener = listener;
                _cts = cts;
                _acceptTask = Task.Run(() => AcceptLoopAsync(listener, cts.Token));
                IsRunning = true;
                ListeningPort = port;
                ListeningHost = host;
                LastError = null;
                _logger.LogInformation("Дежурный HTTP слушает {Host}:{Port}", host, port);

                return true;
            }
        }

        public async Task StopAsync()
        {
            HttpListener? listener;
            CancellationTokenSource? cts;
            Task? acceptTask;

            lock (_gate)
            {
                listener = _listener;
                cts = _cts;
                acceptTask = _acceptTask;
                _listener = null;
                _cts = null;
                _acceptTask = null;
                IsRunning = false;
                ListeningPort = 0;
                ListeningHost = null;
                LastError = null;
            }

            if (cts is not null)
            {
                await cts.CancelAsync();
            }

            CloseListener(listener);

            if (acceptTask is not null)
            {
                try
                {
                    await acceptTask;
                }
                catch (Exception)
                {
                    // Close/Cancel разрывают GetContext — это стоп, не сбой.
                }
            }

            cts?.Dispose();

            if (listener is not null)
            {
                _logger.LogInformation("Дежурный HTTP остановлен");
            }

            if (_offers is not null)
            {
                try
                {
                    await _offers.StopSessionAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Сбой остановки WebRTC-сессии");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }

        private async Task AcceptLoopAsync(HttpListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;

                try
                {
                    context = await listener.GetContextAsync().WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                try
                {
                    await HandleRequestAsync(context, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Один битый запрос не должен убить Accept.
                    _logger.LogError(ex, "Сбой обработки HTTP-запроса");
                }
                finally
                {
                    try
                    {
                        context.Response.Close();
                    }
                    catch (Exception)
                    {
                        // Ответ мог быть уже закрыт.
                    }
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            HttpListenerRequest request = context.Request;
            string path = request.Url?.AbsolutePath ?? string.Empty;
            bool isOffer = string.Equals(request.HttpMethod, "POST", StringComparison.Ordinal)
                && path == "/offer";
            bool isHangup = string.Equals(request.HttpMethod, "POST", StringComparison.Ordinal)
                && path == "/hangup";
            bool isCamera = string.Equals(request.HttpMethod, "POST", StringComparison.Ordinal)
                && path == "/camera";

            int bodyLength = GetBodyLength(request);
            string body = string.Empty;
            if (isOffer && bodyLength > 0 && bodyLength <= NannyConstants.MaxOfferBodyBytes)
            {
                body = await ReadBodyAsync(request, bodyLength, cancellationToken);
            }

            HttpRequestInfo info = new HttpRequestInfo
            {
                Method = request.HttpMethod,
                Path = path,
                PinHeader = request.Headers[NannyConstants.PinHeaderName],
                PinCookie = request.Cookies[NannyConstants.PinCookieName]?.Value,
                PinQuery = request.QueryString["pin"],
                SessionHeader = request.Headers[NannyConstants.SessionHeaderName],
                BodyLength = bodyLength,
                Body = body
            };

            HttpResponseInfo response = _router.Route(info);

            if (response.StatusCode == 401)
            {
                _logger.LogWarning("Отклонён запрос без верного PIN");
                await Task.Delay(WrongPinDelay, cancellationToken);
            }
            else if (response.StatusCode == 200 && isOffer && _offers is not null)
            {
                response = await _offers.HandleOfferAsync(body, cancellationToken);
            }
            else if (response.StatusCode == 200 && isHangup && _offers is not null)
            {
                try
                {
                    bool stopped = await _offers.StopSessionAsync(info.SessionHeader);
                    if (stopped)
                    {
                        _logger.LogInformation("Зритель остановил просмотр");
                    }
                    else
                    {
                        response = Json(409, "Сессия больше не активна");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Сбой hangup");
                }
            }
            else if (response.StatusCode == 200 && isCamera)
            {
                try
                {
                    if (_offers is not null)
                    {
                        _settings.ToggleCameraFacing();
                        bool switched = await _offers.SwitchCameraAsync(info.SessionHeader);
                        if (!switched)
                        {
                            _settings.ToggleCameraFacing();
                            response = Json(409, "Сессия больше не активна");
                        }
                        else
                        {
                            _logger.LogInformation("Камера переключена");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Сбой смены камеры");
                }
            }

            context.Response.StatusCode = response.StatusCode;
            context.Response.ContentType = response.ContentType;
            byte[] bytes = Encoding.UTF8.GetBytes(response.Body);
            context.Response.ContentLength64 = bytes.Length;

            await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
        }

        private static HttpResponseInfo Json(int statusCode, string message)
        {
            return new HttpResponseInfo
            {
                StatusCode = statusCode,
                ContentType = "application/json; charset=utf-8",
                Body = OfferSdp.ToErrorJson(message)
            };
        }

        private static async Task<string> ReadBodyAsync(
            HttpListenerRequest request,
            int bodyLength,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[bodyLength];
            int read = 0;
            while (read < bodyLength)
            {
                int n = await request.InputStream.ReadAsync(buffer.AsMemory(read, bodyLength - read), cancellationToken);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            return Encoding.UTF8.GetString(buffer, 0, read);
        }

        private HttpListener? TryCreateStartedListener(string host, int port, out bool isAddressInUse)
        {
            isAddressInUse = false;
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://{host}:{port}/");

            try
            {
                listener.Start();
                return listener;
            }
            catch (HttpListenerException ex) when (IsAddressInUse(ex))
            {
                listener.Close();
                isAddressInUse = true;
                LastError = $"Порт {port} занят";
                _logger.LogWarning("Порт {Port} занят", port);
                return null;
            }
            catch (Exception ex)
            {
                listener.Close();
                LastError = $"Не удалось открыть порт {port}";
                _logger.LogError(ex, "Не удалось открыть порт {Port} на {Host}", port, host);
                return null;
            }
        }

        /// <summary>
        /// Тело больше лимита не читаем в память — роутеру достаточно длины для 413.
        /// </summary>
        private static int GetBodyLength(HttpListenerRequest request)
        {
            long contentLength = request.ContentLength64;

            if (contentLength <= 0)
                return 0;


            if (contentLength > int.MaxValue)
                return int.MaxValue;

            return (int)contentLength;
        }

        private static void CloseListener(HttpListener? listener)
        {
            if (listener is null)
            {
                return;
            }

            try
            {
                listener.Stop();
            }
            catch (Exception)
            {
            }

            try
            {
                listener.Abort();
            }
            catch (Exception)
            {
            }

            try
            {
                listener.Close();
            }
            catch (Exception)
            {
            }
        }

        private static bool IsAddressInUse(HttpListenerException ex)
        {
            // 32/10048 — Windows, 48 — Darwin, 98 — Linux/Android, 183 — HTTP.sys already exists.
            int code = ex.ErrorCode;
            int native = ex.NativeErrorCode;
            return code is 32 or 48 or 98 or 183 or 10048
                || native is 32 or 48 or 98 or 183 or 10048
                || Math.Abs(code) == 98
                || Math.Abs(native) == 98;
        }
    }
}
