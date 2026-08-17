using Android.Views;
using AndroidCameraShare.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

namespace AndroidCameraShare
{
    /// <summary>
    /// TextureView держим в поле: свойство PlatformView до Connect кидает и роняет процесс.
    /// </summary>
    internal sealed class CameraPreviewViewHandler : ViewHandler<CameraPreviewView, TextureView>
    {
        public static readonly IPropertyMapper<CameraPreviewView, CameraPreviewViewHandler> Mapper =
            new PropertyMapper<CameraPreviewView, CameraPreviewViewHandler>(ViewMapper)
            {
                [nameof(CameraPreviewView.IsActive)] = MapIsActive
            };

        private TextureView? _texture;
        private LocalCameraPreview? _preview;
        private CameraPreviewView? _virtual;

        public CameraPreviewViewHandler()
            : base(Mapper)
        {
        }

        protected override TextureView CreatePlatformView()
        {
            TextureView view = new TextureView(Context);
            _texture = view;
            return view;
        }

        protected override void ConnectHandler(TextureView platformView)
        {
            _texture = platformView;
            _virtual = VirtualView;
            base.ConnectHandler(platformView);
            SyncPreview();
        }

        protected override void DisconnectHandler(TextureView platformView)
        {
            StopPreview();
            _texture = null;
            _virtual = null;
            base.DisconnectHandler(platformView);
        }

        private static void MapIsActive(CameraPreviewViewHandler handler, CameraPreviewView view)
        {
            try
            {
                handler.SyncPreview();
            }
            catch (Exception ex)
            {
                view.NotifyFailed("Не удалось открыть превью");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void SyncPreview()
        {
            if (_texture is null)
            {
                return;
            }

            CameraPreviewView? view = _virtual ?? VirtualView;
            if (view is null)
            {
                return;
            }

            if (view.IsActive)
            {
                StartPreview();
                return;
            }

            StopPreview();
        }

        private void StartPreview()
        {
            if (_preview is not null || _texture is null)
            {
                return;
            }

            try
            {
                IServiceProvider? services = IPlatformApplication.Current?.Services;
                if (services is null)
                {
                    _virtual?.NotifyFailed("Не удалось открыть превью");
                    return;
                }

                AppSettings settings = services.GetRequiredService<AppSettings>();
                ILoggerFactory loggerFactory = services.GetRequiredService<ILoggerFactory>();
                ILogger logger = loggerFactory.CreateLogger("LocalCameraPreview");
                _preview = new LocalCameraPreview(settings, logger);
                _preview.Failed += OnPreviewFailed;
                _preview.Attach(_texture);
            }
            catch (Exception)
            {
                StopPreview();
                _virtual?.NotifyFailed("Не удалось открыть превью");
            }
        }

        private void StopPreview()
        {
            if (_preview is null)
            {
                return;
            }

            _preview.Failed -= OnPreviewFailed;
            try
            {
                _preview.Release();
            }
            catch (Exception)
            {
            }

            _preview = null;
        }

        private void OnPreviewFailed(string message)
        {
            CameraPreviewView? view = _virtual;
            MainThread.BeginInvokeOnMainThread(() => view?.NotifyFailed(message));
        }
    }
}
