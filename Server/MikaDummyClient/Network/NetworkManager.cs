using System.Threading.Tasks;
using MikaNetwork;
using MikaProtocol;
using MikaUtils;

namespace MikaDummyClient
{
    public class NetworkManager : Singleton<NetworkManager>
    {
        private readonly MikaClient _client = new();
        private readonly ServerPacketManager _packetManager = new();

        public async Task Initialize()
        {
            _client.PacketReceived += (session, data) =>
            {
                _packetManager.OnRecvPacket(session, data);
                return default(ValueTask);
            };

            await _client.ConnectAsync("127.0.0.1", 10050);
        }

        public void Send<T>(T packet) where T : IPacket
        {
            _client.Send(packet);
        }
    }
}
