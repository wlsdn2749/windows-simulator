using System.Net;

namespace MikaNetwork
{
    public sealed class MikaServerOptions
    {
        public MikaAcceptorOptions? AcceptorOpt { get; set; }
        public MikaSessionOptions SessionOpt { get; } = new();
    }

    public sealed class MikaAcceptorOptions
    {
        public int Port { get; set; }
        public IPAddress LocalAddr { get; set; } = IPAddress.Loopback;
        public int Backlog { get; } = 65535;
    }

    public sealed class MikaSessionOptions
    {
        public int SendQueueCapacity { get; } = 1024;
        public int ReceiveBufferSize { get; } = 8192;
    }
}
