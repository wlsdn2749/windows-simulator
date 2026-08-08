using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using MikaProtocol;

namespace MikaNetwork
{
    public sealed class MikaClient
    {
        private readonly MikaConnector _connector = new();
        public MikaClientSession? Session { get; private set; }
        public event Func<ISession, ReadOnlyMemory<byte>, ValueTask>? PacketReceived;

        public async Task ConnectAsync(string ip, int port) => await ConnectAsync(IPAddress.Parse(ip), port);
        public async Task ConnectAsync(IPAddress ipAddress, int port)
        {
            Session = await _connector.ConnectAsync(ipAddress, port);

            Session.Received += OnSessionPacketReceived;
            Session.Connected += OnConnected;
            Session.Disconnected += OnDisconnected;

            _ = Session.StartAsync();
        }

        public void Disconnect()
        {
            Session?.Disconnect();
        }

        public void Send<T>(T packet) where T : IPacket
        {
            Session?.SendPacket(packet);
        }

        private async ValueTask OnSessionPacketReceived(ISession session, ReadOnlyMemory<byte> data)
        {
            var handler = PacketReceived;
            if (handler != null)
            {
                await handler(session, data);
            }
        }


        private void OnConnected(ISession session)
        {
            Debug.Log($"Connected to {session.RemoteEndPoint}");
        }

        private void OnDisconnected(ISession session)
        {
            Debug.Log($"$Disconnected from {session.RemoteEndPoint}");
        }

    }
}
