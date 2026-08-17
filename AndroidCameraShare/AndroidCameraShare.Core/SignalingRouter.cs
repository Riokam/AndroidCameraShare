namespace AndroidCameraShare.Core
{
    /// <summary>
    /// Разбор HTTP: код ответа и тело
    /// </summary>
    public sealed class SignalingRouter
    {
        private readonly AppSettings _settings;
        private readonly ViewerCounter _viewers;
        private readonly IBatteryStatus? _battery;

        public SignalingRouter(AppSettings settings, ViewerCounter viewers, IBatteryStatus? battery = null)
        {
            _settings = settings;
            _viewers = viewers;
            _battery = battery;
        }

        public HttpResponseInfo Route(HttpRequestInfo request)
        {
            string path = request.Path ?? string.Empty;

            //без нормализации ".." != /health и тд
            if (path.Contains("..", StringComparison.Ordinal))
                return NotFound();

            if (IsGet(request.Method) && path == "/health")
                return Health();

            // Браузер сам просит иконку вкладки — не 404 и не PIN.
            if (IsGet(request.Method) && path == "/favicon.ico")
            {
                return new HttpResponseInfo
                {
                    StatusCode = 204,
                    ContentType = "image/x-icon"
                };
            }

            if (IsGet(request.Method) && path == "/")
            {
                if (!IsAuthorized(request))
                {
                    return new HttpResponseInfo
                    {
                        StatusCode = 401,
                        ContentType = "text/html; charset=utf-8",
                        Body = ViewerPage.PinFormHtml
                    };
                }

                return new HttpResponseInfo
                {
                    StatusCode = 200,
                    ContentType = "text/html; charset=utf-8",
                    Body = ViewerPage.WatchHtml
                };
            }

            if (IsGet(request.Method) && path == "/status")
            {
                if (!IsAuthorized(request))
                    return Unauthorized();

                return Status();
            }

            if (IsPost(request.Method) && path == "/hangup")
            {
                if (!IsAuthorized(request))
                    return Unauthorized();

                return new HttpResponseInfo
                {
                    StatusCode = 200,
                    ContentType = "application/json; charset=utf-8",
                    Body = "{}"
                };
            }

            if (IsPost(request.Method) && path == "/offer")
            {
                if (!IsAuthorized(request))
                    return Unauthorized();

                if (request.BodyLength > NannyConstants.MaxOfferBodyBytes)
                    return new HttpResponseInfo { StatusCode = 413 };

                if (request.BodyLength <= 0)
                    return new HttpResponseInfo
                    {
                        StatusCode = 400,
                        ContentType = "application/json; charset=utf-8",
                        Body = "{\"error\":\"Bad request\"}"
                    };

                return new HttpResponseInfo
                {
                    StatusCode = 200,
                    ContentType = "application/json; charset=utf-8",
                    Body = "{}"
                };
            }

            return NotFound();
        }

        private static HttpResponseInfo NotFound()
        {
            return new HttpResponseInfo { StatusCode = 404 };
        }

        private bool IsAuthorized(HttpRequestInfo request)
        {
            if (_settings.MatchesPin(request.PinHeader))
                return true;

            return _settings.MatchesPin(request.PinCookie);
        }

        private HttpResponseInfo Health()
        {
            string json = "{\"duty\":true,\"viewers\":" + _viewers.Count
                + ",\"version\":\"" + AppVersion.Display + "\"}";
            return new HttpResponseInfo
            {
                StatusCode = 200,
                ContentType = "application/json; charset=utf-8",
                Body = json
            };
        }

        /// <summary>
        /// PIN уже проверен. Только заряд — без PIN и без /health-полей.
        /// </summary>
        private HttpResponseInfo Status()
        {
            string battery = _battery?.TryGetPercent() is int percent
                ? percent.ToString()
                : "null";
            return new HttpResponseInfo
            {
                StatusCode = 200,
                ContentType = "application/json; charset=utf-8",
                Body = "{\"battery\":" + battery + "}"
            };
        }

        private static HttpResponseInfo Unauthorized()
        {
            // Одна и та же 401: не говорим, пустой PIN, неверный или его нет.
            return new HttpResponseInfo { StatusCode = 401 };
        }
        private static bool IsGet(string? method)
        {
            return string.Equals(method, "GET", StringComparison.Ordinal);
        }
        private static bool IsPost(string? method)
        {
            return string.Equals(method, "POST", StringComparison.Ordinal);
        }
    }
}
