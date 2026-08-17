using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidCameraShare.Core;
using AndroidX.Core.App;
using Microsoft.Extensions.Logging;
using Handler = Android.OS.Handler;

namespace AndroidCameraShare
{

    /// <summary>
    /// Держит процесс и тихое уведомление. HTTP и камеру не создаёт сама.
    /// </summary>
    [Service(
        Name = "com.androidcamerashare.app.DutyService",
        Exported = false)]
    public sealed class DutyService : Service
    {
        public const string ActionStop = "com.androidcamerashare.app.STOP_DUTY";

        private const string ChannelId = "nanny.duty";
        private const int NotificationId = 1;

        private readonly Handler _mainHandler = new Handler(Looper.MainLooper!);
        private ViewerCounter? _viewers;
        private IDutyController? _duty;
        private ILogger<DutyService>? _logger;
        private bool _needsOpenAppForCamera;

        public override IBinder? OnBind(Intent? intent)
        {
            return null;
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            try
            {
                IServiceProvider services = IPlatformApplication.Current!.Services;
                _viewers = services.GetRequiredService<ViewerCounter>();
                _duty = services.GetRequiredService<IDutyController>();
                _logger = services.GetRequiredService<ILogger<DutyService>>();

                if (intent?.Action == ActionStop)
                {
                    _ = StopDutyFromNotificationAsync();
                    return StartCommandResult.NotSticky;
                }

                if (_viewers is not null)
                {
                    _viewers.Changed -= OnViewersChanged;
                    _viewers.Changed += OnViewersChanged;
                }

                StartForegroundSafe();
                _ = RestoreHttpIfNeededAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Сбой старта дежурного сервиса");
                try
                {
                    StopSelf();
                }
                catch (Exception)
                {
                    // Процесс должен пережить ошибку FGS.
                }
            }

            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            if (_viewers is not null)
            {
                _viewers.Changed -= OnViewersChanged;
            }

            DismissNotification();
            base.OnDestroy();
        }

        private void OnViewersChanged()
        {
            // StartForeground только с главного потока: hangup приходит с HTTP/signaling.
            _mainHandler.Post(UpdateForegroundNotification);
        }

        private void UpdateForegroundNotification()
        {
            try
            {
                bool cameraOn = HasCameraPermission() && (_viewers?.HasViewer ?? false);
                try
                {
                    StartForegroundWithType(includeCamera: cameraOn);
                }
                catch (Exception ex) when (cameraOn)
                {
                    _logger?.LogWarning(ex, "Тип camera у FGS не принят, оставляем dataSync");
                    StartForegroundWithType(includeCamera: false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Не удалось обновить уведомление дежурства");
                try
                {
                    NotificationManagerCompat.From(this)?.Notify(NotificationId, BuildNotification());
                }
                catch (Exception)
                {
                    // Уведомление не должно ронять дежурство.
                }
            }
        }

        /// <summary>
        /// После abort процесса Android поднимает FGS заново — HTTP сам не оживёт.
        /// </summary>
        private async Task RestoreHttpIfNeededAsync()
        {
            try
            {
                if (_duty is DutyController controller && !controller.IsRunning)
                {
                    _logger?.LogWarning("Дежурный HTTP не слушает, поднимаем снова");
                    await controller.StartFromBootAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Не удалось восстановить дежурный HTTP");
            }
        }

        private void StartForegroundSafe()
        {
            CreateChannel();
            _needsOpenAppForCamera = false;

            try
            {
                // Тип camera только пока идёт съёмка. Иначе Android 14+ гасит службу после hangup.
                StartForegroundWithType(includeCamera: false);
            }
            catch (Exception ex) when (IsForegroundTypeRejected(ex))
            {
                _logger?.LogWarning(ex, "Тип FGS запрещён");
                _needsOpenAppForCamera = true;
                StartForegroundWithType(includeCamera: false);
            }
        }

        private void StartForegroundWithType(bool includeCamera)
        {
            Notification notification = BuildNotification();

            if (OperatingSystem.IsAndroidVersionAtLeast(30) && includeCamera)
            {
                StartForeground(
                    NotificationId,
                    notification,
                    ForegroundService.TypeDataSync | ForegroundService.TypeCamera);
                return;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
                return;
            }

            StartForeground(NotificationId, notification);
        }

        private static bool IsForegroundTypeRejected(Exception ex)
        {
            // Имя типа — без ссылки на API 31, minSdk у проекта 26.
            return ex is Java.Lang.SecurityException
                || ex.GetType().Name == "ForegroundServiceStartNotAllowedException";
        }

        private async Task StopDutyFromNotificationAsync()
        {
            try
            {
                if (_duty is DutyController controller)
                {
                    await controller.StopFromServiceAsync();
                }
            }
            finally
            {
                // GetService с ActionStop не уничтожает FGS — только шлёт OnStartCommand.
                DismissNotification();
                StopSelf();
            }
        }

        private void DismissNotification()
        {
            try
            {
                StopForeground(StopForegroundFlags.Remove);
            }
            catch (Exception)
            {
                // Служба могла уже не быть foreground.
            }

            NotificationManagerCompat.From(this)?.Cancel(NotificationId);
        }

        private Notification BuildNotification()
        {
            int count = _viewers?.Count ?? 0;
            string title = count == 0
                ? $"Няня ждёт зрителя · {count} подключений"
                : $"Идёт просмотр · {count} подключений";

            Intent launchIntent = PackageManager!.GetLaunchIntentForPackage(PackageName!)!;
            PendingIntent contentIntent = PendingIntent.GetActivity(
                this,
                0,
                launchIntent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;

            Intent stopIntent = new Intent(this, typeof(DutyService));
            stopIntent.SetAction(ActionStop);
            PendingIntent stopPending = PendingIntent.GetService(
                this,
                1,
                stopIntent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;

            NotificationCompat.Builder builder = new NotificationCompat.Builder(this, ChannelId);
            builder.SetContentTitle(title);
            builder.SetContentText(_needsOpenAppForCamera
                ? "Откройте няню, чтобы разрешить камеру"
                : "Дежурный режим");
            builder.SetSmallIcon(Android.Resource.Drawable.IcDialogInfo);
            builder.SetContentIntent(contentIntent);
            builder.SetOngoing(true);
            builder.SetOnlyAlertOnce(true);
            builder.SetSilent(true);
            builder.AddAction(Android.Resource.Drawable.IcMenuCloseClearCancel, "Стоп", stopPending);
            return builder.Build()!;
        }

        private void CreateChannel()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                return;
            }

            NotificationChannel channel = new NotificationChannel(
                ChannelId,
                "Дежурный режим",
                NotificationImportance.Low)
            {
                Description = "Няня слушает HTTP, камера выкл"
            };
            channel.EnableVibration(false);
            channel.EnableLights(false);
            channel.SetSound(null, null);

            NotificationManager manager = (NotificationManager)GetSystemService(NotificationService)!;
            manager.CreateNotificationChannel(channel);
        }

        private bool HasCameraPermission()
        {
            return CheckSelfPermission(Manifest.Permission.Camera) == Permission.Granted;
        }
    }
}