using AndroidCameraShare.Core;
using Microsoft.Extensions.Logging;

namespace AndroidCameraShare.Tests
{
    public sealed class RotatingFileLoggerTests
    {
        [Fact]
        public void Sanitize_WhenSecretsArePresent_RedactsThem()
        {
            string message = "X-Pin: 1234 cookie=pin-value {\"sdp\":\"v=0 secret\"}";

            string sanitized = LogSanitizer.Sanitize(message);

            Assert.DoesNotContain("1234", sanitized, StringComparison.Ordinal);
            Assert.DoesNotContain("pin-value", sanitized, StringComparison.Ordinal);
            Assert.DoesNotContain("v=0", sanitized, StringComparison.Ordinal);
            Assert.Contains("[redacted]", sanitized, StringComparison.Ordinal);
        }

        [Fact]
        public void Provider_WhenFileExceedsLimit_RotatesAndFlushes()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                $"camera-share-logs-{Guid.NewGuid():N}");

            try
            {
                using (RotatingFileLoggerProvider provider = new RotatingFileLoggerProvider(
                    directory,
                    maxBytes: 180,
                    maxFiles: 2))
                {
                    ILogger logger = provider.CreateLogger("RotationTest");
                    for (int index = 0; index < 20; index++)
                    {
                        logger.LogInformation(
                            "Запись {Index}: проверка ограниченного файлового журнала",
                            index);
                    }
                }

                Assert.True(File.Exists(Path.Combine(directory, "camera-share.log")));
                Assert.True(File.Exists(Path.Combine(directory, "camera-share.log.1")));
                Assert.False(File.Exists(Path.Combine(directory, "camera-share.log.2")));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }
}
