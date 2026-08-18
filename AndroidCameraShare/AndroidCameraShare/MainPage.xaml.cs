using AndroidCameraShare.Core;
using QRCoder;

namespace AndroidCameraShare
{

    public partial class MainPage : ContentPage
    {
        private readonly AppSettings _settings;
        private bool _isUpdatingDutySwitch;
        private bool _dutyBusy;
        private bool _cameraReady;
        private readonly IDutyController _duty;
        private readonly ViewerCounter _viewers;
        private readonly IOfferHandler _offers;

        public MainPage(
            AppSettings settings,
            IDutyController duty,
            ViewerCounter viewers,
            IOfferHandler offers)
        {
            InitializeComponent();
            _settings = settings;
            _duty = duty;
            _viewers = viewers;
            _offers = offers;
            _duty.StateChanged += OnDutyStateChanged;
            _viewers.Changed += OnViewersChanged;
            CameraPicker.SelectedIndex = _settings.CameraFacing == CameraFacing.Front ? 1 : 0;
            _cameraReady = true;
            RefreshView();
        }

        private void OnViewersChanged()
        {
            MainThread.BeginInvokeOnMainThread(RefreshView);
        }
        private void OnDutyStateChanged()
        {
            MainThread.BeginInvokeOnMainThread(RefreshView);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _cameraReady = false;
            CameraPicker.SelectedIndex = _settings.CameraFacing == CameraFacing.Front ? 1 : 0;
            _cameraReady = true;
            RefreshView();
        }

        private async void OnSettingsClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(SettingsPage));
        }

        private async void OnDutyToggled(object? sender, ToggledEventArgs e)
        {
            if (_isUpdatingDutySwitch || _dutyBusy)
            {
                return;
            }

            _dutyBusy = true;
            DutySwitch.IsEnabled = false;
            try
            {
                if (e.Value)
                {
                    bool started = await _duty.StartAsync();
                    if (!started
                        && string.Equals(
                            _duty.LastError,
                            "Сначала задайте PIN в настройках",
                            StringComparison.Ordinal))
                    {
                        await DisplayAlertAsync(
                            "Дежурный режим",
                            "Сначала задайте PIN в настройках",
                            "OK");
                    }
                }
                else
                {
                    await _duty.StopAsync();
                }
            }
            finally
            {
                _dutyBusy = false;
                DutySwitch.IsEnabled = true;
                RefreshView();
            }
        }

        private async void OnCameraChanged(object? sender, EventArgs e)
        {
            if (!_cameraReady)
            {
                return;
            }

            CameraFacing previous = _settings.CameraFacing;
            CameraFacing target = CameraPicker.SelectedIndex == 1
                ? CameraFacing.Front
                : CameraFacing.Back;
            if (await _offers.TrySwitchCameraAsync(target))
            {
                return;
            }

            _cameraReady = false;
            CameraPicker.SelectedIndex = previous == CameraFacing.Front ? 1 : 0;
            _cameraReady = true;
            await DisplayAlertAsync("Камера", "Не удалось сменить камеру", "OK");
        }

        private async void OnPreviewCameraClicked(object? sender, EventArgs e)
        {
            PermissionStatus camera = await Permissions.RequestAsync<Permissions.Camera>();
            if (camera != PermissionStatus.Granted)
            {
                await DisplayAlertAsync("Камера", "Нет разрешения на камеру", "OK");
                return;
            }

            await Shell.Current.GoToAsync(nameof(CameraPreviewPage));
        }

        private void RefreshView()
        {
            if (_dutyBusy)
            {
                return;
            }

            bool isRunning = _duty.IsRunning;
            string? host = _duty.ListeningHost;
            bool canShowAddress = isRunning && !string.IsNullOrEmpty(host);

            AddressLabel.IsVisible = canShowAddress;
            QrImage.IsVisible = canShowAddress;

            if (canShowAddress)
            {
                string url = $"http://{host}:{_duty.ListeningPort}";
                AddressLabel.Text = url;
                QrImage.Source = ImageSource.FromStream(() => new MemoryStream(CreateQrPng(url)));
            }
            else
            {
                AddressLabel.Text = string.Empty;
                QrImage.Source = null;
            }

            StatusLabel.Text = !isRunning
                ? "Выключено"
                : _viewers.HasViewer
                    ? "Идёт просмотр, экран можно погасить"
                    : "Ждёт запрос";

            bool hasError = !string.IsNullOrEmpty(_duty.LastError);
            LastErrorLabel.IsVisible = hasError;
            LastErrorLabel.Text = _duty.LastError ?? string.Empty;

            SetDutySwitch(isRunning);
        }

        /// <summary>
        /// QR только с URL. PIN в код не кладём — иначе он окажется в кадре и в истории сканера.
        /// </summary>
        private static byte[] CreateQrPng(string url)
        {
            using QRCodeGenerator generator = new QRCodeGenerator();
            using QRCodeData data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using PngByteQRCode qr = new PngByteQRCode(data);
            return qr.GetGraphic(8);
        }

        private void SetDutySwitch(bool isOn)
        {
            if (DutySwitch.IsToggled == isOn)
            {
                return;
            }

            // MAUI на Android шлёт Toggled после присвоения — флаг снимаем на следующем кадре.
            _isUpdatingDutySwitch = true;
            DutySwitch.IsToggled = isOn;
            Dispatcher.Dispatch(() => _isUpdatingDutySwitch = false);
        }
    }
}