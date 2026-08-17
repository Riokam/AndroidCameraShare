using Microsoft.Extensions.Logging;
using AndroidCameraShare.Core;

namespace AndroidCameraShare
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                })
                .ConfigureMauiHandlers(handlers =>
                {
#if ANDROID
                    handlers.AddHandler<CameraPreviewView, CameraPreviewViewHandler>();
#endif
                });

            builder.Services.AddSingleton<AppSettings>();
            builder.Services.AddSingleton<AppSettingsStore>();
            builder.Services.AddSingleton<ViewerCounter>();
            builder.Services.AddSingleton<PowerPolicy>();
            builder.Services.AddSingleton<IBatteryStatus, AndroidBatteryStatus>();
            builder.Services.AddSingleton<IOfferHandler, WebRtcHost>();
            builder.Services.AddSingleton<SignalingServer>();
            builder.Services.AddSingleton<IDutyController, DutyController>();
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<CameraPreviewPage>();
            builder.Services.AddSingleton<AppShell>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
