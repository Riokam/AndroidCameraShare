using Android.Content;
using Android.Net.Wifi;
using Android.OS;
using AndroidCameraShare.Core;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace AndroidCameraShare
{
    /// <summary>
    /// Lock’и и гашение экрана только на время просмотра. В простое CPU может спать.
    /// </summary>
    public sealed class PowerPolicy
    {
        private const string CpuTag = "nanny:cpu";
        private const string WifiTag = "nanny:wifi";

        private readonly AppSettings _settings;
        private readonly ILogger<PowerPolicy> _logger;
        private readonly object _gate = new object();

        private PowerManager.WakeLock? _cpuLock;
        private WifiManager.WifiLock? _wifiLock;
        private bool _sessionActive;

        public PowerPolicy(AppSettings settings, ILogger<PowerPolicy> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        public void OnSessionStarted()
        {
            lock (_gate)
            {
                if (_sessionActive)
                {
                    return;
                }

                _sessionActive = true;
                AcquireCpuLock();
                if (_settings.PowerMode == PowerMode.Reliable)
                {
                    AcquireWifiLock();
                }

                if (_settings.ShouldDimScreen)
                {
                    DimScreen();
                }
            }
        }

        public void OnSessionEnded()
        {
            lock (_gate)
            {
                if (!_sessionActive)
                {
                    return;
                }

                _sessionActive = false;
                RestoreScreen();
                ReleaseWifiLock();
                ReleaseCpuLock();
            }
        }

        private void AcquireCpuLock()
        {
            if (_cpuLock is not null)
            {
                return;
            }

            PowerManager? power = Application.Context.GetSystemService(Context.PowerService) as PowerManager;
            _cpuLock = power?.NewWakeLock(WakeLockFlags.Partial, CpuTag);
            _cpuLock?.SetReferenceCounted(false);
            _cpuLock?.Acquire();
        }

        private void ReleaseCpuLock()
        {
            if (_cpuLock is null)
            {
                return;
            }

            try
            {
                if (_cpuLock.IsHeld)
                {
                    _cpuLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось отпустить CPU WakeLock");
            }
            finally
            {
                _cpuLock.Dispose();
                _cpuLock = null;
            }
        }

        private void AcquireWifiLock()
        {
            if (_wifiLock is not null)
            {
                return;
            }

            WifiManager? wifi = Application.Context.GetSystemService(Context.WifiService) as WifiManager;
            // Full, не FullHighPerf: LAN доступен, без high-perf.
            _wifiLock = wifi?.CreateWifiLock(Android.Net.WifiMode.Full, WifiTag);
            _wifiLock?.SetReferenceCounted(false);
            _wifiLock?.Acquire();
        }

        private void ReleaseWifiLock()
        {
            if (_wifiLock is null)
            {
                return;
            }

            try
            {
                if (_wifiLock.IsHeld)
                {
                    _wifiLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось отпустить WifiLock");
            }
            finally
            {
                _wifiLock.Dispose();
                _wifiLock = null;
            }
        }

        private void DimScreen()
        {
            MainActivity.TrySetSessionDimming(true);
        }

        private void RestoreScreen()
        {
            MainActivity.TrySetSessionDimming(false);
        }
    }
}
