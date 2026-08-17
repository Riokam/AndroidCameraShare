using Android.Content;
using Android.OS;
using AndroidCameraShare.Core;
using Application = Android.App.Application;

namespace AndroidCameraShare
{
    /// <summary>
    /// Заряд из BatteryManager. Без отдельного permission.
    /// </summary>
    public sealed class AndroidBatteryStatus : IBatteryStatus
    {
        public int? TryGetPercent()
        {
            BatteryManager? battery = Application.Context.GetSystemService(Context.BatteryService) as BatteryManager;
            if (battery is null)
            {
                return null;
            }

            int value = battery.GetIntProperty((int)BatteryProperty.Capacity);
            if (value < 0 || value > 100)
            {
                return null;
            }

            return value;
        }
    }
}
