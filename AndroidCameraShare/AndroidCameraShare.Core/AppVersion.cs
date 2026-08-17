using System.Reflection;

namespace AndroidCameraShare.Core
{
    /// <summary>
    /// Версия из сборки (Directory.Build.props → Version). Один источник для UI и /health.
    /// </summary>
    public static class AppVersion
    {
        public static string Display { get; } = ReadDisplay();

        private static string ReadDisplay()
        {
            AssemblyInformationalVersionAttribute? attr = typeof(AppVersion).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string? value = attr?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(value))
            {
                return "0.0.0";
            }

            int plus = value.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? value : value[..plus];
        }
    }
}
