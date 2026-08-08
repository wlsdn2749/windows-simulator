using MemoryPack;

/// <summary>
/// 패킷 바디 안에서 재사용되는 데이터 타입(Info)들을 모아두는 파일.
/// - 패킷이 아니므로 [Packet(...)]·IPacket 은 붙이지 않는다.
/// - 단, MemoryPack 이 직렬화하려면 반드시 [MemoryPackable] partial 이어야 한다.
/// </summary>

namespace MikaProtocol
{
    // 인벤토리 아이템 한 칸 (item_id, count)
    [MemoryPackable]
    public partial class ItemInfo
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
    }
    
    [MemoryPackable]
    public partial class ItemChangeInfo   // Count는 델타가 아니라 갱신 후 누적 총량 — 클라는 덮어쓴다
    {
        public int ItemId { get; set; }
        public int Count  { get; set; }
        public EItemChangeKind Kind { get; set; }
    }

    /// <summary>
    /// 재화 한 종류의 보유량.
    /// <c>Amount</c>는 증감이 아니라 <b>확정된 잔액</b>이라 받는 쪽은 덮어쓰기만 하면 된다.
    /// </summary>
    [MemoryPackable]
    public partial class CurrencyInfo
    {
        public byte CurrencyType { get; set; }  // GameData.CurrencyType (1=Gold)
        public long Amount       { get; set; }  // 보유량. int로 받지 말 것 — 거래 경제에서 21억을 넘길 수 있다
    }

    // 가챠로 뽑힌 결과 1건 (인벤토리 누적 수량이 아닌 "이번에 획득한 것")
    [MemoryPackable]
    public partial class GachaRewardInfo
    {
        public int ItemId { get; set; }
        public int Count  { get; set; }        // 이번에 획득한 수량
        public EGlobalRarity Rarity { get; set; } // 연출용 등급
    }

    /// <summary>
    /// 보유 캐릭터 개체 하나.
    /// <c>CharacterId</c>는 DB 발급 개체 PK, <c>CharacterTid</c>는 테이블 정의 —
    /// 같은 캐릭터(TID)를 여러 장 가질 수 있으므로 배치·식별은 반드시 <c>CharacterId</c>로 한다.
    /// 이름·적성 같은 고정값은 내려보내지 않는다 — 클라이언트가 <c>CharacterTable</c>에서 TID로 읽는다.
    /// </summary>
    [MemoryPackable]
    public partial class CharacterInfo
    {
        public long CharacterId  { get; set; }
        public int  CharacterTid { get; set; }
        public int  Level        { get; set; }
        public int  Exp          { get; set; }
    }

    /// <summary>
    /// 작업슬롯 한 칸의 상태.
    /// 뒤쪽 세 값은 클라이언트가 <b>다음 채취까지 남은 시간을 로컬에서 계산</b>하라고 준다.
    /// 그 카운트다운은 연출일 뿐이고, 실제로 몇 개가 나왔는지는 서버가 정한다.
    ///
    /// <para>
    /// <b>주기를 초로 내려보내지 않는 이유:</b> 캐릭터 스탯·버프로 슬롯마다 주기가 달라져서
    /// "30초"라는 고정값이 없다. 대신 진행도·속도·1회 비용을 그대로 주면 클라이언트가
    /// <c>남은시간 = (비용 - 진행도 - 경과 × 속도) / 속도</c>로 직접 구할 수 있고,
    /// 나중에 주기 규칙이 바뀌어도 클라이언트를 고치지 않아도 된다.
    /// </para>
    /// </summary>
    [MemoryPackable]
    public partial class WorkStationSlotInfo
    {
        public int  SlotIndex      { get; set; }
        public byte Industry       { get; set; }  // GameData.ItemType (0=미지정)
        public long CharacterId    { get; set; }  // 0=비어 있음 (채취하지 않는다)
        public long LastTickAtUnix { get; set; }  // 마지막 정산 시각 (Unix epoch 초, UTC)
        public long ProgressUnits  { get; set; }  // 마지막 정산 시점의 누적 작업량 (판정에 못 미친 자투리)
        public int  CurrentWorkSpeed { get; set; }  // 현재 작업속도 — 보정 전부 적용된 확정값 (1000 = 기준 1.0배)
        public long JudgeCostUnits { get; set; }  // 판정 1회에 필요한 작업량
    }
}
