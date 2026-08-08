using System.Collections.Concurrent;
using System.Net.Sockets;
using MikaNetwork.Server;

namespace MikaNetwork.Server;

public static class MikaSessionFactory
{
    private static long _sequenceKey;
    
    public static MikaServerSession Create(Socket socket)
    {
        return new MikaServerSession(socket, CreateSessionId());
    }
    
    private static long CreateSessionId()
    {
        long newKey = Interlocked.Increment(ref _sequenceKey);
        if (0 == newKey) // overflow시 한번 더 증가시켜서 반환
        {
            newKey = Interlocked.Increment(ref _sequenceKey);
        }
        return newKey;
    }
}

public sealed class SessionManager
{
    public ConcurrentDictionary<long, MikaServerSession> Sessions {get; init;} = new();
    
    public int Count => Sessions.Count;
    
    public bool TryAdd(long sessionId, MikaServerSession serverSession)
    {
        return Sessions.TryAdd(sessionId, serverSession);
    }

    public bool TryRemove(long sessionId, out MikaServerSession? removedSession)
    {
        return Sessions.TryRemove(sessionId, out removedSession);
    }

    public bool TryGet(long sessionId, out MikaServerSession? session)
    {
        return Sessions.TryGetValue(sessionId, out session);
    }
}