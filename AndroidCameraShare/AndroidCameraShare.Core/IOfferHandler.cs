namespace AndroidCameraShare.Core
{
    /// <summary>
    /// Камера и WebRTC только на POST /offer. В тестах можно подставить заглушку.
    /// </summary>
    public interface IOfferHandler
    {
        string? LastError { get; }

        bool HasLiveSession { get; }

        Task<HttpResponseInfo> HandleOfferAsync(string body, CancellationToken cancellationToken);

        Task StopSessionAsync();

        /// <summary>
        /// Остановить сессию только по её идентификатору.
        /// </summary>
        Task<bool> StopSessionAsync(string? sessionId);

        /// <summary>
        /// Переключить камеру и сохранить facing только после успеха.
        /// </summary>
        Task<bool> TrySwitchCameraAsync(CameraFacing target);

        /// <summary>
        /// Переключить камеру только для владельца активной сессии.
        /// </summary>
        Task<CameraSwitchResult> TrySwitchCameraAsync(CameraFacing target, string? sessionId);
    }
}
