#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Net.Wifi;
using AndroidX.Core.App;

namespace MonopolyBank.Platforms.Android
{
    [Service(ForegroundServiceType = ForegroundService.TypeDataSync, Exported = false)]
    public class GameHostService : Service
    {
        private const int NotificationId = 2026;
        private const string ChannelId = "monopoly_host_channel";
        private PowerManager.WakeLock? _wakeLock;
        private WifiManager.WifiLock? _wifiLock;

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            CreateNotificationChannel();

            var notificationIntent = new Intent(this, typeof(MainActivity));
            var pendingIntent = PendingIntent.GetActivity(this, 0, notificationIntent, 
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            int iconId = Resources?.GetIdentifier("appicon", "mipmap", PackageName) ?? 0;
            if (iconId == 0) iconId = global::Android.Resource.Drawable.StatNotifySync;

            var notification = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle("🎲 Monopoly Bank — Сервер активен")
                .SetContentText("Комната игры открыта и работает в фоновом режиме")
                .SetSmallIcon(iconId)
                .SetOngoing(true)
                .SetContentIntent(pendingIntent)
                .Build();

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
            }
            else
            {
                StartForeground(NotificationId, notification);
            }

            AcquireLocks();

            return StartCommandResult.Sticky;
        }

        private void AcquireLocks()
        {
            try
            {
                var pm = (PowerManager?)GetSystemService(PowerService);
                if (pm != null && _wakeLock == null)
                {
                    _wakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "MonopolyBank:HostWakeLock");
                    _wakeLock.Acquire();
                }

                var wm = (WifiManager?)GetSystemService(WifiService);
                if (wm != null && _wifiLock == null)
                {
                    _wifiLock = wm.CreateWifiLock("MonopolyBank:HostWifiLock");
                    _wifiLock.Acquire();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameHostService] Locks error: {ex.Message}");
            }
        }

        private void ReleaseLocks()
        {
            try
            {
                if (_wakeLock?.IsHeld == true)
                {
                    _wakeLock.Release();
                }
                _wakeLock = null;

                if (_wifiLock?.IsHeld == true)
                {
                    _wifiLock.Release();
                }
                _wifiLock = null;
            }
            catch { }
        }

        public override void OnDestroy()
        {
            ReleaseLocks();
            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                StopForeground(StopForegroundFlags.Remove);
            }
            else
            {
                StopForeground(true);
            }
            base.OnDestroy();
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(ChannelId, "Игровой сервер Монополии", NotificationImportance.Low)
                {
                    Description = "Уведомление о фоновой работе игрового сервера"
                };
                var manager = (NotificationManager?)GetSystemService(NotificationService);
                manager?.CreateNotificationChannel(channel);
            }
        }
    }
}
#endif
