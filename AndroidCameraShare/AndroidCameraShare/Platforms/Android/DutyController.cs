using System.Net;
using Android.Content;
using Android.Net;
using AndroidCameraShare.Core;
using Microsoft.Extensions.Logging;

namespace AndroidCameraShare
{

    /// <summary>
    /// Сначала HTTP, потом FGS. Если служба не поднялась — HTTP гасим, процесс не падает.
    /// </summary>
    public sealed class DutyController : IDutyController
    {
        private readonly SignalingServer _server;
        private readonly IOfferHandler? _offers;
        private readonly ILogger<DutyController> _logger;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private string? _controllerError;

        public DutyController(SignalingServer server, ILogger<DutyController> logger, IOfferHandler? offers = null)
        {
            _server = server;
            _logger = logger;
            _offers = offers;
        }

        public bool IsRunning => _server.IsRunning;

        public string? LastError => _controllerError ?? _offers?.LastError ?? _server.LastError;

        public int ListeningPort => _server.ListeningPort;

        public string? ListeningHost => _server.ListeningHost;

        public event Action? StateChanged;

        public Task<bool> StartAsync()
        {
            return StartCoreAsync(requestPermissions: true);
        }

        /// <summary>
        /// После reboot нет Activity: диалоги разрешений не показываем, камеру не открываем.
        /// </summary>
        public Task<bool> StartFromBootAsync()
        {
            return StartCoreAsync(requestPermissions: false);
        }

        private async Task<bool> StartCoreAsync(bool requestPermissions)
        {
            await _gate.WaitAsync();
            try
            {
                _controllerError = null;

                if (requestPermissions)
                {
                    PermissionStatus notify = await Permissions.RequestAsync<Permissions.PostNotifications>();
                    if (OperatingSystem.IsAndroidVersionAtLeast(33) && notify != PermissionStatus.Granted)
                    {
                        _controllerError = "Нет разрешения на уведомления";
                        StateChanged?.Invoke();
                        return false;
                    }

                    // Тип FGS camera на Android 14+ требует разрешение; без него стартуем только dataSync.
                    await Permissions.RequestAsync<Permissions.Camera>();
                }

                string? ip = LanAddressFinder.TryGetIpv4() ?? TryGetAndroidWifiIpv4();
                if (ip is null && !requestPermissions)
                {
                    // Сразу после reboot Wi‑Fi часто ещё не поднялся.
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    ip = LanAddressFinder.TryGetIpv4() ?? TryGetAndroidWifiIpv4();
                }

                if (ip is null)
                {
                    _controllerError = "Нет адреса Wi‑Fi";
                    _logger.LogWarning("Нет адреса Wi‑Fi, дежурство не стартовало");
                    StateChanged?.Invoke();
                    return false;
                }

                if (!await TryStartListenerAsync(ip))
                {
                    StateChanged?.Invoke();
                    return false;
                }

                try
                {
                    Android.Content.Context context = Android.App.Application.Context;
                    Intent intent = new Intent(context, typeof(DutyService));
                    if (OperatingSystem.IsAndroidVersionAtLeast(26))
                    {
                        context.StartForegroundService(intent);
                    }
                    else
                    {
                        context.StartService(intent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Не удалось запустить дежурную службу");
                    await _server.StopAsync();
                    _controllerError = "Не удалось запустить службу";
                    StateChanged?.Invoke();
                    return false;
                }

                _logger.LogInformation("Дежурный режим включён, порт {Port}", _server.ListeningPort);
                StateChanged?.Invoke();
                return true;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Android не всегда сразу отпускает порт после Close — повторяем bind.
        /// </summary>
        private async Task<bool> TryStartListenerAsync(string ip)
        {
            const int attempts = 4;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (_server.TryStart(ip))
                {
                    return true;
                }

                if (attempt == attempts - 1)
                {
                    return false;
                }

                await Task.Delay(250);
            }

            return false;
        }

        public async Task StopAsync()
        {
            await StopCoreAsync(stopService: true);
        }

        /// <summary>
        /// Стоп из уведомления: службу система уже останавливает.
        /// </summary>
        public Task StopFromServiceAsync()
        {
            return StopCoreAsync(stopService: false);
        }

        private async Task StopCoreAsync(bool stopService)
        {
            await _gate.WaitAsync();
            try
            {
                _controllerError = null;
                await _server.StopAsync();

                if (stopService)
                {
                    try
                    {
                        Android.Content.Context context = Android.App.Application.Context;
                        context.StopService(new Intent(context, typeof(DutyService)));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Не удалось остановить дежурную службу");
                    }
                }

                _logger.LogInformation("Дежурный режим выключен");
                StateChanged?.Invoke();
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// На Android 10+ NetworkInterface иногда пустой — берём адрес активной сети.
        /// </summary>
        private static string? TryGetAndroidWifiIpv4()
        {
            ConnectivityManager? connectivity = Android.App.Application.Context
                .GetSystemService(Android.Content.Context.ConnectivityService) as ConnectivityManager;
            Network? network = connectivity?.ActiveNetwork;
            if (connectivity is null || network is null)
            {
                return null;
            }

            LinkProperties? properties = connectivity.GetLinkProperties(network);
            if (properties?.LinkAddresses is null)
            {
                return null;
            }

            List<IPAddress> addresses = [];
            foreach (LinkAddress link in properties.LinkAddresses)
            {
                string? host = link.Address?.HostAddress;
                if (host is not null && IPAddress.TryParse(host, out IPAddress? parsed))
                {
                    addresses.Add(parsed);
                }
            }

            return LanAddressFinder.TryPickIpv4(addresses);
        }
    }
}