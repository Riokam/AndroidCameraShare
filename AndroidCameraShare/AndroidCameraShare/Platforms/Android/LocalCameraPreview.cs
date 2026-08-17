using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.OS;
using Android.Views;
using AndroidCameraShare.Core;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;
using Exception = System.Exception;
using Object = Java.Lang.Object;

namespace AndroidCameraShare
{
    /// <summary>
    /// Локальный превью Camera2. Не трогает WebRTC-сессию.
    /// </summary>
    internal sealed class LocalCameraPreview : Object, TextureView.ISurfaceTextureListener
    {
        private static LocalCameraPreview? _active;

        private readonly AppSettings _settings;
        private readonly ILogger _logger;
        private readonly object _closeGate = new object();
        private TextureView? _texture;
        private CameraDevice? _device;
        private CameraCaptureSession? _session;
        private HandlerThread? _thread;
        private Handler? _handler;
        private bool _released;

        public event Action<string>? Failed;

        public LocalCameraPreview(AppSettings settings, ILogger logger)
        {
            _settings = settings;
            _logger = logger;
        }

        /// <summary>
        /// Offer/смена камеры: отпустить устройство, если открыт превью.
        /// </summary>
        public static void ReleaseActive()
        {
            LocalCameraPreview? active = Interlocked.Exchange(ref _active, null);
            active?.Release();
        }

        public void Attach(TextureView texture)
        {
            LocalCameraPreview? previous = Interlocked.Exchange(ref _active, this);
            if (previous is not null && !ReferenceEquals(previous, this))
            {
                previous.Release();
            }

            _texture = texture;
            texture.SurfaceTextureListener = this;
            if (texture.IsAvailable && texture.SurfaceTexture is not null)
            {
                Open(texture.SurfaceTexture, texture.Width, texture.Height);
            }
        }

        public void Release()
        {
            lock (_closeGate)
            {
                if (_released)
                {
                    return;
                }

                _released = true;
            }

            Interlocked.CompareExchange(ref _active, null, this);
            CloseCamera();
            if (_texture is not null)
            {
                _texture.SurfaceTextureListener = null;
                _texture = null;
            }

            HandlerThread? thread = _thread;
            _thread = null;
            _handler = null;
            if (thread is not null)
            {
                thread.QuitSafely();
                try
                {
                    thread.Join(500);
                }
                catch (Exception)
                {
                }
            }
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture? surface, int width, int height)
        {
            if (surface is null)
            {
                return;
            }

            Open(surface, width, height);
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture? surface)
        {
            CloseCamera();
            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture? surface, int width, int height)
        {
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture? surface)
        {
        }

        private void Open(SurfaceTexture surface, int width, int height)
        {
            if (_released)
            {
                return;
            }

            Context context = Application.Context;
            CameraManager? manager = context.GetSystemService(Context.CameraService) as CameraManager;
            if (manager is null)
            {
                _logger.LogWarning("Нет CameraManager");
                return;
            }

            string? cameraId = FindCameraId(manager);
            if (cameraId is null)
            {
                _logger.LogWarning("Нет камеры для превью");
                return;
            }

            EnsureThread();
            try
            {
                manager.OpenCamera(cameraId, new DeviceCallback(this, surface, width, height), _handler);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось открыть камеру для превью");
                Failed?.Invoke("Камера занята");
            }
        }

        private string? FindCameraId(CameraManager manager)
        {
            string[] ids = manager.GetCameraIdList() ?? [];
            int want = _settings.CameraFacing == CameraFacing.Front
                ? (int)LensFacing.Front
                : (int)LensFacing.Back;

            foreach (string id in ids)
            {
                CameraCharacteristics? chars = manager.GetCameraCharacteristics(id);
                Java.Lang.Integer? facing = chars?.Get(CameraCharacteristics.LensFacing) as Java.Lang.Integer;
                if (facing is not null && facing.IntValue() == want)
                {
                    return id;
                }
            }

            return ids.Length > 0 ? ids[0] : null;
        }

        private void EnsureThread()
        {
            if (_thread is not null)
            {
                return;
            }

            HandlerThread thread = new HandlerThread("nanny-preview");
            thread.Start();
            _thread = thread;
            _handler = new Handler(thread.Looper!);
        }

        private void OnOpened(CameraDevice device, SurfaceTexture surface, int width, int height)
        {
            if (_released)
            {
                device.Close();
                return;
            }

            _device = device;
            int w = width > 0 ? width : NannyConstants.CaptureWidth;
            int h = height > 0 ? height : NannyConstants.CaptureHeight;
            surface.SetDefaultBufferSize(w, h);
            Surface previewSurface = new Surface(surface);
            CaptureRequest.Builder? builder = device.CreateCaptureRequest(CameraTemplate.Preview);
            if (builder is null)
            {
                return;
            }

            builder.AddTarget(previewSurface);
            device.CreateCaptureSession(
                new List<Surface> { previewSurface },
                new SessionCallback(this, builder),
                _handler);
        }

        private void OnSessionReady(CameraCaptureSession session, CaptureRequest.Builder builder)
        {
            if (_released)
            {
                session.Close();
                return;
            }

            _session = session;
            CaptureRequest.Key? controlMode = CaptureRequest.ControlMode;
            if (controlMode is not null)
            {
                builder.Set(controlMode, (int)ControlMode.Auto);
            }
            session.SetRepeatingRequest(builder.Build()!, null, _handler);
        }

        private void CloseCamera()
        {
            try
            {
                _session?.Close();
            }
            catch (Exception)
            {
            }

            _session = null;
            try
            {
                _device?.Close();
            }
            catch (Exception)
            {
            }

            _device = null;
        }

        private sealed class DeviceCallback : CameraDevice.StateCallback
        {
            private readonly LocalCameraPreview _owner;
            private readonly SurfaceTexture _surface;
            private readonly int _width;
            private readonly int _height;

            public DeviceCallback(LocalCameraPreview owner, SurfaceTexture surface, int width, int height)
            {
                _owner = owner;
                _surface = surface;
                _width = width;
                _height = height;
            }

            public override void OnOpened(CameraDevice camera)
            {
                _owner.OnOpened(camera, _surface, _width, _height);
            }

            public override void OnDisconnected(CameraDevice camera)
            {
                camera.Close();
            }

            public override void OnError(CameraDevice camera, CameraError error)
            {
                _owner._logger.LogWarning("Превью камеры: {Error}", error);
                camera.Close();
                _owner.Failed?.Invoke("Камера занята");
            }
        }

        private sealed class SessionCallback : CameraCaptureSession.StateCallback
        {
            private readonly LocalCameraPreview _owner;
            private readonly CaptureRequest.Builder _builder;

            public SessionCallback(LocalCameraPreview owner, CaptureRequest.Builder builder)
            {
                _owner = owner;
                _builder = builder;
            }

            public override void OnConfigured(CameraCaptureSession session)
            {
                _owner.OnSessionReady(session, _builder);
            }

            public override void OnConfigureFailed(CameraCaptureSession session)
            {
                _owner._logger.LogWarning("Превью: сессия Camera2 не собралась");
            }
        }
    }
}
