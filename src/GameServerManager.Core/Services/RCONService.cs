using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace GameServerManager.Core.Services
{
    /// <summary>
    /// RCON service for connecting to game servers and sending commands.
    /// Supports TCP-based RCON protocol commonly used by Source engine games and others.
    /// </summary>
    public class RCONService : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _password;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool IsConnected => _client != null && _client.Connected;
        private int _requestId = 0;
        private readonly List<string> _commandHistory = new();

        public event Action<string>? OnCommandOutput;
        public event Action? OnConnected;
        public event Action? OnDisconnected;
        public event Action<string>? OnError;

        public bool Connected => IsConnected;
        public int CommandCount => _commandHistory.Count;

        public RCONService(string host, int port, string password)
        {
            _host = host;
            _port = port;
            _password = password;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _client = new TcpClient(_host, _port);
                _stream = _client.GetStream();

                // Authenticate with password if required
                if (!string.IsNullOrEmpty(_password))
                {
                    await SendCommandAsync($"pass {_password}");
                }

                OnConnected?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
                return false;
            }
        }

        public async Task<string> SendCommandAsync(string command)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to server.");
            }

            _requestId++;
            var requestBytes = BuildRCONPacket(_requestId, command);
            await _stream!.WriteAsync(requestBytes, 0, requestBytes.Length);

            // Read response (simplified - actual implementation may need more robust parsing)
            var buffer = new byte[4096];
            var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
            var response = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            _commandHistory.Add(command);
            OnCommandOutput?.Invoke(response.Trim());

            return response;
        }

        private byte[] BuildRCONPacket(int requestId, string command)
        {
            var protocol = Encoding.ASCII.GetBytes("sT3");
            var idBytes = BitConverter.GetBytes(_requestId);
            var commandBytes = Encoding.ASCII.GetBytes(command + "\0");
            var typeBytes = Encoding.ASCII.GetBytes("cT\0\0");

            using var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(protocol);
            bw.Write(idBytes);
            bw.Write(typeBytes);
            bw.Write(commandBytes);
            return ms.ToArray();
        }

        public void Dispose()
        {
            _client?.Close();
            _client?.Dispose();
        }
    }
}