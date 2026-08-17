using Android.App;
using Android.Content;
using AndroidCameraShare.Core;
using Microsoft.Extensions.Logging;

namespace AndroidCameraShare
{
    /// <summary>
    /// Поднимает дежурный HTTP после reboot, если пользователь явно включил автозапуск.
    /// </summary>
    [BroadcastReceiver(
        Name = "com.androidcamerashare.app.BootReceiver",
        Enabled = true,
        Exported = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted, "android.intent.action.QUICKBOOT_POWERON" })]
    public sealed class BootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context is null || intent?.Action is null)
            {
                return;
            }

            bool isBoot = intent.Action == Intent.ActionBootCompleted
                || intent.Action == "android.intent.action.QUICKBOOT_POWERON";
            if (!isBoot)
            {
                return;
            }

            // OnReceive нельзя держать долго: GoAsync держит процесс, пока старт не закончится.
            PendingResult? pending = GoAsync();
            if (pending is null)
            {
                return;
            }

            _ = StartDutyAfterBootAsync(pending);
        }

        private static async Task StartDutyAfterBootAsync(PendingResult pending)
        {
            ILogger<BootReceiver>? logger = null;

            try
            {
                IServiceProvider? services = IPlatformApplication.Current?.Services;
                if (services is null)
                {
                    return;
                }

                logger = services.GetService<ILogger<BootReceiver>>();
                AppSettings settings = services.GetRequiredService<AppSettings>();
                if (!settings.IsAutostartEnabled)
                {
                    logger?.LogInformation("Автозапуск выключен, после reboot HTTP не поднимаем");
                    return;
                }

                IDutyController duty = services.GetRequiredService<IDutyController>();
                bool started = await duty.StartFromBootAsync();
                if (started)
                {
                    logger?.LogInformation("Дежурный HTTP поднят после reboot");
                }
                else
                {
                    logger?.LogWarning("Автозапуск после reboot не удался: {Error}", duty.LastError);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Сбой BootReceiver");
            }
            finally
            {
                pending.Finish();
            }
        }
    }
}
