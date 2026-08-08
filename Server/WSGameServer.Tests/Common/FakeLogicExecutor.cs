using MikaNetwork.Server;

namespace WSGameServer;

/// <summary>
/// 로직 스레드를 대신하는 가짜 실행기.
///
/// <para>
/// 운영 구현(<c>LogicExecutor</c>)은 전용 스레드가 채널을 소비하는 비동기라,
/// <c>Create()</c>/<c>Destroy()</c>가 예약한 작업이 <b>그 자리에서 돌지 않는다.</b>
/// 실행기가 싱글턴이던 시절에는 이 자리를 갈아끼울 수 없어 생명주기 검증이 통째로 막혀 있었다.
/// </para>
///
/// <para>
/// 모드가 둘이다 — 어느 쪽을 쓸지는 <b>검증 대상이 예약이냐 결과냐</b>로 갈린다.
/// <list type="bullet">
///   <item><b>기록 모드</b>(기본): 큐에 쌓기만 한다. "몇 번 예약됐는가"를 볼 때 쓴다.
///   멱등 가드처럼 <b>작업이 실제로 돌면 전역 상태를 만지는</b> 경우에도 이쪽이다 —
///   <c>User.OnDestroy</c>는 <c>UserManager.Instance</c>를 건드린다.</item>
///   <item><b>즉시 실행 모드</b>: <c>Post</c>가 곧바로 실행한다. 흐름 전체를 볼 때 쓴다.</item>
/// </list>
/// </para>
/// </summary>
internal sealed class FakeLogicExecutor : ILogicExecutor
{
    private readonly Queue<Action> _pending = new();

    /// <summary>예약된 작업 전부. 실행 여부와 무관하게 순서대로 쌓인다.</summary>
    public List<Action> Posted { get; } = new();

    /// <summary>true면 <see cref="Post"/>가 그 자리에서 실행한다(큐에 남기지 않는다).</summary>
    public bool RunImmediately { get; init; }

    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;
    public void Stop()  => IsRunning = false;

    public void Post(Action job)
    {
        Posted.Add(job);

        if (RunImmediately)
        {
            job();
            return;
        }

        _pending.Enqueue(job);
    }

    /// <summary>
    /// 쌓인 작업을 예약된 순서대로 비운다.
    /// <b>실행 중 새로 예약된 작업도 이어서 돈다</b> — 단일 스레드 실행기의 동작과 같다.
    /// </summary>
    /// <returns>실행한 작업 수.</returns>
    public int Drain()
    {
        var executed = 0;

        while (_pending.Count > 0)
        {
            _pending.Dequeue()();
            executed++;
        }

        return executed;
    }
}
