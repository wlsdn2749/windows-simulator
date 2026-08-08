namespace WSGameServer;

/// <summary>
/// 세션 정리의 멱등성과 중복 로그인 시 매핑 정합성 검증 (이슈 #10).
///
/// <para>
/// 정리 경로가 여러 곳(소켓 해제 · 유휴 스윕 · 중복 로그인)에서 들어오므로,
/// <b>두 번 도는 것</b>과 <b>남의 것을 지우는 것</b>이 실제 위험이다.
/// 앞은 종료 정산 중복이고, 뒤는 살아 있는 세션의 매핑이 끊기는 것이다.
/// </para>
/// </summary>
public class UserManagerTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    // 가드는 Entity 기반 클래스에 있었고, 그때는 스텁 하위 타입으로만 볼 수 있었다.
    // 실행기가 주입 가능해진 뒤로는 진짜 User에 대고 "몇 번 예약됐는가"를 직접 센다.
    // 실행기는 기록 모드다 — OnDestroy를 실제로 돌리면 UserManager.Instance(전역)가 오염된다.

    [Fact]
    public void Destroy를_여러_번_불러도_정리는_한_번만_예약된다()
    {
        // 종료 정산이 OnDestroy에 들어 있어, 두 번 돌면 같은 구간을 두 번 지급하게 된다.
        var builder = new TestUserBuilder();
        var user    = builder.Build();

        user.Destroy().ShouldBeTrue();
        user.Destroy().ShouldBeFalse();
        user.Destroy().ShouldBeFalse();

        // 반환값이 아니라 실제 계약을 본다 — OnDestroy가 큐에 한 번만 실렸는가.
        builder.Executor.Posted.Count.ShouldBe(1);
    }

    [Fact]
    public void Destroy_이후에는_정리됨으로_표시된다()
    {
        var user = new TestUserBuilder().Build();

        user.IsDestroyed.ShouldBeFalse();
        user.Destroy();
        user.IsDestroyed.ShouldBeTrue();
    }

    // ─────────────────── 중복 로그인 매핑 정합성 ───────────────────

    private static User Join(UserManager manager, string pid, long sessionId, out FakeClientChannel channel)
    {
        var ch = new FakeClientChannel { SessionId = sessionId };
        channel = ch;

        var user = new User(ch, new FakeDBQueue(), new FakeLogicExecutor(),
                            pid, nickname: "테스터", loggedInAt: Base);
        manager.JoinUser(user);
        return user;
    }

    [Fact]
    public void 밀려난_유저가_나가도_새_유저의_pid_매핑은_살아_있다()
    {
        // 이슈 #10 증상 3의 뿌리다. 좀비의 LeaveUser가 뒤늦게 돌면서
        // 키만 보고 지우면, 방금 들어온 정상 세션의 매핑까지 함께 사라진다.
        var manager = new UserManager();

        var zombie = Join(manager, "same-pid", sessionId: 1, out _);
        var fresh  = Join(manager, "same-pid", sessionId: 2, out _);

        // 새 유저가 자리를 차지하도록 좀비 매핑을 먼저 비운 뒤 다시 넣는다
        // (CreateUser가 하는 일과 같은 순서).
        manager.LeaveUser(zombie);
        manager.JoinUser(fresh);

        manager.TryGetUserByPid("same-pid", out var found).ShouldBeTrue();
        found!.SessionId.ShouldBe(2);
    }

    [Fact]
    public void 좀비가_늦게_나가도_새_유저를_밀어내지_않는다()
    {
        // 순서가 뒤집힌 경우 — 새 유저가 먼저 자리를 잡고, 좀비 정리가 나중에 온다.
        var manager = new UserManager();

        var zombie = Join(manager, "same-pid", sessionId: 1, out _);
        manager.LeaveUser(zombie);

        var fresh = Join(manager, "same-pid", sessionId: 2, out _);

        // 뒤늦게 한 번 더 들어오는 정리(멱등 가드가 없던 시절의 중복 경로)
        manager.LeaveUser(zombie);

        manager.TryGetUserByPid("same-pid", out var found).ShouldBeTrue();
        found!.SessionId.ShouldBe(2);
    }

    [Fact]
    public void 나간_유저는_세션과_pid_모두에서_사라진다()
    {
        var manager = new UserManager();
        var user = Join(manager, "solo-pid", sessionId: 9, out _);

        manager.LeaveUser(user);

        manager.TryGetUserByPid("solo-pid", out _).ShouldBeFalse();
        manager.TryGetUserBySessionId(9, out _).ShouldBeFalse();
        manager.Count.ShouldBe(0);
    }
}
