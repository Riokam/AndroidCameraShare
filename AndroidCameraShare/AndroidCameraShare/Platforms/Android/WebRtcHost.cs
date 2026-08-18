using Android.Content;
using AndroidCameraShare.Core;
using Microsoft.Extensions.Logging;
using Org.Webrtc;
using System.Security.Cryptography;
using System.Text;
using Application = Android.App.Application;
using Exception = System.Exception;
using Manifest = Android.Manifest;
using Permission = Android.Content.PM.Permission;

namespace AndroidCameraShare
{
    /// <summary>
    /// Камера и PeerConnection только на время offer. Сбой → 500, HTTP не трогаем.
    /// </summary>
    public sealed class WebRtcHost : IOfferHandler
    {
        private readonly AppSettings _settings;
        private readonly ViewerCounter _viewers;
        private readonly PowerPolicy _power;
        private readonly AppSettingsStore _store;
        private readonly ILogger<WebRtcHost> _logger;
        private readonly SemaphoreSlim _sessionGate = new SemaphoreSlim(1, 1);

        private IEglBase? _eglBase;
        private PeerConnectionFactory? _factory;
        private SurfaceTextureHelper? _surfaceHelper;
        private IVideoCapturer? _capturer;
        private VideoSource? _videoSource;
        private VideoTrack? _videoTrack;
        private PeerConnection? _peerConnection;
        private CancellationTokenSource? _iceWatchdog;
        private CancellationTokenSource? _disconnectGrace;
        private SessionObserver? _observer;
        private bool _sessionLive;
        private string? _sessionId;
        private static int _isFactoryInitialized;

        public WebRtcHost(
            AppSettings settings,
            ViewerCounter viewers,
            PowerPolicy power,
            AppSettingsStore store,
            ILogger<WebRtcHost> logger)
        {
            _settings = settings;
            _viewers = viewers;
            _power = power;
            _store = store;
            _logger = logger;
        }

        public string? LastError { get; private set; }

        public bool HasLiveSession => _sessionLive;

