#if UNITY_5_3_OR_NEWER

namespace MikaNetwork
{
    public class ServerPacketManager : MikaPacketManager
    {
        public ServerPacketManager()
        {
            MikaGenerated.GeneratedHandlers.RegisterAll(this);
        }
    }
}

#endif
