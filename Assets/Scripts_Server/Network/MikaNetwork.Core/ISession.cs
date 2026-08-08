using System;
using System.Net;
using System.Threading.Tasks;

namespace MikaNetwork
{
    public interface ISession : IDisposable
    {
        long SessionId { get; }
        EndPoint? RemoteEndPoint { get; }
        bool IsConnected { get; }

        event Func<ISession, ReadOnlyMemory<byte>, ValueTask>? Received;
        event Action<ISession>? Connected;
        event Action<ISession>? Disconnected;

        void Send(ReadOnlyMemory<byte> data); // byte[]보다 할당 안 강제
        void Disconnect();
    }
}