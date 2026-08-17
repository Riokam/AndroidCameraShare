using System.Reflection;

namespace AndroidCameraShare.Core
{
    /// <summary>
    /// HTML зрителя из Pages/*.html. Телефон и тесты читают один встроенный ресурс.
    /// </summary>
    public static class ViewerPage
    {
        public static string PinFormHtml { get; } = ReadEmbedded("pin.html");

        public static string WatchHtml { get; } = ReadEmbedded("watch.html");

        private static string ReadEmbedded(string fileName)
        {
            Assembly assembly = typeof(ViewerPage).Assembly;
            string resourceName = "AndroidCameraShare.Core.Pages." + fileName;
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                throw new InvalidOperationException("Нет страницы " + fileName);
            }

            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
