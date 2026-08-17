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
            Assembly assembly = typeof(AppVersion).Assembly;
            string? informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            string fromInfo = TrimRevision(informational);
            if (IsUsable(fromInfo))
            {
                return fromInfo;
            }

            // Атрибут на Android может срезать линкер; Version сборки остаётся.
            Version? assemblyVersion = assembly.GetName().Version;
            if (assemblyVersion is not null)
            {
                return assemblyVersion.ToString(3);
            }

            return "0.0.0";
        }

        private static string TrimRevision(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            int plus = value.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? value : value[..plus];
        }

        private static bool IsUsable(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value != "0.0.0";
        }
    }
}
