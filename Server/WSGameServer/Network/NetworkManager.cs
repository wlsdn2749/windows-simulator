using MikaUtils;
using MikaNetwork.Server;

namespace WSGameServer;

public class NetworkManager
{
    private readonly MikaServer _server = new(10050);
    private readonly ClientPacketManager _packetManager = new();
    private readonly ILogicExecutor _logicExecutor;
    private readonly SessionWatchdog _sessionWatchdog;
    public NetworkManager(ILogicExecutor logicExecutor, SessionWatchdog sessionWatchdog)
    {
        _logicExecutor = logicExecutor;
        _sessionWatchdog = sessionWatchdog;
    }

    public void Initialize()
    {
        // 패킷 훅 → 로그 연결. 리슨보다 먼저 붙여야 첫 접속의 패킷부터 남는다.
        PacketLogger.Attach();

        _server.PacketReceived += (session, data) =>
        {
            _packetManager.OnRecvPacket(session, data, _logicExecutor.Post);
            
            return ValueTask.CompletedTask;
        };

        _server.Connected += session =>
            ServerLog.Info("세션", $"접속 sid={session.SessionId} {session.RemoteEndPoint}");

        // 접속 해제 시 해당 세션의 User 정리
        // 네트워크 스레드에서 호출되지만, Destroy()가 OnDestroy를 LogicExecutor로 넘겨
        // 실제 정리(UserManager.LeaveUser)는 로직 스레드에서 실행된다.
        _server.Disconnected += session =>
        {
            ServerLog.Info("세션", $"해제 sid={session.SessionId}");
            session.GetUser()?.Destroy();
        };

        _server.Listen();

        // 리슨 이후에 건다. FIN/RST 없이 사라지는 클라이언트를 여기서 정리한다.
        _sessionWatchdog.Start(_server);
    }
}
