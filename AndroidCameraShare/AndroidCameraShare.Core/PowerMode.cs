namespace AndroidCameraShare.Core
{
    /// <summary>
    /// Режим энергосбережения
    /// </summary>
    public enum PowerMode
    {
        /// <summary>
        /// По-умолчанию: без WakeLock и WifiLock.
        /// </summary>
        Economy,

        /// <summary>
        /// Мягкий WifiLock, если пользователь не смог подключиться в экономичном режиме
        /// </summary>
        Reliable
    }
}
