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
        /// Сменить камеру на текущую из настроек, не рвя HTTP. Нет сессии — ничего.
        /// </summary>
        Task SwitchCameraAsync();
    }
}
