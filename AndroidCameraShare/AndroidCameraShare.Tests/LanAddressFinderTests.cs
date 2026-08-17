using System.Net;
using AndroidCameraShare.Core;

namespace AndroidCameraShare.Tests
{
    public class LanAddressFinderTests
    {
        [Fact]
        public void TryPickIpv4_WhenOnlyLoopback_ReturnsNull()
        {
            string? ip = LanAddressFinder.TryPickIpv4([IPAddress.Loopback]);

            Assert.Null(ip);
        }

        [Fact]
        public void TryPickIpv4_WhenLinkLocal_ReturnsNull()
        {
            string? ip = LanAddressFinder.TryPickIpv4([IPAddress.Parse("169.254.10.20")]);

            Assert.Null(ip);
        }

        [Fact]
        public void TryPickIpv4_WhenPrivateIpv4_ReturnsThatAddress()
        {
            string? ip = LanAddressFinder.TryPickIpv4(
            [
                IPAddress.Loopback,
                IPAddress.Parse("169.254.1.1"),
                IPAddress.Parse("192.168.1.42")
            ]);

            Assert.Equal("192.168.1.42", ip);
        }
    }
}
