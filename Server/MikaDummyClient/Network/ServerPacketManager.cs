using MikaNetwork;

namespace MikaDummyClient
{

    public class ServerPacketManager : MikaPacketManager
    {
        public ServerPacketManager()
        {
            MikaGenerated.GeneratedHandlers.RegisterAll(this);
        }
    }
}