        public async Task<HttpResponseInfo> HandleOfferAsync(string body, CancellationToken cancellationToken)
        {
            if (!OfferSdp.TryReadOffer(body, out string offerSdp))
            {
                return Json(400, OfferSdp.ToErrorJson("Bad request"));
            }

            await _sessionGate.WaitAsync(cancellationToken);
            try
            {
                if (_sessionLive)
                {
                    _logger.LogInformation("Второй зритель отклонён: сессия уже активна");
                    return Json(409, OfferSdp.ToErrorJson("Камера занята другим зрителем"));
                }

                await TeardownCoreAsync(resetCounter: false);

                // Превью Camera2 должно отпустить устройство до WebRTC.
                LocalCameraPreview.ReleaseActive();

                if (!HasCameraPermission())
                {
                    LastError = "Нет камеры";
                    _logger.LogWarning("Offer без разрешения камеры");
                    _viewers.Reset();
                    return Json(500, OfferSdp.ToErrorJson("Нет камеры"));
                }

                string answerSdp = await StartSessionAsync(offerSdp, cancellationToken);
                string sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                _sessionId = sessionId;
                LastError = null;
                _viewers.RegisterSession();
                _logger.LogInformation("Зритель подключился");
                return Json(200, OfferSdp.ToAnswerJson(answerSdp, sessionId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Сбой offer");
                LastError = "Сбой offer";
                await TeardownCoreAsync(resetCounter: true);
                return Json(500, OfferSdp.ToErrorJson("Сбой offer"));
            }
            finally
            {
                _sessionGate.Release();
            }
        }

        public async Task StopSessionAsync()
        {
            await _sessionGate.WaitAsync();
            try
            {
                await StopSessionCoreAsync();
            }
            finally
            {
                _sessionGate.Release();
            }
        }

        public async Task<bool> StopSessionAsync(string? sessionId)
        {
            await _sessionGate.WaitAsync();
            try
            {
                if (!_sessionLive)
                {
                    return true;
                }

                if (!MatchesSession(sessionId))
                {
                    _logger.LogWarning("Отклонён hangup устаревшей сессии");
                    return false;
                }

                await StopSessionCoreAsync();
                return true;
            }
            finally
            {
                _sessionGate.Release();
            }
        }

        public async Task<bool> TrySwitchCameraAsync(CameraFacing target)
        {
            await _sessionGate.WaitAsync();
            try
            {
                return await TrySwitchCameraCoreAsync(target);
            }
            finally
            {
                _sessionGate.Release();
            }
        }

        public async Task<CameraSwitchResult> TrySwitchCameraAsync(
            CameraFacing target,
            string? sessionId)
        {
            await _sessionGate.WaitAsync();
            try
            {
                if (!_sessionLive || !MatchesSession(sessionId))
                {
                    _logger.LogWarning("Отклонена смена камеры устаревшей сессией");
                    return CameraSwitchResult.SessionNotActive;
                }

                bool switched = await TrySwitchCameraCoreAsync(target);
                return switched
                    ? CameraSwitchResult.Success
                    : CameraSwitchResult.Failed;
            }
            finally
            {
                _sessionGate.Release();
            }
        }

        private async Task StopSessionCoreAsync()
        {
            LastError = null;
            await TeardownCoreAsync(resetCounter: true);
            _logger.LogInformation("Просмотр остановлен, дежурство не выключаем");
        }

        private async Task<bool> TrySwitchCameraCoreAsync(CameraFacing target)
        {
            CameraFacing previous = _settings.CameraFacing;
            if (target == previous)
            {
                return true;
            }

            if (!_sessionLive)
            {
                _settings.CameraFacing = target;
                _store.Save();
                LastError = null;
                return true;
            }

            try
            {
                RestartCapturer(target);
                _settings.CameraFacing = target;
                _store.Save();
                LastError = null;
                _logger.LogInformation("Камера сессии сменена");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось сменить камеру");
                LastError = "Не удалось сменить камеру";
                try
                {
                    RestartCapturer(previous);
                }
                catch (Exception rollbackError)
                {
                    _logger.LogError(
                        rollbackError,
                        "Не удалось восстановить предыдущую камеру, сессия закрывается");
                    await TeardownCoreAsync(resetCounter: true);
                }

                return false;
            }
        }

        private bool MatchesSession(string? sessionId)
        {
            if (string.IsNullOrEmpty(_sessionId)
                || string.IsNullOrEmpty(sessionId)
                || _sessionId.Length != sessionId.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(_sessionId),
                Encoding.UTF8.GetBytes(sessionId));
        }

        private async Task<string> StartSessionAsync(string offerSdp, CancellationToken cancellationToken)
        {
            Context context = Application.Context;
            EnsureFactory(context);
            PeerConnectionFactory factory = _factory
                ?? throw new InvalidOperationException("PeerConnectionFactory");

            IEglBase.IContext eglContext = _eglBase!.EglBaseContext
                ?? throw new InvalidOperationException("Egl context");

            Camera2Enumerator enumerator = new Camera2Enumerator(context);
            string deviceName = FindCameraName(enumerator, _settings.CameraFacing);
            _capturer = enumerator.CreateCapturer(deviceName, null)
                ?? throw new InvalidOperationException("Нет камеры");

            _surfaceHelper = SurfaceTextureHelper.Create("nanny-capture", eglContext)
                ?? throw new InvalidOperationException("SurfaceTextureHelper");
            _videoSource = factory.CreateVideoSource(_capturer.IsScreencast)
                ?? throw new InvalidOperationException("VideoSource");
            // 16:9, как лежит сенсор основной камеры — без портрета из ориентации телефона.
            _videoSource.AdaptOutputFormat(
                NannyConstants.CaptureWidth,
                NannyConstants.CaptureHeight,
                NannyConstants.CaptureFps);
            _capturer.Initialize(_surfaceHelper, context, _videoSource.CapturerObserver);
            _capturer.StartCapture(NannyConstants.CaptureWidth, NannyConstants.CaptureHeight, NannyConstants.CaptureFps);
            _videoTrack = factory.CreateVideoTrack("video0", _videoSource)
                ?? throw new InvalidOperationException("VideoTrack");

            PeerConnection.IceServer.Builder iceBuilder = PeerConnection.IceServer.InvokeBuilder("stun:stun.l.google.com:19302")
                ?? throw new InvalidOperationException("STUN builder");
            PeerConnection.IceServer stun = iceBuilder.CreateIceServer()
                ?? throw new InvalidOperationException("STUN");

            List<PeerConnection.IceServer> iceServers = [stun];
            PeerConnection.RTCConfiguration rtcConfig = new PeerConnection.RTCConfiguration(iceServers)
            {
                SdpSemantics = PeerConnection.SdpSemantics.UnifiedPlan,
                ContinualGatheringPolicy = PeerConnection.ContinualGatheringPolicy.GatherOnce
            };

            TaskCompletionSource iceComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _observer = new SessionObserver(iceComplete);
            _peerConnection = factory.CreatePeerConnection(rtcConfig, _observer)
                ?? throw new InvalidOperationException("PeerConnection не создан");
            _peerConnection.AddTrack(_videoTrack, new List<string> { "stream0" });

            SessionDescription remote = new SessionDescription(SessionDescription.Type.Offer, offerSdp);
            await SetDescriptionAsync(remote, isLocal: false, cancellationToken);

            SessionDescription? local = await CreateAnswerAsync(cancellationToken);
            if (local?.Description is null)
            {
                throw new InvalidOperationException("Пустой answer");
            }

            await SetDescriptionAsync(local, isLocal: true, cancellationToken);
            await WaitIceCompleteAsync(iceComplete, cancellationToken);

            string answerSdp = _peerConnection.LocalDescription?.Description ?? local.Description;
            if (string.IsNullOrWhiteSpace(answerSdp))
            {
                throw new InvalidOperationException("Пустой answer");
            }

            _observer.OnDisconnected = OnIceDisconnected;
            _observer.OnConnected = OnIceConnected;
            _power.OnSessionStarted();
            _sessionLive = true;
            StartIceWatchdog();
            return answerSdp;
        }

        private void StartIceWatchdog()
        {
            _iceWatchdog?.Cancel();
            _iceWatchdog?.Dispose();
            CancellationTokenSource watchdog = new CancellationTokenSource();
            _iceWatchdog = watchdog;
            _ = WaitIceOrTeardownAsync(watchdog.Token);
        }

        private async Task WaitIceOrTeardownAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(NannyConstants.IceTimeout, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _logger.LogWarning("ICE таймаут, сессия закрывается");
            _ = StopSessionAsync();
        }

        private void OnIceDisconnected(PeerConnection.IceConnectionState? state)
        {
            if (!_sessionLive)
            {
                return;
            }

            CancelDisconnectGrace();
            if (state == PeerConnection.IceConnectionState.Failed)
            {
                _logger.LogInformation("ICE завершился ошибкой, сессия закрывается");
                _ = StopSessionAsync();
                return;
            }

            CancellationTokenSource grace = new CancellationTokenSource();
            _disconnectGrace = grace;
            _logger.LogInformation(
                "ICE disconnected, ждём восстановление {GraceSeconds} с",
                NannyConstants.IceDisconnectGrace.TotalSeconds);
            _ = StopAfterDisconnectGraceAsync(grace);
        }

        private void OnIceConnected()
        {
            _iceWatchdog?.Cancel();
            CancelDisconnectGrace();
        }

        private async Task StopAfterDisconnectGraceAsync(CancellationTokenSource grace)
        {
            try
            {
                await Task.Delay(NannyConstants.IceDisconnectGrace, grace.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _disconnectGrace, null, grace) != grace)
            {
                return;
            }

            grace.Dispose();
            _logger.LogInformation("Зритель не вернулся после ICE disconnected");
            await StopSessionAsync();
        }

        private void CancelDisconnectGrace()
        {
            CancellationTokenSource? grace = Interlocked.Exchange(ref _disconnectGrace, null);
            if (grace is null)
            {
                return;
            }

            grace.Cancel();
            grace.Dispose();
        }

        private async Task TeardownCoreAsync(bool resetCounter)
        {
            _sessionLive = false;
            _sessionId = null;
            if (_observer is not null)
            {
                _observer.OnDisconnected = null;
                _observer.OnConnected = null;
            }
            _iceWatchdog?.Cancel();
            _iceWatchdog?.Dispose();
            _iceWatchdog = null;
            CancelDisconnectGrace();

            // Сначала снимаем тип camera у FGS, потом закрываем камеру — иначе Android гасит службу.
            if (resetCounter)
            {
                _viewers.Reset();
            }

            try
            {
                _capturer?.StopCapture();
            }
            catch (Exception)
            {
                // Камера могла уже быть закрыта.
            }

            try
            {
                _capturer?.Dispose();
            }
            catch (Exception)
            {
            }

            _capturer = null;
            _videoTrack?.Dispose();
            _videoTrack = null;
            _videoSource?.Dispose();
            _videoSource = null;
            _surfaceHelper?.Dispose();
            _surfaceHelper = null;

            try
            {
                _peerConnection?.Close();
                _peerConnection?.Dispose();
            }
            catch (Exception)
            {
            }

            _peerConnection = null;
            _observer = null;
            // Factory и EglBase живут до конца процесса: Dispose убивает signaling thread (destroyed mutex).

            _power.OnSessionEnded();
        }

        private async Task SetDescriptionAsync(
            SessionDescription description,
            bool isLocal,
            CancellationToken cancellationToken)
        {
            AwaitableSdpObserver observer = new AwaitableSdpObserver();
            if (isLocal)
            {
                _peerConnection!.SetLocalDescription(observer, description);
            }
            else
            {
                _peerConnection!.SetRemoteDescription(observer, description);
            }

            await observer.Set.Task.WaitAsync(cancellationToken);
        }

        private static HttpResponseInfo Json(int status, string body)
        {
            return new HttpResponseInfo
            {
                StatusCode = status,
                ContentType = "application/json; charset=utf-8",
                Body = body
            };
        }

        private static bool HasCameraPermission()
        {
            return Application.Context.CheckSelfPermission(Manifest.Permission.Camera) == Permission.Granted;
        }

        /// <summary>
        /// Factory и EglBase один раз на процесс. Повторный Dispose рвёт signaling thread.
        /// </summary>
        private void EnsureFactory(Context context)
        {
            EnsureInitialized(context);
            if (_eglBase is not null && _factory is not null)
            {
                return;
            }

            _eglBase = IEglBase.Create() ?? throw new InvalidOperationException("EglBase");
            IEglBase.IContext eglContext = _eglBase.EglBaseContext
                ?? throw new InvalidOperationException("Egl context");
            DefaultVideoEncoderFactory encoder = new DefaultVideoEncoderFactory(eglContext, true, true);
            DefaultVideoDecoderFactory decoder = new DefaultVideoDecoderFactory(eglContext);
            PeerConnectionFactory.Builder factoryBuilder = PeerConnectionFactory.InvokeBuilder()
                ?? throw new InvalidOperationException("PeerConnectionFactory.Builder");
            factoryBuilder = factoryBuilder.SetVideoEncoderFactory(encoder)
                ?? throw new InvalidOperationException("encoder factory");
            factoryBuilder = factoryBuilder.SetVideoDecoderFactory(decoder)
                ?? throw new InvalidOperationException("decoder factory");
            _factory = factoryBuilder.CreatePeerConnectionFactory()
                ?? throw new InvalidOperationException("PeerConnectionFactory");
            _logger.LogInformation("WebRTC factory создан");
        }

        private static void EnsureInitialized(Context context)
        {
            if (Interlocked.Exchange(ref _isFactoryInitialized, 1) == 1)
            {
                return;
            }

            PeerConnectionFactory.InitializationOptions.Builder initBuilder =
                PeerConnectionFactory.InitializationOptions.InvokeBuilder(context)
                ?? throw new InvalidOperationException("WebRTC init builder");
            PeerConnectionFactory.InitializationOptions options = initBuilder.CreateInitializationOptions()
                ?? throw new InvalidOperationException("WebRTC init");
            PeerConnectionFactory.Initialize(options);
        }

        private static string FindCameraName(
            Camera2Enumerator enumerator,
            CameraFacing facing)
        {
            bool wantFront = facing == CameraFacing.Front;
            string[] names = enumerator.GetDeviceNames() ?? [];
            foreach (string name in names)
            {
                if (enumerator.IsFrontFacing(name) == wantFront)
                {
                    return name;
                }
            }

            if (names.Length > 0)
            {
                return names[0];
            }

            throw new InvalidOperationException("Нет камеры");
        }

        /// <summary>
        /// Тот же VideoTrack, другой Camera2 capturer — зритель видит новую камеру без нового offer.
        /// </summary>
        private void RestartCapturer(CameraFacing facing)
        {
            if (_factory is null || _surfaceHelper is null || _videoSource is null)
            {
                throw new InvalidOperationException("Сессия не готова");
            }

            try
            {
                _capturer?.StopCapture();
            }
            catch (Exception)
            {
            }

            try
            {
                _capturer?.Dispose();
            }
            catch (Exception)
            {
            }

            _capturer = null;

            LocalCameraPreview.ReleaseActive();

            Context context = Application.Context;
            Camera2Enumerator enumerator = new Camera2Enumerator(context);
            string deviceName = FindCameraName(enumerator, facing);
            IVideoCapturer capturer = enumerator.CreateCapturer(deviceName, null)
                ?? throw new InvalidOperationException("Нет камеры");
            capturer.Initialize(_surfaceHelper, context, _videoSource.CapturerObserver);
            capturer.StartCapture(NannyConstants.CaptureWidth, NannyConstants.CaptureHeight, NannyConstants.CaptureFps);
            _videoSource.AdaptOutputFormat(
                NannyConstants.CaptureWidth,
                NannyConstants.CaptureHeight,
                NannyConstants.CaptureFps);
            _capturer = capturer;
        }

        private async Task<SessionDescription?> CreateAnswerAsync(CancellationToken cancellationToken)
        {
            AwaitableSdpObserver observer = new AwaitableSdpObserver();
            _peerConnection!.CreateAnswer(observer, new MediaConstraints());
            return await observer.Created.Task.WaitAsync(cancellationToken);
        }

        private async Task WaitIceCompleteAsync(
            TaskCompletionSource iceComplete,
            CancellationToken cancellationToken)
        {
            try
            {
                using CancellationTokenRegistration _ = cancellationToken.Register(() => iceComplete.TrySetCanceled());
                await iceComplete.Task.WaitAsync(NannyConstants.IceGatherTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("ICE complete не пришёл, отдаём host-кандидаты");
            }
        }

        private sealed class AwaitableSdpObserver : Java.Lang.Object, ISdpObserver
        {
            public TaskCompletionSource<SessionDescription?> Created { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Set { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public void OnCreateSuccess(SessionDescription? sdp)
            {
                Created.TrySetResult(sdp);
            }

            public void OnSetSuccess()
            {
                Set.TrySetResult();
            }

            public void OnCreateFailure(string? error)
            {
                Created.TrySetException(new InvalidOperationException(error ?? "SDP create"));
            }

            public void OnSetFailure(string? error)
            {
                Set.TrySetException(new InvalidOperationException(error ?? "SDP set"));
            }
        }

        private sealed class SessionObserver : Java.Lang.Object, PeerConnection.IObserver
        {
            private readonly TaskCompletionSource _iceComplete;

            public SessionObserver(TaskCompletionSource iceComplete)
            {
                _iceComplete = iceComplete;
            }

            public Action<PeerConnection.IceConnectionState?>? OnDisconnected { get; set; }

            public Action? OnConnected { get; set; }

            public void OnSignalingChange(PeerConnection.SignalingState? state)
            {
            }

            public void OnIceConnectionChange(PeerConnection.IceConnectionState? state)
            {
                if (state == PeerConnection.IceConnectionState.Connected
                    || state == PeerConnection.IceConnectionState.Completed)
                {
                    OnConnected?.Invoke();
                    return;
                }

                // Closed — следствие нашего Close(), не уход зрителя. Иначе teardown
                // и SIGABRT на destroyed mutex у signaling thread.
                if (state == PeerConnection.IceConnectionState.Failed
                    || state == PeerConnection.IceConnectionState.Disconnected)
                {
                    OnDisconnected?.Invoke(state);
                }
            }

            public void OnIceConnectionReceivingChange(bool receiving)
            {
            }

            public void OnIceGatheringChange(PeerConnection.IceGatheringState? state)
            {
                if (state == PeerConnection.IceGatheringState.Complete)
                {
                    _iceComplete.TrySetResult();
                }
            }

            public void OnIceCandidate(IceCandidate? candidate)
            {
            }

            public void OnIceCandidatesRemoved(IceCandidate[]? candidates)
            {
            }

            public void OnAddStream(MediaStream? stream)
            {
            }

            public void OnRemoveStream(MediaStream? stream)
            {
            }

            public void OnDataChannel(DataChannel? channel)
            {
            }

            public void OnRenegotiationNeeded()
            {
            }

            public void OnAddTrack(RtpReceiver? receiver, MediaStream[]? streams)
            {
            }
        }
    }
}
