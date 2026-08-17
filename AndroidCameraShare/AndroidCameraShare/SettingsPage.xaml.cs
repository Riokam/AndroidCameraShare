using AndroidCameraShare.Core;

namespace AndroidCameraShare;

public partial class SettingsPage : ContentPage
{
    private readonly AppSettings _settings;
    private readonly AppSettingsStore _store;
    private readonly SignalingServer _server;
    private bool _isUpdatingUi;

    public SettingsPage(AppSettings settings, AppSettingsStore store, SignalingServer server)
    {
        InitializeComponent();
        _settings = settings;
        _store = store;
        _server = server;
        LoadForm();
    }

    private void LoadForm()
    {
        _isUpdatingUi = true;
        PortEntry.Text = _settings.Port.ToString();
        AutostartSwitch.IsToggled = _settings.IsAutostartEnabled;
        PowerPicker.SelectedIndex = _settings.PowerMode == PowerMode.Reliable ? 1 : 0;
        DimScreenSwitch.IsToggled = _settings.ShouldDimScreen;
        VersionLabel.Text = FormatInstalledVersion();
        _isUpdatingUi = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        VersionLabel.Text = FormatInstalledVersion();
    }

    /// <summary>
    /// versionName и versionCode установленного APK — то, что видит система, не атрибут DLL.
    /// </summary>
    private static string FormatInstalledVersion()
    {
        string name = AppInfo.Current.VersionString;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = AppVersion.Display;
        }

        string code = AppInfo.Current.BuildString;
        return string.IsNullOrWhiteSpace(code)
            ? $"Версия {name}"
            : $"Версия {name} ({code})";
    }

    private void OnSavePortClicked(object? sender, EventArgs e)
    {
        PortErrorLabel.IsVisible = false;
        PortHintLabel.IsVisible = false;

        if (!int.TryParse(PortEntry.Text, out int port) || !_settings.TrySetPort(port))
        {
            PortErrorLabel.Text = $"Порт от {NannyConstants.MinPort} до {NannyConstants.MaxPort}";
            PortErrorLabel.IsVisible = true;
            PortEntry.Text = _settings.Port.ToString();
            return;
        }

        _store.Save();

        if (_server.IsRunning && _server.ListeningPort != _settings.Port)
        {
            PortHintLabel.Text = "Порт сменится после выкл/вкл дежурного режима";
            PortHintLabel.IsVisible = true;
        }
    }

    private void OnSavePinClicked(object? sender, EventArgs e)
    {
        PinErrorLabel.IsVisible = false;
        string pin = PinEntry.Text ?? string.Empty;
        if (pin.Length == 0)
        {
            return;
        }

        if (!_settings.TrySetPin(pin))
        {
            PinErrorLabel.Text = "PIN — ровно 4 цифры";
            PinErrorLabel.IsVisible = true;
            return;
        }

        _store.Save();
        PinEntry.Text = string.Empty;
    }

    private void OnAutostartToggled(object? sender, ToggledEventArgs e)
    {
        if (_isUpdatingUi)
        {
            return;
        }

        _settings.IsAutostartEnabled = e.Value;
        _store.Save();
    }

    private void OnPowerChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
        {
            return;
        }

        _settings.PowerMode = PowerPicker.SelectedIndex == 1
            ? PowerMode.Reliable
            : PowerMode.Economy;
        _store.Save();
    }

    private void OnDimScreenToggled(object? sender, ToggledEventArgs e)
    {
        if (_isUpdatingUi)
        {
            return;
        }

        _settings.ShouldDimScreen = e.Value;
        _store.Save();
    }

    private void OnOpenWriteSettingsClicked(object? sender, EventArgs e)
    {
#if ANDROID
        Android.Content.Intent intent = new Android.Content.Intent(
            Android.Provider.Settings.ActionManageWriteSettings);
        intent.SetData(Android.Net.Uri.Parse($"package:{Android.App.Application.Context.PackageName}"));
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        Android.App.Application.Context.StartActivity(intent);
#endif
    }
}