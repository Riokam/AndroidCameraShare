using Android.Views;
using Android.Widget;
using AndroidCameraShare.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using Color = Android.Graphics.Color;

namespace AndroidCameraShare
{
    /// <summary>
    /// PlatformView — FrameLayout: TextureView падает на setBackgroundColor из MAUI.
    /// </summary>
    internal sealed class CameraPreviewViewHandler : ViewHandler<CameraPreviewView, FrameLayout>
    {
        public static readonly IPropertyMapper<CameraPreviewView, CameraPreviewViewHandler> Mapper =
            new PropertyMapper<CameraPreviewView, CameraPreviewViewHandler>(ViewMapper)
            {
                [nameof(CameraPreviewView.IsActive)] = MapIsActive,
                [nameof(CameraPreviewView.Background)] = MapIgnoreBackground,
                [nameof(CameraPreviewView.BackgroundColor)] = MapIgnoreBackground
            };

        private TextureView? _texture;
        private LocalCameraPreview? _preview;
        private CameraPreviewView? _virtual;

        public CameraPreviewViewHandler()
            : base(Mapper)
        {
        }

        protected override FrameLayout CreatePlatformView()
        {
            FrameLayout host = new FrameLayout(Context);
            host.SetBackgroundColor(Color.Black);
            TextureView texture = new TextureView(Context);
            host.AddView(
                texture,
                new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.MatchParent));
            _texture = texture;
            return host;
        }

        protected override void ConnectHandler(FrameLayout platformView)
        {
            _virtual = VirtualView;
            if (_texture is null && platformView.ChildCount > 0)
            {
                _texture = platformView.GetChildAt(0) as TextureView;
            }

            base.ConnectHandler(platformView);
            SyncPreview();
        }

        protected override void DisconnectHandler(FrameLayout platformView)
        {
            StopPreview();
            _texture = null;
            _virtual = null;
            base.DisconnectHandler(platformView);
        }

        private static void MapIgnoreBackground(CameraPreviewViewHandler handler, CameraPreviewView view)
        {
            // Фон только у FrameLayout, не у TextureView.
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
                ILogger? logger = IPlatformApplication.Current?.Services
                    ?.GetService<ILoggerFactory>()
                    ?.CreateLogger(nameof(CameraPreviewViewHandler));
                logger?.LogError(ex, "Сбой превью камеры");
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
