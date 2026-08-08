using MikaNetwork;

namespace WSGameServer;

public class ClientPacketManager : MikaPacketManager
{
    public ClientPacketManager()
    {
        MikaGenerated.GeneratedHandlers.RegisterAll(this);
    }
}
