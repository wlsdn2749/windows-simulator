using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MikaNetwork
{
    public class MikaConnector : IDisposable
    {
        private readonly Socket _connectSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public bool IsConnected => _connectSocket.Connected;

        public void Connect(string ipAddress, int port) => _connectSocket.Connect(ipAddress, port);
        public void Connect(IPAddress ipAddress, int port) => _connectSocket.Connect(ipAddress, port);

        public async Task<MikaClientSession> ConnectAsync(string ipAddress, int port)
        {
            await _connectSocket.ConnectAsync(ipAddress, port);

            return new MikaClientSession(_connectSocket);
        }

        public async Task<MikaClientSession> ConnectAsync(IPAddress ipAddress, int port)
        {
            await _connectSocket.ConnectAsync(ipAddress, port);

            return new MikaClientSession(_connectSocket);
        }

        public void Send(string message)
        {
            // string -> byte
            byte[] bytes = Encoding.UTF8.GetBytes(message);

            _connectSocket.Send(bytes);
        }

        public string Receive()
        {
            byte[] recvBuffer = new byte[1024];

            int recvBytes = _connectSocket.Receive(recvBuffer);
            var recvMessage = Encoding.UTF8.GetString(recvBuffer, 0, recvBytes);

            return recvMessage;
        }

        public void Dispose()
        {
            _connectSocket.Dispose();
        }
    }
}
