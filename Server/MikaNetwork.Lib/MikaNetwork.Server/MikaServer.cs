using System.Net;
using MikaNetwork;

namespace MikaNetwork.Server;

public interface IServer
{
    public event Action<ISession>? Connected;
    public event Action<ISession>? Disconnected;

    public EndPoint EndPoint { get; }
    public void Listen();
    
}

public class MikaServer : IServer, IDisposable
{
    private readonly MikaAcceptor _acceptor;
    public SessionManager SessionManager { get; init; } = new();

    public event Func<ISession, ReadOnlyMemory<byte>, ValueTask>? PacketReceived;

    /// <summary>세션 접속 해제 시 게임 로직 레이어가 정리(User 제거 등)할 수 있도록 노출.</summary>
    /// <summary>세션이 수락돼 세션 목록에 등록된 직후. 수신 루프가 시작되기 전에 불린다.</summary>
    public event Action<ISession>? Connected;

    public event Action<ISession>? Disconnected;

    public EndPoint EndPoint => _acceptor.EndPoint;
    
    public MikaServer(int port)
        : this(new MikaServerOptions
        {
            AcceptorOpt = new MikaAcceptorOptions { LocalAddr = IPAddress.Any, Port = port }
        })
    {
        
    }
    public MikaServer(IPAddress address, int port)
        : this(new MikaServerOptions
        {
            AcceptorOpt = new MikaAcceptorOptions { LocalAddr = address, Port = port }
        })
    {
        
    }
    public MikaServer(MikaServerOptions options)
    {
        _acceptor = new MikaAcceptor(options.AcceptorOpt!);
        _acceptor.Accepted += OnAccepted;
        
    }

    public void Listen()
    {
        _acceptor.Listen();
    }


    public void Send(int sessionId, byte[] data)
    {
        if (SessionManager.TryGet(sessionId, out var session))
        {
            session?.Send(data);
        }
    }
    public void Send(MikaServerSession serverSession, byte[] data)
    {
        serverSession.Send(data);    
    }

    /// <summary>
    /// 일정 시간 아무것도 받지 못한 세션을 끊는다.
    ///
    /// <para>
    /// TCP는 끊김을 알려 주지 않는다. FIN/RST가 나가지 않는 종류의 단절
    /// (Unity 에디터 플레이 중지·절전·랜선 뽑기)에서는 <c>ReceiveAsync</c>가 영원히 대기하고
    /// <c>IsConnected</c>가 참으로 남는다. 그동안 호스트는 그 세션을 접속 중으로 본다.
    /// </para>
    ///
    /// <para>
    /// <b>주기와 임계값은 이 라이브러리가 정하지 않는다.</b> 얼마나 빨리 끊어야 하는지는
    /// 게임 규칙(이 프로젝트에서는 "끊김 구간이 곧 부당 적립 구간")이 정하므로 호스트가 넘긴다.
    /// 로그 정책을 훅으로만 뚫어 두는 것과 같은 이유다.
    /// </para>
    /// </summary>
    /// <returns>끊은 세션 수.</returns>
    public int SweepIdle(DateTime now, TimeSpan timeout)
        => DisconnectIdle(SessionManager.Sessions.Values, now, timeout);

    /// <summary>
    /// <see cref="SweepIdle"/>의 본체. 세션 목록·시각·임계값을 전부 인자로 받아
    /// <b>실제 연결 없이 검증된다</b>(<see cref="IHeartbeatSession"/>은 소켓을 요구하지 않는다).
    /// </summary>
    public static int DisconnectIdle(
        IEnumerable<IHeartbeatSession> sessions, DateTime now, TimeSpan timeout)
    {
        var closed = 0;

        // 순회 중 Disconnect가 세션 목록을 건드리므로 먼저 확정한다.
        foreach (var session in sessions.ToList())
        {
            if (!session.IsConnected)
                continue;

            if (now - session.LastReceivedAt < timeout)
                continue;

            session.Disconnect();
            closed++;
        }

        return closed;
    }

    public void Stop()
    {
        Dispose();
    }
    
    public void Dispose()
    {
        _acceptor.Dispose();
    }

    private void OnAccepted(MikaServerSession serverSession)
    {
        serverSession.Disconnected += OnDisconnected;
        serverSession.Received += OnSessionPacketReceived;
        
        SessionManager.TryAdd(serverSession.SessionId, serverSession);

        // 로그는 찍지 않는다 — 어디에 어떻게 남길지는 호스트가 정한다(WSGameServer는 ServerLog로 받는다).
        Connected?.Invoke(serverSession);

        _ = serverSession.StartAsync();
    }
    private void OnDisconnected(ISession session)
    {
        session.Received -= OnSessionPacketReceived;
        session.Disconnected -= OnDisconnected;

        SessionManager.TryRemove(session.SessionId, out _);

        // 게임 로직 레이어가 User 등 세션 연관 상태를 정리하도록 알림
        Disconnected?.Invoke(session);
    }

    private async ValueTask OnSessionPacketReceived(ISession session, ReadOnlyMemory<byte> data)
    { 
        var handler = PacketReceived;
        if (handler != null)
        {
            await handler(session, data);
        }
    }
    
    
}