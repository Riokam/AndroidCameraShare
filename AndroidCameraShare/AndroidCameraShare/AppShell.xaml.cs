namespace AndroidCameraShare
{
    public partial class AppShell : Shell
    {
        public AppShell(MainPage mainPage)
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(CameraPreviewPage), typeof(CameraPreviewPage));
            Items.Add(new ShellContent
            {
                Title = "CameraShare",
                Content = mainPage,
                Route = nameof(MainPage)
            });
        }
    }
}
