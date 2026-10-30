using System.Net.Sockets;
using System.Text.Json;

namespace MonopolyBank.Networking
{
    public class GameClient
    {
        private TcpClient? _tcpClient;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private bool _isConnected;

        public event Action<NetworkPacket>? OnPacketReceived;
        public event Action? OnDisconnected;

        public bool IsConnected => _isConnected && _tcpClient != null && _tcpClient.Connected;

        public async Task<bool> ConnectAsync(string ipAddress, int port = 8888)
        {
            try
            {
                Disconnect();
                _tcpClient = new TcpClient();
                
                var connectTask = _tcpClient.ConnectAsync(ipAddress, port);
                var timeoutTask = Task.Delay(5000);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    Disconnect();
                    return false;
                }
                await connectTask;
                try { _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true); } catch { }

                var stream = _tcpClient.GetStream();
                _reader = new StreamReader(stream);
                _writer = new StreamWriter(stream) { AutoFlush = true };
                _isConnected = true;

                _ = Task.Run(ReceiveLoopAsync);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameClient] Connection error: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        public async Task SendPacketAsync(NetworkPacket packet)
        {
            if (!_isConnected || _writer == null) return;

            var json = JsonSerializer.Serialize(packet);
            await _writeLock.WaitAsync();
            try
            {
                await _writer.WriteLineAsync(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameClient] SendPacket error: {ex.Message}");
                Disconnect();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (_isConnected && _reader != null)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null) break;

                    try
                    {
                        var packet = JsonSerializer.Deserialize<NetworkPacket>(line);
                        if (packet != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                OnPacketReceived?.Invoke(packet);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GameClient] Deserialize error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameClient] ReceiveLoop error: {ex.Message}");
            }
            finally
            {
                Disconnect();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnDisconnected?.Invoke();
                });
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            try
            {
                _writer?.Close();
                _reader?.Close();
                _tcpClient?.Close();
            }
            catch { }
            _tcpClient = null;
            _reader = null;
            _writer = null;
        }
    }
}
