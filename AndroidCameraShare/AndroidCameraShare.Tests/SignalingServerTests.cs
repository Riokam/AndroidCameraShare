using AndroidCameraShare.Core;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AndroidCameraShare.Tests
{
    public class SignalingServerTests
    {
        [Fact]
        public async Task TryStart_WhenLocalhost_HealthReturnsJson()
        {
            int port = GetFreePort();
            AppSettings settings = CreateSettings(port);
            CollectingLogger<SignalingServer> logger = new CollectingLogger<SignalingServer>();
            SignalingServer server = new SignalingServer(settings, new ViewerCounter(), logger);
            try
            {
                Assert.True(server.TryStart());
                using HttpClient client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(3)
                };
                HttpResponseMessage response = await client.GetAsync($"http://127.0.0.1:{port}/health");
                string body = await response.Content.ReadAsStringAsync();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("{\"duty\":true,\"viewers\":0,\"version\":\"" + AppVersion.Display + "\"}", body);
                Assert.Contains(
                    logger.Entries,
                    entry => entry.Level == LogLevel.Information && entry.Message.Contains(port.ToString()));
            }
            finally
            {
                await server.DisposeAsync();
            }
        }
        [Fact]
        public async Task TryStart_WhenPortBusy_ReturnsFalseAndSetsLastError()
        {
            int port = GetFreePort();
            AppSettings settings = CreateSettings(port);
            SignalingServer first = new SignalingServer(
                settings,
                new ViewerCounter(),
                new CollectingLogger<SignalingServer>());
            try
            {
                Assert.True(first.TryStart());
                CollectingLogger<SignalingServer> secondLogger = new CollectingLogger<SignalingServer>();
                SignalingServer second = new SignalingServer(
                    CreateSettings(port),
                    new ViewerCounter(),
                    secondLogger);
                bool started = second.TryStart();
                Assert.False(started);
                Assert.Equal($"Порт {port} занят", second.LastError);
                Assert.Contains(secondLogger.Entries, entry => entry.Level == LogLevel.Warning);
                await second.DisposeAsync();
            }
            finally
            {
                await first.DisposeAsync();
            }
        }
        [Fact]
        public async Task HandleRequest_WhenUnknownPath_ServerStillAnswersHealth()
        {
            int port = GetFreePort();
            SignalingServer server = new SignalingServer(
                CreateSettings(port),
                new ViewerCounter(),
                new CollectingLogger<SignalingServer>());
            try
            {
                Assert.True(server.TryStart());
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                HttpResponseMessage missing = await client.GetAsync($"http://127.0.0.1:{port}/no-such");
                HttpResponseMessage health = await client.GetAsync($"http://127.0.0.1:{port}/health");
                Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
                Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            }
            finally
            {
                await server.DisposeAsync();
            }
        }
        [Fact]
        public async Task StopAsync_WhenCalled_ReleasesPort()
        {
            int port = GetFreePort();
            SignalingServer first = new SignalingServer(
                CreateSettings(port),
                new ViewerCounter(),
                new CollectingLogger<SignalingServer>());
            Assert.True(first.TryStart());
            await first.DisposeAsync();
            SignalingServer second = new SignalingServer(
                CreateSettings(port),
                new ViewerCounter(),
                new CollectingLogger<SignalingServer>());
            try
            {
                Assert.True(second.TryStart());
            }
            finally
            {
                await second.DisposeAsync();
            }
        }

        [Fact]
        public async Task TryStart_WhenSameInstanceAfterStop_SucceedsAndClearsError()
        {
            int port = GetFreePort();
            SignalingServer server = new SignalingServer(
                CreateSettings(port),
                new ViewerCounter(),
                new CollectingLogger<SignalingServer>());
            try
            {
                Assert.True(server.TryStart());
                await server.StopAsync();
                Assert.False(server.IsRunning);
                Assert.Null(server.LastError);
                Assert.True(server.TryStart());
                Assert.True(server.IsRunning);
                Assert.Null(server.LastError);
            }
            finally
            {
                await server.DisposeAsync();
            }
        }

        [Fact]
        public async Task Offer_WhenHandlerFails_Returns500AndKeepsCounterAtZero()
        {
            int port = GetFreePort();
            ViewerCounter viewers = new ViewerCounter();
            FailingOfferHandler handler = new FailingOfferHandler(viewers);
            SignalingServer server = new SignalingServer(
                CreateSettings(port),
                viewers,
                new CollectingLogger<SignalingServer>(),
                handler);

            try
            {
                Assert.True(server.TryStart());
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                using HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"http://127.0.0.1:{port}/offer");
                request.Headers.Add("X-Pin", "1234");
                request.Content = new StringContent(
                    "{\"type\":\"offer\",\"sdp\":\"v=0\"}",
                    Encoding.UTF8,
                    "application/json");

                HttpResponseMessage response = await client.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();

                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
                Assert.Contains("Нет камеры", body, StringComparison.Ordinal);
                Assert.Equal(0, viewers.Count);
            }
            finally
            {
                await server.DisposeAsync();
            }
        }

        [Fact]
        public async Task Hangup_WhenAuthorized_StopsSession()
        {
            int port = GetFreePort();
            ViewerCounter viewers = new ViewerCounter();
            FailingOfferHandler handler = new FailingOfferHandler(viewers);
            SignalingServer server = new SignalingServer(
                CreateSettings(port),
                viewers,
                new CollectingLogger<SignalingServer>(),
                handler);

            try
            {
                Assert.True(server.TryStart());
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                using HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"http://127.0.0.1:{port}/hangup");
                request.Headers.Add("X-Pin", "1234");

                HttpResponseMessage response = await client.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("{}", body);
                Assert.Equal(1, handler.StopCount);
            }
            finally
            {
                await server.DisposeAsync();
            }
        }

        [Fact]
        public async Task Hangup_WhenNoPin_Returns401AndDoesNotStop()
        {
            int port = GetFreePort();
            ViewerCounter viewers = new ViewerCounter();
            FailingOfferHandler handler = new FailingOfferHandler(viewers);
            SignalingServer server = new SignalingServer(
                CreateSettings(port),
                viewers,
                new CollectingLogger<SignalingServer>(),
                handler);

            try
            {
                Assert.True(server.TryStart());
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                HttpResponseMessage response = await client.PostAsync(
                    $"http://127.0.0.1:{port}/hangup",
                    content: null);

                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.Equal(0, handler.StopCount);
            }
            finally
            {
                await server.DisposeAsync();
            }
        }

        [Fact]
        public async Task Hangup_WhenSessionIsStale_Returns409AndDoesNotStop()
        {
            int port = GetFreePort();
            ViewerCounter viewers = new ViewerCounter();
            FailingOfferHandler handler = new FailingOfferHandler(viewers)
            {
                AcceptSessionCommands = false
            };
            SignalingServer server = new SignalingServer(
                CreateSettings(port),
                viewers,
                new CollectingLogger<SignalingServer>(),
                handler);

            try
            {
                Assert.True(server.TryStart());
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                using HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"http://127.0.0.1:{port}/hangup");
                request.Headers.Add(NannyConstants.PinHeaderName, "1234");
                request.Headers.Add(NannyConstants.SessionHeaderName, "stale");

                HttpResponseMessage response = await client.SendAsync(request);

                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                Assert.Equal(0, handler.StopCount);
            }
            finally
            {
                await server.DisposeAsync();
            }
        }

        [Fact]
        public async Task Camera_WhenAuthorized_TogglesFacingAndSwitches()
        {
            int port = GetFreePort();
            AppSettings settings = CreateSettings(port);
            ViewerCounter viewers = new ViewerCounter();
            FailingOfferHandler handler = new FailingOfferHandler(viewers);
            SignalingServer server = new SignalingServer(
                settings,
                viewers,
                new CollectingLogger<SignalingServer>(),
                handler);

            try
            {
                Assert.True(server.TryStart());
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                using HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"http://127.0.0.1:{port}/camera");
                request.Headers.Add("X-Pin", "1234");

                HttpResponseMessage response = await client.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("{}", body);
                Assert.Equal(CameraFacing.Front, settings.CameraFacing);
                Assert.Equal(1, handler.SwitchCount);
            }
            finally
            {
                await server.DisposeAsync();
            }
        }

        [Fact]
        public async Task Camera_WhenNoPin_Returns401AndDoesNotSwitch()
        {
            int port = GetFreePort();
            AppSettings settings = CreateSettings(port);
            ViewerCounter viewers = new ViewerCounter();
            FailingOfferHandler handler = new FailingOfferHandler(viewers);
            SignalingServer server = new SignalingServer(
                settings,
                viewers,
                new CollectingLogger<SignalingServer>(),
                handler);

            try
            {
                Assert.True(server.TryStart());
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                HttpResponseMessage response = await client.PostAsync(
                    $"http://127.0.0.1:{port}/camera",
                    content: null);

                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.Equal(CameraFacing.Back, settings.CameraFacing);
                Assert.Equal(0, handler.SwitchCount);
            }
            finally
            {
                await server.DisposeAsync();
            }
        }

        [Fact]
        public async Task Status_WhenAuthorized_ReturnsBattery()
        {
            int port = GetFreePort();
            SignalingServer server = new SignalingServer(
                CreateSettings(port),
                new ViewerCounter(),
                new CollectingLogger<SignalingServer>(),
                offers: null,
                battery: new FixedPercentBattery(64));

            try
            {
                Assert.True(server.TryStart());
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                using HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"http://127.0.0.1:{port}/status");
                request.Headers.Add("X-Pin", "1234");

                HttpResponseMessage response = await client.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("{\"battery\":64,\"camera\":\"back\"}", body);
            }
            finally
            {
                await server.DisposeAsync();
            }
        }

        private sealed class FailingOfferHandler : IOfferHandler
        {
            private readonly ViewerCounter _viewers;

            public FailingOfferHandler(ViewerCounter viewers)
            {
                _viewers = viewers;
            }

            public string? LastError { get; private set; }

            public bool HasLiveSession { get; private set; }

            public int StopCount { get; private set; }

            public int SwitchCount { get; private set; }

            public bool AcceptSessionCommands { get; init; } = true;

            public Task<HttpResponseInfo> HandleOfferAsync(string body, CancellationToken cancellationToken)
            {
                LastError = "Нет камеры";
                _viewers.Reset();
                return Task.FromResult(new HttpResponseInfo
                {
                    StatusCode = 500,
                    ContentType = "application/json; charset=utf-8",
                    Body = OfferSdp.ToErrorJson("Нет камеры")
                });
            }

            public Task StopSessionAsync()
            {
                StopCount++;
                HasLiveSession = false;
                _viewers.Reset();
                return Task.CompletedTask;
            }

            public async Task<bool> StopSessionAsync(string? sessionId)
            {
                if (!AcceptSessionCommands)
                {
                    return false;
                }

                await StopSessionAsync();
                return true;
            }

            public Task SwitchCameraAsync()
            {
                SwitchCount++;
                return Task.CompletedTask;
            }

            public async Task<bool> SwitchCameraAsync(string? sessionId)
            {
                if (!AcceptSessionCommands)
                {
                    return false;
                }

                await SwitchCameraAsync();
                return true;
            }
        }
        private static AppSettings CreateSettings(int port)
        {
            AppSettings settings = new AppSettings();
            settings.TrySetPort(port);
            settings.TrySetPin("1234");
            return settings;
        }
        private static int GetFreePort()
        {
            TcpListener tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            int port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();
            return port;
        }

        private sealed class FixedPercentBattery : IBatteryStatus
        {
            private readonly int _percent;

            public FixedPercentBattery(int percent)
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
