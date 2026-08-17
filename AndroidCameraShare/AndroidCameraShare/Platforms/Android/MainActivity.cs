using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace AndroidCameraShare
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.ScreenSize
            | ConfigChanges.Orientation
            | ConfigChanges.UiMode
            | ConfigChanges.ScreenLayout
            | ConfigChanges.SmallestScreenSize
            | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private static readonly object InstanceGate = new object();
        private static WeakReference<MainActivity>? _current;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            lock (InstanceGate)
            {
                _current = new WeakReference<MainActivity>(this);
            }
        }

        protected override void OnDestroy()
        {
            lock (InstanceGate)
            {
                if (_current is not null && _current.TryGetTarget(out MainActivity? activity) && ReferenceEquals(activity, this))
                {
                    _current = null;
                }
            }

            base.OnDestroy();
        }

        /// <summary>
        /// Гасим окно няни на время съёмки. Без Activity (фон после reboot) просто ничего не делаем.
        /// </summary>
        public static void TrySetSessionDimming(bool dim)
        {
            MainActivity? activity;
            lock (InstanceGate)
            {
                if (_current is null || !_current.TryGetTarget(out activity) || activity is null)
                {
                    return;
                }
            }

            activity.RunOnUiThread(() => activity.ApplySessionDimming(dim));
        }

        private void ApplySessionDimming(bool dim)
        {
            Android.Views.Window? window = base.Window;
            if (window is null)
            {
                return;
            }

            WindowManagerLayoutParams attributes = window.Attributes!;
            attributes.ScreenBrightness = dim
                ? 0.01f
                : WindowManagerLayoutParams.BrightnessOverrideNone;
            window.Attributes = attributes;

            if (dim)
            {
                window.ClearFlags(WindowManagerFlags.KeepScreenOn);
            }
        }
    }
}
