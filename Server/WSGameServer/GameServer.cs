using GameData;
using MikaNetwork.Server;

namespace WSGameServer;

public class GameServer : IDisposable
{
    private const string SQL_CONNECTION_STRING = "game.sqlite3";
    
    private readonly LogicExecutor _logicExecutor;
    private readonly NetworkManager _networkManager;
    private readonly GatheringScheduler _gatheringScheduler;
    private readonly SessionWatchdog _sessionWatchdog;
    private readonly DBManager _dbManager;

    // 매니저, Executor 등 전역 싱글톤은 한번 생성하고 이후는 생성자 주입
    public GameServer()
    {
        _logicExecutor = new LogicExecutor();
        _sessionWatchdog = new SessionWatchdog(_logicExecutor);
        _networkManager = new NetworkManager(_logicExecutor, _sessionWatchdog);
        _gatheringScheduler = new GatheringScheduler(_logicExecutor);
        _dbManager = new DBManager(_logicExecutor);
    }

    public void Initialize()
    {
        UserManager.Instance.Initialize(_dbManager, _logicExecutor);
    }

    public async Task Run()
    {
        await Task.Run(() =>
        {
            // Dapper 컬럼 매핑 옵션은 쓰지 않는다 — Row 프로퍼티가 DB 컬럼명(snake_case)과 1:1이다.
            GameTable.LoadAll(name => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name)));

            // 드롭 테이블·가챠 풀 추첨기를 미리 만들어 둔다. 반드시 GameTable.LoadAll 뒤에 온다.
            DropTableCatalog.Instance.LoadAll();
            GachaPoolCatalog.Instance.LoadAll();

            DBExecutor.Instance.Start(8);
            _logicExecutor.Start();

            // 파일명 → Shared/<file> 경로 탐색 → 커넥션 팩토리 구성. 테스트는 팩토리 오버로드로 :memory:를 넣는다.
            _dbManager.Initialize(SQL_CONNECTION_STRING);
            _networkManager.Initialize();

            // 접속 중인 플레이어의 채취를 주기적으로 정산해 밀어 준다(서버 권위).
            _gatheringScheduler.Start();
        });

    }
    
    
    public void Dispose()
    {
       
    }
}