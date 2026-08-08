namespace WSGameServer;

/// <summary>
/// 로그인한 유저에게 딸린 데이터(인벤토리·재화·캐릭터·작업슬롯)를 한 번에 읽어 온다.
/// 유저 조회·자동가입은 <see cref="AccountRepository"/>가 먼저 끝낸다.
///
/// <para>
/// ExecuteAsync(DB 스레드)는 <b>읽기 전용 — Row 수집까지만</b> 한다. 도메인 변환·신규 지급 판단·
/// 응답 전송은 로직 스레드(<see cref="User.OnLoginDataLoaded"/>)가 맡는다.
/// </para>
/// </summary>
public sealed class LoginRepository : IRepository
{
    // ExecuteAsync에서 채우고 Apply에서 넘기는 조회 결과 (전부 Row — 변환하지 않는다)
    private List<InventoryRow>       _inventoryRows       = new();
    private List<CurrencyRow>        _currencyRows        = new();
    private List<CharacterRow>       _characterRows       = new();
    private List<WorkStationSlotRow> _workStationSlotRows = new();

    // DBExecutor 파티션 키 — 같은 세션 작업은 직렬 처리
    public long Key => User.SessionId;

    public User User { get; init; }

    public LoginRepository(User user)
    {
        User = user;
    }

    // === DB 스레드에서 실행 ===
    //
    // Row 프로퍼티 = DB 컬럼명 그대로(snake_case)라 매핑 옵션 없이 1:1로 대응된다.
    // RepositoryContracts.cs 상단 주석 참조.
    public async Task ExecuteAsync(DbConnection connection)
    {
        // 1) 인벤토리
        _inventoryRows = await connection.QueryAsync<InventoryRow>(
            "SELECT item_id, count FROM t_user_inventory WHERE user_id = @userId",
            new { userId = User.Uid });

        // 2) 재화. 보유하지 않은 재화는 행이 없고, 그건 0으로 본다
        //    (가입 시 0짜리 행을 만들지 않는다 — 재화 종류가 늘 때마다 백필이 필요해진다).
        _currencyRows = await connection.QueryAsync<CurrencyRow>(
            "SELECT currency_type, amount FROM t_user_currency WHERE user_id = @userId",
            new { userId = User.Uid });

        // 3) 캐릭터. 하나도 없으면(신규 유저) 지급 판단은 로직 스레드가 한다.
        _characterRows = await connection.QueryAsync<CharacterRow>(
            @"SELECT character_id, character_tid, level, exp 
              FROM t_character WHERE user_id = @userId",
            new { userId = User.Uid });

        // 4) 작업슬롯. 슬롯 행에는 진행도가 없다 — 배치 설정(산업·캐릭터)뿐이다.
        //    오프라인 진행이 폐지돼 비운 동안의 누적이 없으므로 저장할 진행도 자체가 없다.
        //    기본 슬롯 개설도 로직 스레드가 한다.
        _workStationSlotRows = await connection.QueryAsync<WorkStationSlotRow>(
            @"SELECT slot_index, industry, character_id
              FROM t_user_workstation_slot WHERE user_id = @userId",
            new { userId = User.Uid });
    }

    // === 로직 스레드에서 실행 ===
    //
    // 시각은 여기서 만든다. Apply()는 인자를 받을 수 없는 인프라 경계이고,
    // User 쪽은 시각을 전부 인자로 받도록 되어 있어 "만드는 곳"이 진입점으로 밀려난 결과다.
    public void Apply()
    {
        User.OnLoginDataLoaded(
            new PlayerLoginData(_inventoryRows, _currencyRows, _characterRows, _workStationSlotRows),
            DateTime.UtcNow);
    }
}
