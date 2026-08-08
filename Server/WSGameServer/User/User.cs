using MikaNetwork.Server;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// Session 접속 후 로그인하면 생성되는 게임 로직 단위 객체.
/// 참조 방향은 User -> Session 단방향이며, Session은 User를 알지 못한다.
/// 생성은 반드시 <see cref="UserManager.CreateUser"/>를 통해서만 이루어진다.
/// </summary>
public sealed partial class User
{
    /// <summary>클라이언트로 나가는 통로. 전송 계층(ISession)은 이 뒤에만 있다.</summary>
    private readonly IClientChannel _channel;

    /// <summary>DB 작업 큐. 운영에서는 <see cref="DBManager"/>, 테스트에서는 기록만 하는 가짜가 들어온다.</summary>
    private readonly IDBQueue _db;
    
    private readonly ILogicExecutor _logicExecutor;

    /// <summary>
    /// 채취 판정에 쓸 드롭 테이블. 생략하면 전역 인스턴스를 쓴다 —
    /// <see cref="WorkStation.Settle"/>이 이미 같은 규약이라 맞춘다.
    /// </summary>
    private readonly DropTableCatalog _dropTables;

    public long SessionId { get; }
    public string Pid { get; }

    public string NickName { get; set; }
    public DateTime LoggedInAt { get; }

    public long Uid { get; set; }
    public int AdminLevel { get; set; }
    public bool IsNewbie { get; set; }
    
    private bool _created;

    // Destroy는 여러 곳에서 들어온다(소켓 해제·유휴 스윕·중복 로그인 정리).
    // 가드가 없으면 OnDestroy가 여러 번 큐에 실려 종료 정산이 중복된다.
    private int _destroyed;
    
    public ulong Key { get; } = AllocKey64();
    public ulong GetJobId() { return Key; }
    
    public bool Create()
    {
        if (_created) return false;
        
        _created = true;
        
        Post(OnCreate);

        return true;
    }
    
    public bool Destroy()
    {
        if (Interlocked.Exchange(ref _destroyed, 1) == 1)
            return false;

        Post(OnDestroy);
        return true;
    }
    
    /// <summary>정리가 이미 예약됐는지. 좀비 판정처럼 "이 객체를 아직 살아 있다고 볼지"에 쓴다.</summary>
    public bool IsDestroyed => Volatile.Read(ref _destroyed) == 1;
    
    public void Post(ulong id, Action job)
    {
        _logicExecutor.Post(job);
    }

    public void Post(Action job)
    {
        Post(Key, job);
    }

    // Inventory
    private Inventory Inventory { get; init; } = new();

    /// <summary>
    /// <b>시각을 인자로 받는다</b> — 이 저장소는 순수 코어(<see cref="WorkStationSlot"/>·
    /// <see cref="WorkStation"/>)가 전부 그렇게 되어 있고, 시계를 필드로 들면 같은 문제에
    /// 두 번째 관례가 생긴다. 시각을 만드는 곳은 진입점(핸들러·타이머·Repository)이다.
    /// </summary>
    internal User(
        IClientChannel    channel,
        IDBQueue          db,
        ILogicExecutor    logicExecutor,
        string            pid,
        string            nickname,
        DateTime          loggedInAt,
        DropTableCatalog? dropTables = null)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(db);

        _channel    = channel;
        _db         = db;
        _logicExecutor = logicExecutor;
        _dropTables = dropTables ?? DropTableCatalog.Instance;

        SessionId  = channel.SessionId;
        Pid        = pid;
        NickName   = nickname;
        LoggedInAt = loggedInAt;
    }

    // 주의: 소멸자(finalizer)는 GC가 객체를 수거할 때 비결정적으로 호출된다.
    // Destroy() 호출과 무관하며, 결정적 정리·로그는 OnDestroy에서 처리한다.
    // (여기는 GC 수거 여부 진단용으로만 남겨둠)
    ~User()
    {
        // GC 수거 여부 진단용이라 평소에는 보이지 않는 Trace로 둔다.
        ServerLog.Trace("유저", $"GC 수거 SessionId={SessionId} Pid={Pid}");
    }

    public void Login()
    {
        // 연결되지 않았으면 정리
        if (!_channel.IsConnected)
        {
            Destroy();
            return;
        }
        
        UserManager.Instance.JoinUser(this);
        
        Send(new S_LoginResponse { Result = EResultCode.Ok, SessionId = SessionId });
        
        SendInventory();   // S_InventoryResponse
        SendCurrencies();  // S_CurrencyResponse

        // 캐릭터를 슬롯보다 먼저 보낸다 — 슬롯이 CharacterId를 참조하므로,
        // 클라이언트가 슬롯을 그릴 때 캐릭터를 이미 알고 있어야 한다.
        SendCharacters();  // S_CharacterListResponse

        // 오프라인 진행이 없으므로 로그인 시점에 정산할 구간이 없다.
        // 슬롯은 로그인 흐름에서 이미 "지금부터" 시작하도록 만들어져 있다.
        SendWorkStationSlots(); // S_WorkStationSlotsResponse
    }

    public void OnCreate()
    {
        ServerLog.Info("유저", $"생성 SessionId={SessionId} Pid={Pid}");
        
        PostDBTask(new AccountRepository(this));
    }

    /// <summary>
    /// 접속 종료 정리. <b>시각을 받는 이쪽이 본체고 <see cref="OnDestroy"/>는 배선만 한다</b> —
    /// base 시그니처가 고정이라 인자를 뚫을 수 없어 한 겹을 가른 것이다.
    ///
    /// <para>
    /// 마지막 정산을 한다. 접속 중 완성된 판정은 정당하게 번 것이므로 끊겼다고 버리지 않는다.
    /// 세션이 이미 닫혔으므로 푸시는 하지 않고(notify: false) 지급·저장만 한다.
    /// 판정에 못 미친 조각은 여기서 함께 사라진다 — 진행도는 세션과 수명을 같이한다.
    /// </para>
    /// </summary>
    public void Disconnect(DateTime now)
    {
        try
        {
            SettleWorkStation(now, notify: false);
        }
        catch (Exception e)
        {
            // 정산이 실패해도 세션 정리는 반드시 진행해야 한다(User가 남으면 누수다).
            ServerLog.Error("채취", $"종료 정산 실패 SessionId={SessionId}", e);
        }

        UserManager.Instance.LeaveUser(this);
    }

    public void OnDestroy()
    {
        // 끊김 시 결정적으로 호출됨(로직 스레드). 소멸자가 아니라 여기가 정리 지점이다.
        ServerLog.Info("유저", $"소멸 SessionId={SessionId} Pid={Pid}");

        Disconnect(DateTime.UtcNow);
    }

    public void Initialize(long userId, string nickName, int adminLevel, bool isNewbie)
    {
        Uid = userId;
        NickName = nickName;
        AdminLevel = adminLevel;
        IsNewbie = isNewbie;

        PostDBTask<LoginRepository>(new (this));
    }

    public void Send<T>(T packet) where T : IPacket => _channel.Send(packet);

    /// <summary>
    /// 클라이언트와의 통로를 닫는다. <see cref="Destroy"/>는 게임 상태만 정리하므로
    /// 소켓까지 끊으려면 이쪽을 함께 부른다(중복 로그인으로 밀려나는 세션).
    /// </summary>
    public void CloseChannel() => _channel.Disconnect();

    public void PostDBTask<TRepository>(TRepository repository) where TRepository : IRepository
    {
        _db.Post(repository);
    }
}
