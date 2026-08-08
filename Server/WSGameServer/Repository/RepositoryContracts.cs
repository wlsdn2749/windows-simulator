namespace WSGameServer;

// Dapper 조회 전용 Row — DB 컬럼 값을 그대로 나른다. Protocol DTO(ItemInfo 등)와 분리한다.
// 도메인 변환·정책(스킵·시작 시각 등)은 로직 스레드(User.OnLoginDataLoaded)가 한다.
//
// ⚠️ 프로퍼티 이름 = DB 컬럼 이름 그대로(snake_case). 매핑 옵션 없이 SQL과 1:1로 대응된다 —
//    Row 타입에 한해 C# 명명 관례(PascalCase)의 예외로 둔다. 컬럼과 이름이 어긋나면 매핑이 조용히 빈다.
//
// ⚠️ 전부 "프로퍼티 record"로 둔다 — 위치 기반 record로 바꾸지 않는다.
//    SQLite는 INTEGER를 전부 long으로 돌려주는데, long → int 변환은 프로퍼티 매핑에서만 동작한다.
//    위치 기반이면 생성자 매핑으로 떨어져 타입 불일치로 깨진다.

// t_character 조회 전용 Row. character_id는 개체 PK(long), character_tid는 테이블 정의(int)다.
public sealed record CharacterRow
{
    public long character_id  { get; init; }
    public int  character_tid { get; init; }
    public int  level         { get; init; }
    public int  exp           { get; init; }
}

// t_user_currency 조회 전용 Row. amount는 반드시 long이다 —
// 거래 경제가 붙으면 누적 골드가 int 상한(약 21억)을 넘길 수 있다.
public sealed record CurrencyRow
{
    public int  currency_type { get; init; }
    public long amount        { get; init; }
}

// t_user_inventory 조회 전용 Row
public sealed record InventoryRow
{
    public int item_id { get; init; }
    public int count   { get; init; }
}

// t_user_workstation_slot 조회 전용 Row (배치 설정만 — 진행도는 저장하지 않는다)
public sealed record WorkStationSlotRow
{
    public int  slot_index   { get; init; }
    public int  industry     { get; init; }
    public long character_id { get; init; }
}

/// <summary>
/// 로그인 시 리포지토리가 로직 스레드로 넘기는 조회 결과 묶음.
/// Row가 리포지토리 밖으로 나가는 유일한 통로다 — 순수 코어(Inventory·Wallet·WorkStation)에는 넘기지 않는다.
/// </summary>
public sealed record PlayerLoginData(
    List<InventoryRow> InventoryRows,
    List<CurrencyRow> CurrencyRows,
    List<CharacterRow> CharacterRows,
    List<WorkStationSlotRow> WorkStationSlotRows);
