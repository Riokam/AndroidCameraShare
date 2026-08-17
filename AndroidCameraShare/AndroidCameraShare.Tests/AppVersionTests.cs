using AndroidCameraShare.Core;

namespace AndroidCameraShare.Tests
{
    public class AppVersionTests
    {
        [Fact]
        public void Display_WhenBuilt_IsSemVer()
        {
            Assert.Matches(@"^\d+\.\d+\.\d+", AppVersion.Display);
            Assert.Equal("1.1.0", AppVersion.Display);
        }
    }
}
