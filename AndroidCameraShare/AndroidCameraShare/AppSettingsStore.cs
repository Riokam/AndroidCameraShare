using AndroidCameraShare.Core;

namespace AndroidCameraShare
{
    public sealed class AppSettingsStore
    {
        private const string PortKey = "port";
        private const string PinKey = "pin";
        private const string CameraKey = "camera";
        private const string AutostartKey = "autostart";
        private const string PowerModeKey = "powerMode";
        private const string DimScreenKey = "dimScreen";
        private readonly AppSettings _settings;

        public AppSettingsStore(AppSettings settings)
        {
            _settings = settings;
            Load();
        }

        public void Load()
        {
            int port = Preferences.Default.Get(PortKey, NannyConstants.DefaultPort);
            _settings.TrySetPort(port);

            string pin = Preferences.Default.Get(PinKey, string.Empty);
            if (pin.Length > 0)
            {
                _settings.TrySetPin(pin);
            }

            int camera = Preferences.Default.Get(CameraKey, (int)CameraFacing.Back);
            if (Enum.IsDefined(typeof(CameraFacing), camera))
            {
                _settings.CameraFacing = (CameraFacing)camera;
            }

            _settings.IsAutostartEnabled = Preferences.Default.Get(AutostartKey, false);

            int power = Preferences.Default.Get(PowerModeKey, (int)PowerMode.Economy);
            if (Enum.IsDefined(typeof(PowerMode), power))
            {
                _settings.PowerMode = (PowerMode)power;
            }

            _settings.ShouldDimScreen = Preferences.Default.Get(DimScreenKey, true);
        }

        public void Save()
        {
            Preferences.Default.Set(PortKey, _settings.Port);
            Preferences.Default.Set(PinKey, _settings.Pin);
            Preferences.Default.Set(CameraKey, (int)_settings.CameraFacing);
            Preferences.Default.Set(AutostartKey, _settings.IsAutostartEnabled);
            Preferences.Default.Set(PowerModeKey, (int)_settings.PowerMode);
            Preferences.Default.Set(DimScreenKey, _settings.ShouldDimScreen);
        }
    }
}
