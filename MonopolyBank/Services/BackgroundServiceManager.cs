namespace MonopolyBank.Services
{
    public static class BackgroundServiceManager
    {
        public static void StartHostService()
        {
#if ANDROID
            try
            {
                var context = global::Android.App.Application.Context;
                var intent = new global::Android.Content.Intent(context, typeof(Platforms.Android.GameHostService));
                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                {
                    context.StartForegroundService(intent);
                }
                else
                {
                    context.StartService(intent);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundServiceManager] Start error: {ex.Message}");
            }
#endif
        }

        public static void StopHostService()
        {
#if ANDROID
            try
            {
                var context = global::Android.App.Application.Context;
                var intent = new global::Android.Content.Intent(context, typeof(Platforms.Android.GameHostService));
                context.StopService(intent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundServiceManager] Stop error: {ex.Message}");
            }
#endif
        }
    }
}
