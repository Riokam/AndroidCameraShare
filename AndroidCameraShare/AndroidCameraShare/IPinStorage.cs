namespace AndroidCameraShare
{
    /// <summary>
    /// Защищённое хранилище PIN, отделённое от обычных Preferences.
    /// </summary>
    public interface IPinStorage
    {
        Task<string?> GetAsync();

        Task SetAsync(string pin);
    }
}
