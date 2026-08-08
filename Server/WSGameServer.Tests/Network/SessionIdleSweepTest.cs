using MikaNetwork.Server;

namespace WSGameServer;

/// <summary>
/// 무응답 세션 정리 검증 (이슈 #10).
///
/// <para>
/// 접속 판정이 곧 재화 생성 조건이라(게임기획코어 P2), <b>끊긴 걸 서버가 모르는 구간이
/// 그대로 부당 적립 구간</b>이 된다. 여기서 놓치면 폐지한 오프라인 진행이 되살아난다.
/// </para>
/// </summary>
public class SessionIdleSweepTest
{
    private static readonly DateTime Base = new(2026, 8, 4, 0, 0, 0, DateTimeKind.Utc);

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static Mock<IHeartbeatSession> Session(DateTime lastReceivedAt, bool connected = true)
    {
        var mock = new Mock<IHeartbeatSession>();
        mock.SetupGet(s => s.SessionId).Returns(1);
        mock.SetupGet(s => s.IsConnected).Returns(connected);
        mock.SetupGet(s => s.LastReceivedAt).Returns(lastReceivedAt);
        return mock;
    }

    [Fact]
    public void 판정_시간을_넘겨_무응답이면_끊는다()
    {
        var zombie = Session(Base);

        MikaServer.DisconnectIdle(new[] { zombie.Object }, Base.AddSeconds(15), Timeout).ShouldBe(1);

        zombie.Verify(s => s.Disconnect(), Times.Once);
    }

    [Fact]
    public void 판정_시간_전이면_끊지_않는다()
    {
        // 클라는 5초마다 Ping을 보낸다. 14초는 두 번 놓친 상태 — 아직 살아 있다고 본다.
        var alive = Session(Base);

        MikaServer.DisconnectIdle(new[] { alive.Object }, Base.AddSeconds(14), Timeout).ShouldBe(0);

        alive.Verify(s => s.Disconnect(), Times.Never);
    }

    [Fact]
    public void 이미_끊긴_세션은_다시_끊지_않는다()
    {
        var closed = Session(Base, connected: false);

        MikaServer.DisconnectIdle(new[] { closed.Object }, Base.AddHours(1), Timeout).ShouldBe(0);

        closed.Verify(s => s.Disconnect(), Times.Never);
    }

    [Fact]
    public void 살아_있는_세션은_두고_무응답만_골라_끊는다()
    {
        var zombie = Session(Base);
        var alive  = Session(Base.AddSeconds(12));   // 3초 전에 받았다

        MikaServer.DisconnectIdle(
            new[] { zombie.Object, alive.Object }, Base.AddSeconds(15), Timeout).ShouldBe(1);

        zombie.Verify(s => s.Disconnect(), Times.Once);
        alive.Verify(s => s.Disconnect(), Times.Never);
    }

    [Fact]
    public void 세션이_없어도_터지지_않는다()
    {
        MikaServer.DisconnectIdle(Array.Empty<IHeartbeatSession>(), Base, Timeout).ShouldBe(0);
    }

    [Fact]
    public void 판정_시간은_채취_한_주기보다_짧다()
    {
        // 이 순서가 뒤집히면 부당 적립 구간이 판정 1회를 채운다 —
        // 끊기기 전에 아이템이 나가고, 그게 폐지한 오프라인 진행이다.
        Global.SessionIdleTimeout
            .ShouldBeLessThan(TimeSpan.FromSeconds(WorkStationSlot.BaseCycleSeconds));
    }
}
