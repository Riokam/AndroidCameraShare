using System.Text.RegularExpressions;
using AndroidCameraShare.Core;

namespace AndroidCameraShare.Tests
{
    public class AppVersionTests
    {
        [Fact]
        public void Display_WhenBuilt_MatchesDirectoryBuildProps()
        {
            string? directory = AppContext.BaseDirectory;
            string? propsPath = null;
            while (!string.IsNullOrEmpty(directory))
            {
                string candidate = Path.Combine(directory, "Directory.Build.props");
                if (File.Exists(candidate))
                {
                    propsPath = candidate;
                    break;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            Assert.False(string.IsNullOrEmpty(propsPath));
            Match match = Regex.Match(File.ReadAllText(propsPath), @"<Version>([^<]+)</Version>");
            Assert.True(match.Success);
            Assert.Equal(match.Groups[1].Value.Trim(), AppVersion.Display);
        }
    }
}
