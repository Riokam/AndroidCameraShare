using System.Security.Cryptography;
using System.Text;

namespace AndroidCameraShare.Core
{
    /// <summary>
    /// Настройки приложения в памяти. Невалидный ввод не бросает исключение и не меняет текущее значение.
    /// </summary>
    public sealed class AppSettings
    {
        public int Port { get; set; } = NannyConstants.DefaultPort;
        public CameraFacing CameraFacing { get; set; } = CameraFacing.Back;

        /// <summary>
        /// Пусто, пока пользователь не установил.
        /// </summary>
        public string Pin { get; private set; } = string.Empty;

        public bool HasConfiguredPin => IsValidPin(Pin);

        /// <summary>
        /// Автозапуск после перезагрузки.
        /// </summary>
        public bool IsAutostartEnabled { get; set; } = false;

        public PowerMode PowerMode { get; set; } = PowerMode.Economy;

        /// <summary>
        /// Тема интерфейса. По умолчанию приложение следует теме Android.
        /// </summary>
        public AppThemeMode ThemeMode { get; set; } = AppThemeMode.System;

        public bool TrySetPort(int port)
        {
            if (port < NannyConstants.MinPort || port > NannyConstants.MaxPort)
                return false;

            Port = port;
            return true;
        }

        public bool TrySetPin(string? pin)
        {
            if (!IsValidPin(pin))
                return false;

            Pin = pin!;
            return true;
        }

        /// <summary>
        /// Сравнение по байтам за фиксированное время.
        /// </summary>
        public bool MatchesPin(string? candidate)
        {
            if (!IsValidPin(Pin) || !IsValidPin(candidate))
                return false;

            byte[] storedBytes = Encoding.UTF8.GetBytes(Pin);
            byte[] candidateBytes = Encoding.UTF8.GetBytes(candidate!);

            return CryptographicOperations.FixedTimeEquals(storedBytes, candidateBytes);
        }

        public static bool IsValidPin(string? pin)
        {
            if (pin == null || pin.Length != NannyConstants.PinLength)
                return false;

            foreach (char digit in pin)
            {
                if (!char.IsDigit(digit))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// При просмотре гасить дисплей няни.
        /// </summary>
        public bool ShouldDimScreen { get; set; } = true;

        public void ToggleCameraFacing()
        {
            CameraFacing = CameraFacing == CameraFacing.Front
                ? CameraFacing.Back
                : CameraFacing.Front;
        }
    }
}
