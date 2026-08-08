namespace WSGameServer;

/// <summary>
/// DB 작업을 밀어 넣는 큐. <see cref="User"/>와 DB 사이의 <b>경계</b>다.
///
/// <para>
/// 운영 구현(<see cref="DBManager"/>)은 요청을 <c>DBExecutor</c>(DB 스레드)로 보내고
/// 결과를 <c>LogicExecutor</c>(로직 스레드)로 되돌린다 — <b>스레드를 두 번 건너는 비동기</b>다.
/// 그래서 User 로직을 검증하려면 실행기 두 개와 SQLite를 세우고 완료를 기다려야 했다.
/// </para>
///
/// <para>
/// <b>이 인터페이스는 커넥션이 아니라 큐를 자른다.</b> <c>DBManager.Initialize(Func&lt;SqliteConnection&gt;)</c>
/// 는 DB <b>안쪽</b>(파일이냐 <c>:memory:</c>냐)을 갈아끼우는 것이라 층이 다르다 —
/// SQL이 맞게 도는지는 Repository 테스트가 <c>:memory:</c>로 보고,
/// "저장 요청이 나갔는가·무엇을 담아 보냈는가"는 여기서 본다.
/// </para>
/// </summary>
public interface IDBQueue
{
    void Post<TRepository>(TRepository repository) where TRepository : IRepository;
}
