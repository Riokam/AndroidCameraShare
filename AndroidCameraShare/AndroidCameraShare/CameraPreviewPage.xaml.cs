using AndroidCameraShare.Core;
using Microsoft.Extensions.Logging;

namespace AndroidCameraShare;

public partial class CameraPreviewPage : ContentPage
{
    private readonly AppSettings _settings;
    private readonly IOfferHandler _offers;
    private readonly ViewerCounter _viewers;
    private readonly ILogger<CameraPreviewPage> _logger;

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
        PreviewView.Failed += OnPreviewFailed;
    }

    private void OnPreviewFailed(object? sender, string message)
    {
        PreviewStatus.Text = message;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewers.Changed += OnViewersChanged;
        ApplyPreviewState();
    }

    protected override void OnDisappearing()
    {
        _viewers.Changed -= OnViewersChanged;
        PreviewView.IsActive = false;
        base.OnDisappearing();
    }

    private void OnViewersChanged()
    {
        MainThread.BeginInvokeOnMainThread(ApplyPreviewState);
    }

    private void ApplyPreviewState()
    {
        try
        {
            if (_offers.HasLiveSession || _viewers.HasViewer)
            {
                PreviewView.IsActive = false;
                PreviewStatus.Text = "Камера занята";
                _logger.LogInformation("Превью не стартовало: камера занята зрителем");
                return;
            }

            PreviewView.IsActive = true;
            PreviewStatus.Text = _settings.CameraFacing == CameraFacing.Front
                ? "Фронтальная камера"
                : "Основная камера";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Сбой превью");
            PreviewView.IsActive = false;
            PreviewStatus.Text = "Не удалось открыть превью";
        }
    }
}
