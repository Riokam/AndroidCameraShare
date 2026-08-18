namespace AndroidCameraShare
{
    /// <summary>
    /// PIN хранится через Android Keystore, который использует MAUI SecureStorage.
    /// </summary>
    public sealed class SecurePinStorage : IPinStorage
    {
        private const string PinKey = "nanny.pin";

        public Task<string?> GetAsync()
        {
            return SecureStorage.Default.GetAsync(PinKey);
        }

        public Task SetAsync(string pin)
        {
            return SecureStorage.Default.SetAsync(PinKey, pin);
        }
    }
}
