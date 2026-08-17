namespace AndroidCameraShare.Core
{
    /// <summary>
    /// Вкл/выкл дежурства.
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
