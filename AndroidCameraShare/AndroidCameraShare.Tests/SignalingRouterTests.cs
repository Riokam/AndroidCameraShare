using AndroidCameraShare.Core;

namespace AndroidCameraShare.Tests
{
    public class SignalingRouterTests
    {
        private const string StoredPin = "1234";
        [Fact]
        public void Route_WhenUnknownPath_Returns404()
        {
            SignalingRouter router = CreateRouter();
            HttpResponseInfo response = router.Route(Get("/secret"));
            Assert.Equal(404, response.StatusCode);
        }
        [Fact]
        public void Route_WhenFavicon_Returns204WithoutPin()
        {
            SignalingRouter router = CreateRouter();
            HttpResponseInfo response = router.Route(Get("/favicon.ico"));
            Assert.Equal(204, response.StatusCode);
            Assert.Empty(response.Body);
        }
        [Fact]
        public void Route_WhenPathContainsDotDot_Returns404()
        {
            SignalingRouter router = CreateRouter();
            HttpResponseInfo response = router.Route(Get("/health/../offer"));
            Assert.Equal(404, response.StatusCode);
        }
        [Fact]
        public void Route_WhenHealth_ReturnsDutyAndViewersWithoutPin()
        {
            AppSettings settings = CreateSettings();
            ViewerCounter viewers = new ViewerCounter();
            viewers.RegisterSession();
            SignalingRouter router = new SignalingRouter(settings, viewers);
            HttpResponseInfo response = router.Route(Get("/health"));
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("{\"duty\":true,\"viewers\":1,\"version\":\"" + AppVersion.Display + "\"}", response.Body);
            Assert.DoesNotContain(StoredPin, response.Body);
            Assert.DoesNotContain("pin", response.Body, StringComparison.OrdinalIgnoreCase);
        }
        [Fact]
        public void Route_WhenRootWithoutPin_Returns401()
        {
            SignalingRouter router = CreateRouter();
            HttpResponseInfo response = router.Route(Get("/"));

            Assert.Equal(401, response.StatusCode);
            Assert.StartsWith("text/html", response.ContentType, StringComparison.Ordinal);
            Assert.Contains("pin", response.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(StoredPin, response.Body);
            Assert.DoesNotContain("RTCPeerConnection", response.Body, StringComparison.Ordinal);
        }
        [Fact]
        public void Route_WhenPinOnlyInQuery_Returns401()
        {
            SignalingRouter router = CreateRouter();
            HttpRequestInfo request = new HttpRequestInfo
            {
                Method = "GET",
                Path = "/",
                PinQuery = StoredPin
            };
            HttpResponseInfo response = router.Route(request);
            Assert.Equal(401, response.StatusCode);
        }
        [Fact]
        public void Route_WhenRootWithHeaderPin_Returns200()
        {
            SignalingRouter router = CreateRouter();
            HttpRequestInfo request = new HttpRequestInfo
            {
                Method = "GET",
                Path = "/",
                PinHeader = StoredPin
            };
            HttpResponseInfo response = router.Route(request);
            Assert.Equal(200, response.StatusCode);
            Assert.StartsWith("text/html", response.ContentType, StringComparison.Ordinal);
            Assert.Contains("video", response.Body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("RTCPeerConnection", response.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(StoredPin, response.Body);
        }
        [Fact]
        public void Route_WhenRootWithCookiePin_ReturnsWatchPage()
        {
            SignalingRouter router = CreateRouter();
            HttpRequestInfo request = new HttpRequestInfo
            {
                Method = "GET",
                Path = "/",
                PinCookie = StoredPin
            };

            HttpResponseInfo response = router.Route(request);

            Assert.Equal(200, response.StatusCode);
            Assert.Contains("RTCPeerConnection", response.Body, StringComparison.Ordinal);
            Assert.Contains("waitIceComplete(pc, 3000)", response.Body, StringComparison.Ordinal);
            Assert.Contains("AbortController", response.Body, StringComparison.Ordinal);
            Assert.Contains("landscape-rotate", response.Body, StringComparison.Ordinal);
            Assert.Contains("Смотреть", response.Body, StringComparison.Ordinal);
            Assert.Contains("/hangup", response.Body, StringComparison.Ordinal);
            Assert.Contains("/status", response.Body, StringComparison.Ordinal);
            Assert.Contains("30000", response.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("?pin", response.Body, StringComparison.Ordinal);
        }
        [Fact]
        public void Route_WhenHangupWithoutPin_Returns401()
        {
            SignalingRouter router = CreateRouter();
            HttpRequestInfo request = new HttpRequestInfo
            {
                Method = "POST",
                Path = "/hangup"
            };
            HttpResponseInfo response = router.Route(request);
            Assert.Equal(401, response.StatusCode);
        }
        [Fact]
        public void Route_WhenHangupAuthorized_Returns200WithoutBodyRequirement()
        {
            SignalingRouter router = CreateRouter();
            HttpRequestInfo request = new HttpRequestInfo
            {
                Method = "POST",
                Path = "/hangup",
                PinHeader = StoredPin,
                BodyLength = 0
            };
            HttpResponseInfo response = router.Route(request);
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("{}", response.Body);
        }
        [Fact]
        public void Route_WhenHangupPinOnlyInQuery_Returns401()
        {
            SignalingRouter router = CreateRouter();
            HttpRequestInfo request = new HttpRequestInfo
            {
                Method = "POST",
                Path = "/hangup",
                PinQuery = StoredPin
            };
            HttpResponseInfo response = router.Route(request);
            Assert.Equal(401, response.StatusCode);
        }
        [Fact]
        public void Route_WhenStatusWithoutPin_Returns401()
        {
            SignalingRouter router = CreateRouter();
            HttpResponseInfo response = router.Route(Get("/status"));
            Assert.Equal(401, response.StatusCode);
        }
        [Fact]
        public void Route_WhenStatusAuthorized_ReturnsBatteryJson()
        {
            SignalingRouter router = new SignalingRouter(CreateSettings(), new ViewerCounter(), new FixedBattery(87));
            HttpRequestInfo request = new HttpRequestInfo
            {
                Method = "GET",
                Path = "/status",
                PinCookie = StoredPin
            };
            HttpResponseInfo response = router.Route(request);
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("{\"battery\":87,\"camera\":\"back\"}", response.Body);
            Assert.DoesNotContain(StoredPin, response.Body);
        }
        [Fact]
        public void Route_WhenStatusPinOnlyInQuery_Returns401()
        {
            SignalingRouter router = CreateRouter();
            HttpRequestInfo request = new HttpRequestInfo
            {
                Method = "GET",
                Path = "/status",
                PinQuery = StoredPin
            };
            HttpResponseInfo response = router.Route(request);
            Assert.Equal(401, response.StatusCode);
        }
        [Fact]
        public void Route_WhenHealth_DoesNotIncludeBattery()
        {
            SignalingRouter router = new SignalingRouter(CreateSettings(), new ViewerCounter(), new FixedBattery(87));
            HttpResponseInfo response = router.Route(Get("/health"));
            Assert.DoesNotContain("battery", response.Body, StringComparison.OrdinalIgnoreCase);
        }
        [Fact]
        public void Route_WhenOfferBodyTooLarge_Returns413()
        {
            SignalingRouter router = CreateRouter();
            HttpRequestInfo request = new HttpRequestInfo
            {
                Method = "POST",
                Path = "/offer",
                PinHeader = StoredPin,
                BodyLength = NannyConstants.MaxOfferBodyBytes + 1
            };
            HttpResponseInfo response = router.Route(request);
            Assert.Equal(413, response.StatusCode);
        }
        [Fact]
        public void Route_WhenOfferEmptyBody_Returns400()
        {
            SignalingRouter router = CreateRouter();
            HttpRequestInfo request = new HttpRequestInfo
            {
                Method = "POST",
                Path = "/offer",
                PinHeader = StoredPin,
                BodyLength = 0
            };
            HttpResponseInfo response = router.Route(request);
            Assert.Equal(400, response.StatusCode);
        }
        [Fact]
        public void Route_WhenOfferAuthorized_DoesNotChangeViewerCount()
        {
            ViewerCounter viewers = new ViewerCounter();
            SignalingRouter router = new SignalingRouter(CreateSettings(), viewers);
            HttpRequestInfo request = new HttpRequestInfo
            {
                Method = "POST",
                Path = "/offer",
                PinHeader = StoredPin,
                BodyLength = 16
            };
            HttpResponseInfo response = router.Route(request);
            Assert.Equal(200, response.StatusCode);
            Assert.Equal(0, viewers.Count);
        }
        private static SignalingRouter CreateRouter()
        {
            return new SignalingRouter(CreateSettings(), new ViewerCounter());
        }
        private static AppSettings CreateSettings()
        {
            AppSettings settings = new AppSettings();
            settings.TrySetPin(StoredPin);
            return settings;
        }
        private static HttpRequestInfo Get(string path)
        {
            return new HttpRequestInfo
            {
                Method = "GET",
                Path = path
            };
        }

        private sealed class FixedBattery : IBatteryStatus
        {
            private readonly int _percent;

            public FixedBattery(int percent)
            {
                _percent = percent;
            }

            public int? TryGetPercent()
            {
                return _percent;
            }
        }

    }
}
