using System.Collections.Concurrent;
using Plugin.Maui.Audio;

namespace MonopolyBank.Services
{
    public class SoundService
    {
        private static SoundService? _instance;
        public static SoundService Instance => _instance ??= new SoundService();

        private readonly ConcurrentDictionary<IAudioPlayer, Stream> _activePlayers = new();

        public async Task PlaySoundAsync(string soundName)
        {
            try
            {
                var filename = soundName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                    ? soundName
                    : $"{soundName}.mp3";

                var audioStream = await FileSystem.OpenAppPackageFileAsync(filename);
                var player = AudioManager.Current.CreatePlayer(audioStream);
                player.Volume = 1.0;

                _activePlayers[player] = audioStream;

                player.PlaybackEnded += (s, e) =>
                {
                    CleanupPlayer(player);
                };

                player.Play();

                // Safety cleanup timeout after 15 seconds in case PlaybackEnded doesn't fire
                _ = Task.Delay(15000).ContinueWith(_ => CleanupPlayer(player));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SoundService] Failed to play {soundName}: {ex.Message}");
            }
        }

        private void CleanupPlayer(IAudioPlayer player)
        {
            if (_activePlayers.TryRemove(player, out var stream))
            {
                try
                {
                    player.Dispose();
                }
                catch { }

                try
                {
                    stream?.Dispose();
                }
                catch { }
            }
        }
    }
}
