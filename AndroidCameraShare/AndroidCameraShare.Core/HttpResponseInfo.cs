namespace AndroidCameraShare.Core
{
    public sealed class HttpResponseInfo
    {
        public int StatusCode { get; init; }
        public string ContentType { get; init; } = "text/plain; charset=utf-8";
        public string Body { get; init; } = string.Empty;
    }
}
