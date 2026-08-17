namespace AndroidCameraShare
{
    /// <summary>
    /// Вкл/выкл дежурства. Только Android: HTTP + foreground-сервис.
    /// </summary>
    public interface IDutyController
    {
        bool IsRunning { get; }
        string? LastError { get; }
        int ListeningPort { get; }
        string? ListeningHost { get; }
        event Action? StateChanged;
        Task<bool> StartAsync();
        Task<bool> StartFromBootAsync();
        Task StopAsync();
    }
}
