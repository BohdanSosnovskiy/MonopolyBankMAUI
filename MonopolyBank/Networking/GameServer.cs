using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace MonopolyBank.Networking
{
    public class GameServer
    {
        private TcpListener? _tcpListener;
        private readonly ConcurrentDictionary<string, ServerClientHandler> _clients = new();
        private bool _isRunning;
        private readonly int _port;

        public event Action<NetworkPacket, string>? OnPacketReceived;
        public event Action<string>? OnClientDisconnected;
        public event Action<string>? OnClientConnected;

        public GameServer(int port = 8888)
        {
            _port = port;
        }

        public async Task StartAsync()
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, _port);
                _tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _tcpListener.Start();
                _isRunning = true;

                while (_isRunning)
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    try { tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true); } catch { }
                    var clientId = Guid.NewGuid().ToString("N")[..8];
                    var handler = new ServerClientHandler(clientId, tcpClient, this);
                    _clients.TryAdd(clientId, handler);
                    _ = Task.Run(handler.RunAsync);
                    OnClientConnected?.Invoke(clientId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameServer] Exception: {ex.Message}");
            }
            finally
            {
                Stop();
            }
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                _tcpListener?.Stop();
            }
            catch { }

            foreach (var handler in _clients.Values)
            {
                handler.Close();
            }
            _clients.Clear();
        }

        public async Task BroadcastPacketAsync(NetworkPacket packet, string? excludeClientId = null)
        {
            var json = JsonSerializer.Serialize(packet);
            foreach (var (id, handler) in _clients)
            {
                if (id != excludeClientId)
                {
                    await handler.SendLineAsync(json);
                }
            }
        }

        public async Task SendPacketToClientAsync(string clientId, NetworkPacket packet)
        {
            if (_clients.TryGetValue(clientId, out var handler))
            {
                var json = JsonSerializer.Serialize(packet);
                await handler.SendLineAsync(json);
            }
        }

        internal void RemoveClient(string clientId)
        {
            if (_clients.TryRemove(clientId, out var handler))
            {
                handler.Close();
                OnClientDisconnected?.Invoke(clientId);
            }
        }

        internal void HandleIncomingPacket(NetworkPacket packet, string clientId)
        {
            OnPacketReceived?.Invoke(packet, clientId);
        }
    }

    internal class ServerClientHandler
    {
        public string ClientId { get; }
        private readonly TcpClient _tcpClient;
        private readonly GameServer _server;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public ServerClientHandler(string id, TcpClient client, GameServer server)
        {
            ClientId = id;
            _tcpClient = client;
            _server = server;
            var stream = _tcpClient.GetStream();
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream) { AutoFlush = true };
        }

        public async Task RunAsync()
        {
            try
            {
                while (true)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null) break;

                    try
                    {
                        var packet = JsonSerializer.Deserialize<NetworkPacket>(line);
                        if (packet != null)
                        {
                            _server.HandleIncomingPacket(packet, ClientId);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ServerClientHandler] Parse error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ServerClientHandler] Run error: {ex.Message}");
            }
            finally
            {
                _server.RemoveClient(ClientId);
            }
        }

        public async Task SendLineAsync(string line)
        {
            await _writeLock.WaitAsync();
            try
            {
                await _writer.WriteLineAsync(line);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ServerClientHandler] Send error: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Close()
        {
            try
            {
                _writer.Close();
                _reader.Close();
                _tcpClient.Close();
            }
            catch { }
        }
    }
}
