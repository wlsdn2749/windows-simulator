using System.Collections.Concurrent;
using MikaNetwork;
using MikaNetwork.Server;
using MikaProtocol;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 로그인한 User들을 SessionId 단위로 보관/조회/정리하는 게임 로직 레이어 매니저.
/// 프레임워크의 <c>SessionManager</c>(transport)와 분리된 게임 로직 전용 저장소다.
/// </summary>
public sealed class UserManager : Singleton<UserManager>
{
    private readonly ConcurrentDictionary<ulong, User>   _userKeys    = new(); // uid -> User
    private readonly ConcurrentDictionary<string, ulong> _pids        = new(); // pid -> uid
    private readonly ConcurrentDictionary<long, ulong>   _sessionKeys = new(); // sessId -> uid  
    //private readonly ConcurrentDictionary<long, long>   _uids        = new(); // 

    private DBManager? _dbManager;
    private ILogicExecutor? _logicExecutor;
    public void Initialize(DBManager dbManager, ILogicExecutor logicExecutor)
    {
        _dbManager = dbManager;
        _logicExecutor = logicExecutor;
    }
    
    public bool TryGetUserBySessionId(long sessionId, out User? user)
    {
        if (!_sessionKeys.TryGetValue(sessionId, out var uid))
        {
            user = null;
            return false;
        }

        return TryGetUser(uid, out user);
    }

    
    public bool TryGetUserByPid(string pid, out User? user)
    {
        if (!_pids.TryGetValue(pid, out var uid))
        {
            user = null;
            return false;
        }

        return TryGetUser(uid, out user);            
    }
    
    public bool TryGetUser(ulong uid, out User? user) => _userKeys.TryGetValue(uid, out user);

    public int Count => _userKeys.Count;

    /// <summary>접속 중인 모든 유저. 주기 정산처럼 전체를 훑어야 할 때 쓴다.</summary>
    public IEnumerable<User> All => _userKeys.Values;

    
    public void CreateUser(ISession session, string pid, string nickname) // pid is token or ...
    {
        // 같은 pid가 이미 접속 중이면 기존 쪽을 끊고 새 로그인을 받는다.
        //
        // 조용히 return하면 안 된다. 하트비트가 못 잡은 좀비가 남아 있을 때
        // 정상적인 재접속이 응답조차 없이 막혀 클라가 원인을 알 수 없다(이슈 #10 증상 3).
        // 살아 있는 쪽을 남기고 새 접속을 거절하는 선택도 가능하지만,
        // "끊긴 걸 서버가 아직 모르는" 경우가 압도적이라 새 접속을 신뢰한다.
        if (TryGetUserByPid(pid, out var previous) && previous is not null)
        {
            ServerLog.Warn("로그인",
                $"pid 중복 — 기존 세션을 정리하고 새 로그인을 받는다. " +
                $"Pid={pid} 기존sid={previous.SessionId} 새sid={session.SessionId}");

            // pid 자리를 여기서 즉시 비운다. Destroy는 OnDestroy를 로직 스레드 큐에 넣을 뿐이라
            // LeaveUser가 도는 시점이 DB 왕복 뒤인 새 유저의 JoinUser보다 늦을 수 있다.
            // 그러면 새 유저의 _pids.TryAdd가 조용히 실패하고, 뒤늦게 도는 기존 LeaveUser가
            // 살아 있는 세션의 매핑을 지워 버린다.
            _pids.TryRemove(new KeyValuePair<string, ulong>(pid, previous.Key));

            // Destroy는 멱등이다. 종료 정산은 로직 스레드에서 이어서 돈다.
            previous.Destroy();

            // 소켓도 닫는다 — Destroy는 게임 상태만 정리한다.
            previous.CloseChannel();
        }

        // 같은 세션에서 로그인을 두 번 보낸 경우. 이건 정리 대상이 아니라 잘못된 요청이다.
        if (TryGetUserBySessionId(session.SessionId, out _))
        {
            ServerLog.Warn("로그인", $"이미 로그인된 세션의 중복 요청 sid={session.SessionId} Pid={pid}");
            session.SendPacket(new S_LoginResponse { Result = EResultCode.AlreadyLoggedIn });
            return;
        }

        // 여기가 조립 지점이다 — 전송 통로·DB 큐·시각을 운영 구현으로 묶어 User에 넘긴다.
        // User 자신은 ISession도 DBManager도 모른다.
        var user = new User(
            new SessionClientChannel(session),
            _dbManager!,
            _logicExecutor!,
            pid,
            nickname,
            DateTime.UtcNow);

        if (!user.Create())
        {
            // Log
            user.Destroy();
        }
    }

    public bool JoinUser(User? user)
    {
        if (null == user)
        {
            // Log
            return false;
        }

        _userKeys.TryAdd(user.Key, user);
        _pids.TryAdd(user.Pid, user.Key);
        _sessionKeys.TryAdd(user.SessionId, user.Key);

        return true;
    }

    public bool LeaveUser(User? user)
    {
        if (null == user)
        {
            // LOg
            return false;
        }

        // ⚠️ 키만 보고 지우지 않는다. 같은 pid로 새 유저가 이미 들어와 있을 수 있고
        //    (중복 로그인), 그때 키로 지우면 살아 있는 세션의 매핑을 끊게 된다.
        //    값까지 일치할 때만 지우는 오버로드를 쓴다.
        _sessionKeys.TryRemove(new KeyValuePair<long, ulong>(user.SessionId, user.Key));
        _pids.TryRemove(new KeyValuePair<string, ulong>(user.Pid, user.Key));
        _userKeys.TryRemove(new KeyValuePair<ulong, User>(user.Key, user));

        return true;
    }
    
}
