using MikaNetwork.Server;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 접속 중인 플레이어의 채취를 <b>주기적으로 정산해 밀어 주는</b> 스케줄러.
///
/// <para>
/// <b>초당 수십 번 도는 루프(30fps 등)를 두지 않는다.</b> 판정 간격이 수십 초인데 초당 30번 깨우면
/// 대부분의 틱이 헛돌고, 게임 로직 스레드가 단일이라 그 낭비가 그대로 패킷 처리를 밀어낸다.
/// 진행도는 시각의 함수이므로 <b>깨어난 시점에 경과분을 한 번에 계산</b>하면 된다.
/// 이 타이머가 늦게 돌아도 결과는 같다 — 정산량은 주기가 아니라 경과 시각이 정한다.
/// </para>
///
/// <para>
/// 이 타이머는 "언제 계산할지"만 정하고 "얼마나 나올지"에는 관여하지 않는다.
/// </para>
/// </summary>
public sealed class GatheringScheduler
{
    private Timer? _timer;

    /// <summary>
    /// <b>푸시 해상도.</b> 채취 주기가 아니다 — 캐릭터 스탯·버프로 슬롯마다 주기가 다르므로
    /// 맞출 대상이 되는 단일 주기가 존재하지 않는다.
    ///
    /// <para>
    /// 이 값은 <b>정확성이 아니라 체감에만</b> 영향을 준다. 정산량은 경과 시각이 정하므로
    /// 얼마로 잡든 나오는 개수는 같고, 다만 "잡혔다"가 최대 이만큼 늦게 도착한다.
    /// 가장 빠른 슬롯의 주기보다 짧게 두면 몰아서 도착하는 일이 없다.
    /// </para>
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly ILogicExecutor _logicExecutor;
    public GatheringScheduler(ILogicExecutor logicExecutor)
    {
        _logicExecutor = logicExecutor;
    }
    
    public void Start()
    {
        if (_timer is not null)
            return;

        // 타이머 스레드에서 게임 상태를 직접 만지면 안 된다. 로직 스레드로 넘긴다.
        // 시각과 대상 목록은 여기서 만들어 넣는다 — Tick 자신은 전역을 보지 않는다.
        _timer = new Timer(_ => _logicExecutor.Post(() => Tick(DateTime.UtcNow, UserManager.Instance.All)),
            null, Interval, Interval);
        ServerLog.Info("채취", $"스케줄러 시작 — 푸시 해상도 {Interval.TotalSeconds}초");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// 로직 스레드에서 실행된다. <b>시각과 대상을 인자로 받는다</b> —
    /// 재화가 실제로 생기는 권위 루프라 밖에서 부를 수 있어야 검증이 된다.
    /// </summary>
    public static void Tick(DateTime now, IEnumerable<User> users)
    {
        foreach (var user in users)
        {
            // 한 유저의 예외가 나머지 유저의 정산을 막지 않게 한다.
            try
            {
                user.SettleWorkStation(now);
            }
            catch (Exception e)
            {
                ServerLog.Error("채취", $"주기 정산 실패 Uid={user.Uid}", e);
            }
        }
    }
}
