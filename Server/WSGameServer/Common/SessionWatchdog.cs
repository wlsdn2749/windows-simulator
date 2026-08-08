using MikaNetwork;
using MikaNetwork.Server;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 오래 아무것도 받지 못한 세션을 끊는 감시자.
///
/// <para>
/// TCP는 끊김을 알려 주지 않는다. FIN/RST 없이 사라지는 종료(Unity 에디터 플레이 중지·
/// 절전·랜선 뽑기)에서는 <c>ReceiveAsync</c>가 영원히 대기하고 세션이 접속 중으로 남는다.
/// 그동안 <see cref="GatheringScheduler"/>가 매초 정산·지급을 계속하므로,
/// <b>좀비 유저는 폐지한 오프라인 적립을 되살린다</b>(이슈 #10).
/// </para>
///
/// <para>
/// <b>송신은 살아 있음의 증거가 되지 못한다.</b> <c>Send</c>는 소켓 쓰기가 아니라 큐 적재라
/// 소켓이 죽어도 1024개가 찰 때까지 성공한다. 그래서 <b>수신 시각</b>만 본다.
/// </para>
///
/// <para>
/// <see cref="GatheringScheduler"/>와 타이머를 합치지 않았다. 채취 정산과 세션 정리는
/// 서로 다른 관심사이고, 주기도 다르다(1초 vs 5초).
/// </para>
/// </summary>
public interface ISessionWatchdog
{
    void Start(MikaServer server);
    void Stop();
}
public sealed class SessionWatchdog : ISessionWatchdog 
{
    private Timer? _timer;
    private MikaServer? _server;

    /// <summary>
    /// 검사 주기. <b>판정 시간이 아니다</b> — 얼마나 자주 들여다볼지일 뿐이다.
    /// 실제 끊김 시점은 최대 이만큼 늦어지므로 <see cref="Global.SessionIdleTimeout"/>보다 충분히 짧게 둔다.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly ILogicExecutor _logicExecutor;
    
    public SessionWatchdog(ILogicExecutor logicExecutor)
    {
        _logicExecutor = logicExecutor;
    }
    
    public void Start(MikaServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (_timer is not null)
            return;

        _server = server;

        // 타이머 스레드에서 게임 상태를 만지면 안 된다. 로직 스레드로 넘긴다 —
        // 세션을 끊으면 Disconnected가 발화해 User.Destroy로 이어진다.
        _timer = new Timer(_ => _logicExecutor.Post(() => Sweep(DateTime.UtcNow)),
                           null, Interval, Interval);

        ServerLog.Info("세션",
            $"감시자 시작 — 검사 {Interval.TotalSeconds}초, 무응답 판정 {Global.SessionIdleTimeout.TotalSeconds}초");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>로직 스레드에서 실행된다.</summary>
    public void Sweep(DateTime now)
    {
        if (_server is null)
            return;

        try
        {
            var closed = _server.SweepIdle(now, Global.SessionIdleTimeout);
            if (closed > 0)
                ServerLog.Warn("세션", $"무응답으로 {closed}개 세션을 끊었다 (판정 {Global.SessionIdleTimeout.TotalSeconds}초)");
        }
        catch (Exception e)
        {
            // 한 번 실패했다고 감시가 멈추면 좀비가 영구히 남는다.
            ServerLog.Error("세션", "유휴 세션 정리 실패", e);
        }
    }
}
