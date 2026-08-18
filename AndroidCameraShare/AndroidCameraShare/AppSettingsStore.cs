using AndroidCameraShare.Core;
using Microsoft.Extensions.Logging;

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
        private const string ThemeModeKey = "themeMode";
        private readonly AppSettings _settings;
        private readonly IPinStorage _pinStorage;
        private readonly ILogger<AppSettingsStore> _logger;
        private readonly SemaphoreSlim _pinLoadGate = new SemaphoreSlim(1, 1);
        private bool _pinLoadCompleted;

        public AppSettingsStore(
            AppSettings settings,
            IPinStorage pinStorage,
            ILogger<AppSettingsStore> logger)
        {
            _settings = settings;
            _pinStorage = pinStorage;
            _logger = logger;
            Load();
        }

        public void Load()
        {
            int port = Preferences.Default.Get(PortKey, NannyConstants.DefaultPort);
            _settings.TrySetPort(port);

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

            int theme = Preferences.Default.Get(ThemeModeKey, (int)AppThemeMode.System);
            _settings.ThemeMode = Enum.IsDefined(typeof(AppThemeMode), theme)
                ? (AppThemeMode)theme
                : AppThemeMode.System;
        }

        public void Save()
        {
            Preferences.Default.Set(PortKey, _settings.Port);
            Preferences.Default.Set(CameraKey, (int)_settings.CameraFacing);
            Preferences.Default.Set(AutostartKey, _settings.IsAutostartEnabled);
            Preferences.Default.Set(PowerModeKey, (int)_settings.PowerMode);
            Preferences.Default.Set(DimScreenKey, _settings.ShouldDimScreen);
            Preferences.Default.Set(ThemeModeKey, (int)_settings.ThemeMode);
        }

        /// <summary>
        /// Загружает PIN из SecureStorage и один раз мигрирует старое значение из Preferences.
        /// </summary>
        public async Task<bool> EnsurePinLoadedAsync()
        {
            await _pinLoadGate.WaitAsync();
            try
            {
                if (_pinLoadCompleted)
                {
                    return _settings.HasConfiguredPin;
                }

                string? securePin = await _pinStorage.GetAsync();
                if (AppSettings.IsValidPin(securePin))
                {
                    _settings.TrySetPin(securePin);
                    RemoveLegacyPin();
                    _pinLoadCompleted = true;
                    return true;
                }

                if (!string.IsNullOrEmpty(securePin))
                {
                    _logger.LogWarning("Защищённое хранилище содержит некорректный PIN");
                }

                string legacyPin = Preferences.Default.Get(PinKey, string.Empty);
                if (AppSettings.IsValidPin(legacyPin))
                {
                    await _pinStorage.SetAsync(legacyPin);
                    _settings.TrySetPin(legacyPin);
                    RemoveLegacyPin();
                    _logger.LogInformation("PIN перенесён в защищённое хранилище");
                }

                _pinLoadCompleted = true;
                return _settings.HasConfiguredPin;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось загрузить PIN из защищённого хранилища");
                return false;
            }
            finally
            {
                _pinLoadGate.Release();
            }
        }

        public async Task<bool> SavePinAsync(string pin)
        {
            if (!AppSettings.IsValidPin(pin))
            {
                return false;
            }

            await _pinLoadGate.WaitAsync();
            try
            {
                await _pinStorage.SetAsync(pin);
                _settings.TrySetPin(pin);
                RemoveLegacyPin();
                _pinLoadCompleted = true;
                _logger.LogInformation("PIN сохранён в защищённом хранилище");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось сохранить PIN в защищённом хранилище");
                return false;
            }
            finally
            {
                _pinLoadGate.Release();
            }
        }

        private void RemoveLegacyPin()
        {
            try
            {
                Preferences.Default.Remove(PinKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось удалить старую копию PIN из Preferences");
            }
        }
    }
}
