namespace AndroidCameraShare.Core
{
    public static class NannyConstants
    {
        /// <summary>
        /// Порт HTTP по-умолчанию. Пользователь может задать свой в настройках.
        /// </summary>
        public const int DefaultPort = 8080;

        /// <summary>
        /// Минимальное значение порта HTTP
        /// </summary>
        public const int MinPort = 1024;

        /// <summary>
        /// Максимальное значение порта HTTP
        /// </summary>
        public const int MaxPort = 65535;

        /// <summary>
        /// Короткий PIN
        /// </summary>
        public const int PinLength = 4;

        /// <summary>
        /// Максимальный размер тела POST /offer
        /// </summary>
        public const int MaxOfferBodyBytes = 64 * 1024;

        /// <summary>
        /// Pin в заголовке, а не в url
        /// </summary>
        public const string PinHeaderName = "X-Pin";

        public const string PinCookieName = "pin";

        /// <summary>
        /// Нет ICE/кадров столько — закрываем сессию, камера гаснет, HTTP остаётся.
        /// </summary>
        public static readonly TimeSpan IceTimeout = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Сколько ждём host-кандидаты перед отправкой SDP. STUN complete может не прийти.
        /// </summary>
        public static readonly TimeSpan IceGatherTimeout = TimeSpan.FromSeconds(3);

        public const int CaptureWidth = 640;
        public const int CaptureHeight = 360;
        public const int CaptureFps = 15;

        /// <summary>
        /// Как часто страница зрителя спрашивает заряд. Не логируем каждый опрос.
        /// </summary>
        public const int BatteryPollMs = 30000;
    }
}
