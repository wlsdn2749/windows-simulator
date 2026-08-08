namespace WSGameServer;

/// <summary>
/// 슬롯의 <b>배치 설정</b>을 DB에 반영한다. 산업·캐릭터가 바뀌었을 때만 부르면 된다.
///
/// <para>
/// <b>진행도는 저장하지 않는다.</b> 오프라인 진행이 폐지되면서 진행도가 세션 지역 상태가 됐고
/// (접속마다 0에서 시작한다), 그래서 정산 결과로 이 테이블이 바뀔 일이 없어졌다.
/// 정산으로 생긴 아이템은 <c>GainItem</c>이 각자 저장하므로 여기서 신경 쓸 것이 없다.
/// </para>
/// </summary>
public sealed class SaveWorkStationSlotRepository : IRepository
{
    private readonly IReadOnlyList<WorkStationSlot> _slots;

    public SaveWorkStationSlotRepository(User user, IReadOnlyList<WorkStationSlot> slots)
    {
        User   = user;
        _slots = slots;
    }

    public long Key => User.SessionId;

    public User User { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        // 슬롯 여러 개를 한 번의 왕복으로 처리한다. Dapper는 배열을 넘기면 문장을 반복 실행한다.
        var rows = _slots.Select(s => new
        {
            // user_id 기준은 t_user.user_id(User.Uid)다. 로드(LoginRepository)와 반드시 같아야 한다.
            userId      = User.Uid,
            slotIndex   = s.SlotIndex,
            industry    = (int)s.Industry,
            characterId = s.CharacterId,
        }).ToList();

        await connection.ExecuteAsync(
            @"INSERT INTO t_user_workstation_slot (user_id, slot_index, industry, character_id)
              VALUES (@userId, @slotIndex, @industry, @characterId)
              ON CONFLICT (user_id, slot_index) DO UPDATE SET
                  industry     = excluded.industry,
                  character_id = excluded.character_id;",
            rows);
    }

    public void Apply()
    {
    }
}
