using AndroidCameraShare.Core;
using Microsoft.Extensions.Logging;

namespace AndroidCameraShare;

public partial class CameraPreviewPage : ContentPage
{
    private readonly AppSettings _settings;
    private readonly IOfferHandler _offers;
    private readonly ViewerCounter _viewers;
    private readonly ILogger<CameraPreviewPage> _logger;
#if ANDROID
    private LocalCameraPreview? _preview;
    private Android.Views.TextureView? _texture;
#endif

    public CameraPreviewPage(
        AppSettings settings,
        IOfferHandler offers,
        ViewerCounter viewers,
        ILogger<CameraPreviewPage> logger)
    {
        InitializeComponent();
        _settings = settings;
        _offers = offers;
        _viewers = viewers;
        _logger = logger;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewers.Changed += OnViewersChanged;
        Dispatcher.Dispatch(StartPreview);
    }

    protected override void OnDisappearing()
    {
        _viewers.Changed -= OnViewersChanged;
        StopPreview();
        base.OnDisappearing();
    }

    private void OnViewersChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_viewers.HasViewer || _offers.HasLiveSession)
            {
                StopPreview();
                PreviewStatus.Text = "Камера занята";
                return;
            }

            StartPreview();
        });
    }

    private void StartPreview()
    {
        StopPreview();
        if (_offers.HasLiveSession || _viewers.HasViewer)
        {
            PreviewStatus.Text = "Камера занята";
            return;
        }

#if ANDROID
        try
        {
            if (PreviewHost.Handler?.PlatformView is not Android.Views.ViewGroup host)
            {
                PreviewStatus.Text = "Не удалось открыть превью";
                return;
            }

            Android.Content.Context? context = host.Context ?? Android.App.Application.Context;
            if (context is null)
            {
                PreviewStatus.Text = "Не удалось открыть превью";
                return;
            }

            Android.Views.TextureView texture = new Android.Views.TextureView(context);
            host.RemoveAllViews();
            host.AddView(
                texture,
                new Android.Views.ViewGroup.LayoutParams(
                    Android.Views.ViewGroup.LayoutParams.MatchParent,
                    Android.Views.ViewGroup.LayoutParams.MatchParent));
            _texture = texture;
            _preview = new LocalCameraPreview(_settings, _logger);
            _preview.Failed += OnPreviewFailed;
            _preview.Attach(texture);
            PreviewStatus.Text = _settings.CameraFacing == CameraFacing.Front
                ? "Фронтальная камера"
                : "Основная камера";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Сбой локального превью");
            StopPreview();
            PreviewStatus.Text = "Камера занята";
        }
#else
        PreviewStatus.Text = "Превью только на Android";
#endif
    }

    private void OnPreviewFailed(string message)
    {
        MainThread.BeginInvokeOnMainThread(() => PreviewStatus.Text = message);
    }

    private void StopPreview()
    {
#if ANDROID
        if (_preview is not null)
        {
            _preview.Failed -= OnPreviewFailed;
            _preview.Release();
            _preview = null;
        }

        if (_texture?.Parent is Android.Views.ViewGroup host)
        {
            host.RemoveView(_texture);
        }

        _texture = null;
#endif
    }
}
