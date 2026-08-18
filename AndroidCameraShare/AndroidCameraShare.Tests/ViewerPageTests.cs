using AndroidCameraShare.Core;

namespace AndroidCameraShare.Tests
{
    public class ViewerPageTests
    {
        [Fact]
        public void PinFormHtml_WhenLoaded_AsksPinWithoutWebrtc()
        {
            string html = ViewerPage.PinFormHtml;
            Assert.Contains("pin", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<title>CameraShare</title>", html, StringComparison.Ordinal);
            Assert.Contains("Подключение к CameraShare", html, StringComparison.Ordinal);
            Assert.DoesNotContain("RTCPeerConnection", html, StringComparison.Ordinal);
            Assert.DoesNotContain("?pin", html, StringComparison.Ordinal);
            Assert.Contains("<form id=\"pin-form\"", html, StringComparison.Ordinal);
            Assert.Contains("type=\"submit\"", html, StringComparison.Ordinal);
            Assert.Contains("addEventListener('submit'", html, StringComparison.Ordinal);
            Assert.Contains("viewport-fit=cover", html, StringComparison.Ordinal);
            Assert.Contains("safe-area-inset", html, StringComparison.Ordinal);
        }

        [Fact]
        public void WatchHtml_WhenLoaded_HasPlayerAndSignaling()
        {
            string html = ViewerPage.WatchHtml;
            Assert.Contains("<title>CameraShare</title>", html, StringComparison.Ordinal);
            Assert.Contains("RTCPeerConnection", html, StringComparison.Ordinal);
            Assert.Contains("waitIceComplete(pc, 3000)", html, StringComparison.Ordinal);
            Assert.Contains("AbortController", html, StringComparison.Ordinal);
            Assert.Contains("landscape-rotate", html, StringComparison.Ordinal);
            Assert.Contains("class=\"hud\"", html, StringComparison.Ordinal);
            Assert.Contains("(hover: none) and (pointer: coarse)", html, StringComparison.Ordinal);
            Assert.Contains("controls-on", html, StringComparison.Ordinal);
            Assert.Contains("cameraFacing === 'back' ? 180 : 0", html, StringComparison.Ordinal);
            Assert.Contains("aria-label=\"Повернуть 180°\"", html, StringComparison.Ordinal);
            Assert.Contains("aria-label=\"Сменить камеру\"", html, StringComparison.Ordinal);
            Assert.Contains("aria-label=\"Полный экран\"", html, StringComparison.Ordinal);
            Assert.Contains("requestFullscreen", html, StringComparison.Ordinal);
            Assert.Contains("/camera", html, StringComparison.Ordinal);
            Assert.Contains("rotationOffset", html, StringComparison.Ordinal);
            Assert.Contains("icon-btn", html, StringComparison.Ordinal);
            Assert.Contains("aria-label=\"Смотреть\"", html, StringComparison.Ordinal);
            Assert.Contains("aria-label=\"Остановить просмотр\"", html, StringComparison.Ordinal);
            Assert.Contains("/hangup", html, StringComparison.Ordinal);
            Assert.Contains("X-Session", html, StringComparison.Ordinal);
            Assert.Contains("response.status === 409", html, StringComparison.Ordinal);
            Assert.Contains("Камера занята другим зрителем", html, StringComparison.Ordinal);
            Assert.Contains("reconnectOwnedSession", html, StringComparison.Ordinal);
            Assert.Contains("requestVideoFrameCallback", html, StringComparison.Ordinal);
            Assert.Contains("isVideoStale(2500)", html, StringComparison.Ordinal);
            Assert.Contains("aria-label=\"Переподключить\"", html, StringComparison.Ordinal);
            Assert.Contains("disconnectTimer", html, StringComparison.Ordinal);
            Assert.Contains("/status", html, StringComparison.Ordinal);
            Assert.Contains("30000", html, StringComparison.Ordinal);
            Assert.Contains("navigator.wakeLock.request('screen')", html, StringComparison.Ordinal);
            Assert.Contains("visibilitychange", html, StringComparison.Ordinal);
            Assert.Contains("pageshow", html, StringComparison.Ordinal);
            Assert.DoesNotContain("window.addEventListener('pagehide'", html, StringComparison.Ordinal);
            Assert.Contains("viewport-fit=cover", html, StringComparison.Ordinal);
            Assert.Contains("prefers-reduced-motion", html, StringComparison.Ordinal);
            Assert.Contains("-webkit-backdrop-filter", html, StringComparison.Ordinal);
            Assert.DoesNotContain("?pin", html, StringComparison.Ordinal);
        }
    }
}
