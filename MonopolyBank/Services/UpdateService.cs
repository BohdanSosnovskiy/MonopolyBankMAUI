using System.Text.Json;
using MonopolyBank.Views;

namespace MonopolyBank.Services
{
    public class RemoteVersionInfo
    {
        public int VersionCode { get; set; }
        public string VersionName { get; set; } = "";
        public string ApkUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
    }

    public class UpdateService
    {
        private const int CurrentVersionCode = 7;
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };

        public static async Task CheckForUpdatesAsync(INavigation? navigation, string? candidateHostIp = null)
        {
            try
            {
                var ipsToTry = new List<string>();
                if (!string.IsNullOrEmpty(candidateHostIp)) ipsToTry.Add(candidateHostIp);

                var savedIp = Preferences.Get("LastHostIp", "");
                if (!string.IsNullOrEmpty(savedIp) && !ipsToTry.Contains(savedIp)) ipsToTry.Add(savedIp);

                // Dev PC default IP
                const string devHostIp = "192.168.88.14";
                if (!ipsToTry.Contains(devHostIp)) ipsToTry.Add(devHostIp);

                // Try quick UDP discovery for OTA server on port 8890
                try
                {
                    using var udp = new System.Net.Sockets.UdpClient();
                    udp.EnableBroadcast = true;
                    udp.Client.ReceiveTimeout = 1200;
                    var req = System.Text.Encoding.UTF8.GetBytes("DISCOVER_MONOPOLY_OTA");
                    await udp.SendAsync(req, req.Length, new System.Net.IPEndPoint(System.Net.IPAddress.Broadcast, 8890));

                    var start = DateTime.UtcNow;
                    while ((DateTime.UtcNow - start).TotalMilliseconds < 1200)
                    {
                        if (udp.Available > 0)
                        {
                            var res = await udp.ReceiveAsync();
                            var msg = System.Text.Encoding.UTF8.GetString(res.Buffer);
                            if (msg.StartsWith("MONOPOLY_OTA|"))
                            {
                                var parts = msg.Split('|');
                                if (parts.Length > 1 && !ipsToTry.Contains(parts[1]))
                                {
                                    ipsToTry.Insert(0, parts[1]);
                                    break;
                                }
                            }
                        }
                        await Task.Delay(50);
                    }
                }
                catch { }

                // Also try gateway/local host IP
                var localIp = GameManager.GetLocalIpAddress();
                if (localIp.Contains('.'))
                {
                    var subnet = localIp.Substring(0, localIp.LastIndexOf('.'));
                    var candidate1 = $"{subnet}.1";
                    if (!ipsToTry.Contains(candidate1)) ipsToTry.Add(candidate1);
                    var candidate2 = $"{subnet}.14";
                    if (!ipsToTry.Contains(candidate2)) ipsToTry.Add(candidate2);
                }

                foreach (var ip in ipsToTry)
                {
                    try
                    {
                        var url = $"http://{ip}:3000/version.json";
                        var response = await _httpClient.GetStringAsync(url);
                        var remote = JsonSerializer.Deserialize<RemoteVersionInfo>(response, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (remote != null && remote.VersionCode > CurrentVersionCode)
                        {
                            var fullApkUrl = remote.ApkUrl.StartsWith("http") 
                                ? remote.ApkUrl 
                                : $"http://{ip}:3000/MonopolyBank.apk";

                            var confirm = await PopUpAlert.ShowConfirmAsync(
                                navigation,
                                "Обновление игры",
                                $"Доступна новая версия {remote.VersionName}!\n\n{remote.ReleaseNotes}\n\nСкачать и установить сейчас?",
                                "Скачать",
                                "Позже"
                            );

                            if (confirm)
                            {
                                await Launcher.OpenAsync(new Uri(fullApkUrl));
                            }
                            return;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateService] Error: {ex.Message}");
            }
        }
    }
}
