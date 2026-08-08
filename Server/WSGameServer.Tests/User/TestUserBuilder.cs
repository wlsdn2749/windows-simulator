using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 나간 패킷을 <b>객체 그대로</b> 담아 두는 가짜 통로.
///
/// <para>
/// 운영 구현(<c>SessionClientChannel</c>)이 하는 직렬화·프레이밍을 건너뛴다.
/// 검증 대상이 "무엇을 보냈는가"이지 "어떻게 인코딩됐는가"가 아니기 때문이다 —
/// 와이어 포맷을 통과시키면 테스트가 빨개졌을 때 원인이 로직인지 직렬화인지 갈린다.
/// </para>
/// </summary>
internal sealed class FakeClientChannel : IClientChannel
{
    public List<IPacket> Sent { get; } = new();

    public long SessionId   { get; init; } = 1;
    public bool IsConnected { get; set; }  = true;

    /// <summary>통로가 닫힌 횟수. 중복 로그인으로 밀려나는 세션을 검증할 때 본다.</summary>
    public int DisconnectCount { get; private set; }

    public void Send<T>(T packet) where T : IPacket => Sent.Add(packet);

    public void Disconnect()
    {
        DisconnectCount++;
        IsConnected = false;
    }

    /// <summary>보낸 패킷 중 해당 타입만 순서대로 꺼낸다.</summary>
    public List<T> SentOf<T>() where T : IPacket => Sent.OfType<T>().ToList();
}

/// <summary>
/// 요청받은 Repository를 실행하지 않고 기록만 하는 가짜 DB 큐.
/// 운영 구현은 DB 스레드와 로직 스레드를 왕복하는 비동기라 테스트에서 완료를 기다려야 한다 —
/// 여기서 보고 싶은 것은 "저장이 요청됐는가"뿐이므로 동기적으로 담기만 한다.
/// </summary>
internal sealed class FakeDBQueue : IDBQueue
{
    public List<IRepository> Posted { get; } = new();

    public void Post<TRepository>(TRepository repository) where TRepository : IRepository
        => Posted.Add(repository);

    public List<T> PostedOf<T>() where T : IRepository => Posted.OfType<T>().ToList();
}

/// <summary>
/// <c>GameTable</c>을 테스트 프로세스에서 한 번만 적재한다.
/// <c>Character.GetBaseWorkSpeed</c>가 <c>WorkSpeedTable</c>을 읽으므로 배치 성공 경로에 필요하다.
/// 적재 후에는 읽기만 하므로 여러 테스트가 동시에 봐도 안전하다.
/// </summary>
internal static class GameTableFixture
{
    private static readonly object Gate = new();
    private static bool _loaded;

    public static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_loaded)
                return;

            GameTable.LoadAll(name =>
                File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name)));

            _loaded = true;
        }
    }
}

/// <summary>
/// 테스트용 <see cref="User"/> 조립기.
///
/// <para>
/// <b>기본은 <c>Create()</c>를 거치지 않는다.</b> 기록 모드 실행기가 들어가므로
/// <c>Create()</c>/<c>Destroy()</c>는 <b>예약만</b> 되고 <c>OnCreate</c>/<c>OnDestroy</c>는 돌지 않는다.
/// 예약 자체를 보려면 <see cref="Executor"/>의 <c>Posted</c>를,
/// 흐름 전체를 보려면 <see cref="WithInlineExecutor"/>를 쓴다.
/// </para>
/// </summary>
internal sealed class TestUserBuilder
{
    /// <summary>모든 테스트가 공유하는 기준 시각. 실제 시계를 쓰지 않는다.</summary>
    public static readonly DateTime Base = new(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

    public FakeClientChannel  Channel  { get; } = new();
    public FakeDBQueue        DB       { get; } = new();
    public DropTableCatalog   Drops    { get; } = new();
    public FakeLogicExecutor  Executor { get; private set; } = new();

    /// <summary>
    /// 예약된 작업을 그 자리에서 실행하게 만든다 — <c>Create()</c> 이후의 흐름을 볼 때.
    /// <b><c>Destroy()</c> 검증에는 쓰지 않는다</b>: <c>OnDestroy</c>가
    /// <c>UserManager.Instance</c>(프로세스 전역)를 만져 다른 테스트로 샌다.
    /// </summary>
    public TestUserBuilder WithInlineExecutor()
    {
        Executor = new FakeLogicExecutor { RunImmediately = true };
        return this;
    }

    /// <summary>
    /// 낚시 드롭 테이블 하나만 등록한다. 후보가 1종이라 <b>어떤 아이템이 나올지 확정</b>되고,
    /// 판정 횟수와 획득 개수를 그대로 대조할 수 있다.
    /// </summary>
    public const int FishItemTid = 1001;

    public TestUserBuilder WithFishingDrops()
    {
        Drops.Register(ItemType.Fishing, DropTable.From(
            "FishingBasicTable",
            new[] { (ItemTID: FishItemTid, Weight: 100) },
            r => r.ItemTID, r => r.Weight));

        return this;
    }

    public User Build(long uid = 1)
    {
        var user = new User(Channel, DB, Executor,
                            pid: "test-pid", nickname: "테스터", loggedInAt: Base, Drops);
        user.Uid = uid;
        return user;
    }
}
