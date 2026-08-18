namespace AndroidCameraShare.Core
{
    /// <summary>
    /// Что приходит в запросе. Pin здесь для авного игнорирования
    /// </summary>
    public sealed class HttpRequestInfo
    {
        public required string Method { get; init; }
        public required string Path { get; init; }
        public string? PinHeader { get; init; }
        public string? PinCookie { get; init; }
        public string? PinQuery { get; init; }
        public string? SessionHeader { get; init; }
        public int BodyLength { get; init; }

        /// <summary>
        /// Тело POST /offer. Для остальных путей пусто — роутеру оно не нужно.
        /// </summary>
        public string Body { get; init; } = string.Empty;
    }
}
