using AndroidCameraShare.Core;

namespace AndroidCameraShare
{
    public partial class App : Application
    {
        private readonly AppShell _shell;

        public App(AppShell shell, AppSettings settings)
        {
            InitializeComponent();
            _shell = shell;
            UserAppTheme = ToAppTheme(settings.ThemeMode);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(_shell);
        }

        /// <summary>
        /// Применяет выбранную тему сразу ко всем страницам приложения.
        /// </summary>
        public static void ApplyTheme(AppThemeMode themeMode)
        {
            if (Current is not null)
            {
                Current.UserAppTheme = ToAppTheme(themeMode);
            }
        }

        private static AppTheme ToAppTheme(AppThemeMode themeMode)
        {
            return themeMode switch
            {
                AppThemeMode.Light => AppTheme.Light,
                AppThemeMode.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
        }
    }
}
