using AndroidCameraShare.Core;

namespace AndroidCameraShare.Tests
{
    public class OfferSdpTests
    {
        [Fact]
        public void TryReadOffer_WhenValidJson_ReturnsSdp()
        {
            bool parsed = OfferSdp.TryReadOffer("{\"type\":\"offer\",\"sdp\":\"v=0\"}", out string sdp);

            Assert.True(parsed);
            Assert.Equal("v=0", sdp);
        }

        [Fact]
        public void TryReadOffer_WhenGarbage_ReturnsFalse()
        {
            Assert.False(OfferSdp.TryReadOffer("not-json", out _));
            Assert.False(OfferSdp.TryReadOffer("{}", out _));
            Assert.False(OfferSdp.TryReadOffer(null, out _));
        }

        [Fact]
        public void ToAnswerJson_WhenCalled_DoesNotEmbedPin()
        {
            string json = OfferSdp.ToAnswerJson("v=0", "session-1");

            Assert.Contains("answer", json, StringComparison.Ordinal);
            Assert.Contains("v=0", json, StringComparison.Ordinal);
            Assert.Contains("session-1", json, StringComparison.Ordinal);
            Assert.DoesNotContain("pin", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ToErrorJson_WhenCalled_HasErrorField()
        {
            string json = OfferSdp.ToErrorJson("Нет камеры");

            Assert.Contains("Нет камеры", json, StringComparison.Ordinal);
            Assert.DoesNotContain("sdp", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
