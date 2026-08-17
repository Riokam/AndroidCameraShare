using AndroidCameraShare.Core;

namespace AndroidCameraShare.Tests
{
    public class AppSettingsTests
    {
        [Fact]
        public void Constructor_WhenCreate_HasDefaults()
        {
            AppSettings settings = new AppSettings();

            Assert.Equal(NannyConstants.DefaultPort, settings.Port);
            Assert.Equal(string.Empty, settings.Pin);
            Assert.Equal(CameraFacing.Back, settings.CameraFacing);
            Assert.False(settings.IsAutostartEnabled);
            Assert.Equal(PowerMode.Economy, settings.PowerMode);
            Assert.True(settings.ShouldDimScreen);

        }

        [Fact]
        public void TrySetPort_WhenTooLow_ReturnFalse()
        {
            AppSettings settings = new AppSettings();

            bool accepted = settings.TrySetPort(NannyConstants.MinPort - 1);

            Assert.False(accepted);
            Assert.Equal(NannyConstants.DefaultPort, settings.Port);
        }

        [Fact]
        public void TrySetPort_WhenTooHigh_ReturnsFalse()
        {
            AppSettings settings = new AppSettings();
            bool accepted = settings.TrySetPort(NannyConstants.MaxPort + 1);
            Assert.False(accepted);
            Assert.Equal(NannyConstants.DefaultPort, settings.Port);
        }
        [Fact]
        public void TrySetPort_WhenInRange_StoresPort()
        {
            AppSettings settings = new AppSettings();
            bool accepted = settings.TrySetPort(9090);
            Assert.True(accepted);
            Assert.Equal(9090, settings.Port);
        }
        [Fact]
        public void TrySetPort_WhenOutOfRange_KeepsPreviousPort()
        {
            AppSettings settings = new AppSettings();
            settings.TrySetPort(9090);
            bool accepted = settings.TrySetPort(80);
            Assert.False(accepted);
            Assert.Equal(9090, settings.Port);
        }
        [Fact]
        public void TrySetPin_WhenNotFourDigits_ReturnsFalse()
        {
            AppSettings settings = new AppSettings();
            Assert.False(settings.TrySetPin("12"));
            Assert.False(settings.TrySetPin("12345"));
            Assert.False(settings.TrySetPin("12ab"));
            Assert.False(settings.TrySetPin(null));
            Assert.Equal(string.Empty, settings.Pin);
        }
        [Fact]
        public void TrySetPin_WhenValid_StoresPin()
        {
            AppSettings settings = new AppSettings();
            bool accepted = settings.TrySetPin("1234");
            Assert.True(accepted);
            Assert.Equal("1234", settings.Pin);
        }
        [Fact]
        public void TrySetPin_WhenInvalid_KeepsPreviousPin()
        {
            AppSettings settings = new AppSettings();
            settings.TrySetPin("1234");
            bool accepted = settings.TrySetPin("abcd");
            Assert.False(accepted);
            Assert.Equal("1234", settings.Pin);
        }
        [Fact]
        public void MatchesPin_WhenCorrect_ReturnsTrue()
        {
            AppSettings settings = new AppSettings();
            settings.TrySetPin("1234");
            Assert.True(settings.MatchesPin("1234"));
        }
        [Fact]
        public void MatchesPin_WhenWrongOrEmpty_ReturnsFalse()
        {
            AppSettings settings = new AppSettings();
            Assert.False(settings.MatchesPin("1234"));
            settings.TrySetPin("1234");
            Assert.False(settings.MatchesPin("0000"));
            Assert.False(settings.MatchesPin("123"));
            Assert.False(settings.MatchesPin(null));
        }
    }
}
