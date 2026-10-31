using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace MonopolyBank.Networking
{
    public class DiscoveredServer
    {
        public string Name { get; set; } = "Монополия Сервер";
        public string IpAddress { get; set; } = "";
        public int Port { get; set; } = 8888;
    }

    public class UdpBeaconHost
    {
        private const int DiscoveryPort = 8889;
        private UdpClient? _udpListener;
        private CancellationTokenSource? _cts;
        private readonly string _serverName;
        private readonly int _tcpPort;

        public UdpBeaconHost(string serverName, int tcpPort = 8888)
        {
            _serverName = serverName;
            _tcpPort = tcpPort;
        }

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
            try
            {
                _udpListener?.Close();
                _udpListener?.Dispose();
            }
            catch { }
            _udpListener = null;
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            try
            {
                _udpListener = new UdpClient();
                _udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

                while (!token.IsCancellationRequested)
                {
                    var result = await _udpListener.ReceiveAsync(token);
                    var message = Encoding.UTF8.GetString(result.Buffer);

                    if (message.StartsWith("DISCOVER_MONOPOLY"))
                    {
                        var response = $"MONOPOLY_SERVER|{_serverName}|{_tcpPort}";
                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        await _udpListener.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UdpBeaconHost] Error: {ex.Message}");
            }
        }
    }

    public class UdpDiscoveryClient
    {
        private const int DiscoveryPort = 8889;

        public static async Task<List<DiscoveredServer>> DiscoverServersAsync(int timeoutMs = 2500)
        {
            var servers = new List<DiscoveredServer>();
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            udp.Client.ReceiveTimeout = timeoutMs;

            try
            {
                var queryBytes = Encoding.UTF8.GetBytes("DISCOVER_MONOPOLY");

                // Broadcast to global broadcast
                var broadcastEp = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                await udp.SendAsync(queryBytes, queryBytes.Length, broadcastEp);

                // Also broadcast on subnet-specific broadcasts if available
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        foreach (var u in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (u.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                var ipBytes = u.Address.GetAddressBytes();
                                
                                // На iOS u.IPv4Mask часто null! В таком случае берем стандартную маску /24 (255.255.255.0)
                                byte[] bcastBytes = new byte[4];
                                if (u.IPv4Mask != null)
                                {
                                    var maskBytes = u.IPv4Mask.GetAddressBytes();
                                    for (int i = 0; i < 4; i++)
                                    {
                                        bcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                                    }
                                }
                                else
                                {
                                    // Стандартный fallback для /24: например 192.168.88.255
                                    bcastBytes[0] = ipBytes[0];
                                    bcastBytes[1] = ipBytes[1];
                                    bcastBytes[2] = ipBytes[2];
                                    bcastBytes[3] = 255;
                                }

                                var subnetBcast = new IPAddress(bcastBytes);
                                try
                                {
                                    await udp.SendAsync(queryBytes, queryBytes.Length, new IPEndPoint(subnetBcast, DiscoveryPort));
                                }
                                catch { }
                            }
                        }
                    }
                }

                var cts = new CancellationTokenSource(timeoutMs);
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var receiveTask = udp.ReceiveAsync(cts.Token).AsTask();
                        var result = await receiveTask;
                        var text = Encoding.UTF8.GetString(result.Buffer);

                        if (text.StartsWith("MONOPOLY_SERVER|"))
                        {
                            var parts = text.Split('|');
                            var name = parts.Length > 1 ? parts[1] : "Сервер";
                            var port = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 8888;
                            var ip = result.RemoteEndPoint.Address.ToString();

                            if (!servers.Any(s => s.IpAddress == ip))
                            {
                                servers.Add(new DiscoveredServer
                                {
                                    Name = name,
                                    IpAddress = ip,
                                    Port = port
                                });
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UdpDiscoveryClient] Error: {ex.Message}");
            }

            return servers;
        }
    }
}
