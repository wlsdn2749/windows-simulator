using System;
using System.Collections.Generic;
using MemoryPack;

/// <summary>
/// 1. 패킷은 반드시 ushort인 id, size를 포함해야 함.
/// 2. ([id][size][---body---]) 이렇게 이루어진 byte array를 TCP로 송수신 함
/// 3. id, size는 먼저 body를 serialize한 후, size를 측정하여 앞 비트에 써넣는 방식을 사용하며 
/// 4. body부분은 MemoryPack 등으로 Serialize/Deserialize 한다.
/// </summary>
///

namespace MikaProtocol
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PacketAttribute : Attribute
    {
        public PacketId Id { get;}
        public PacketAttribute(PacketId id)
        {
            Id = id;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PacketHandlerAttribute : Attribute { }


    public enum PacketId : ushort
    {
        None = 0,
        C_EchoRequest = 1,
        S_EchoResponse = 2,
        C_PingRequest = 3,
        S_PongResponse = 4,
        C_LoginRequest = 5,
        S_LoginResponse = 6,
        C_AddItemRequest = 7,
        S_UpdateItemResponse = 8,
        C_GachaDrawRequest = 9,
        S_GachaDrawResponse = 10,
        S_InventoryResponse = 11,
        C_WorkStationAssignRequest = 12,
        S_WorkStationAssignResponse = 13,
        S_WorkStationSlotsResponse = 14,
        S_GatherResultResponse = 15,
        S_CurrencyResponse = 16,
        S_CharacterListResponse = 17,
    }

    [MemoryPackable, Packet(PacketId.C_EchoRequest)]
    public partial class C_EchoRequest : IPacket
    {
        public string Message { get; set; } = "";
    }

    [MemoryPackable, Packet(PacketId.S_EchoResponse)]
    public partial class S_EchoResponse : IPacket
    {
        public string Message { get; set; } = "";
    }

    [MemoryPackable, Packet(PacketId.C_PingRequest)]
    public partial class C_PingRequest : IPacket
    {
        
    }
    
    [MemoryPackable, Packet(PacketId.S_PongResponse)]
    public partial class S_PongResponse : IPacket
    {

    }

    [MemoryPackable, Packet(PacketId.C_LoginRequest)]
    public partial class C_LoginRequest : IPacket
    {
        public string Id { get; set; } = "";
    }

    [MemoryPackable, Packet(PacketId.S_LoginResponse)]
    public partial class S_LoginResponse : IPacket
    {
        public EResultCode Result { get; set; }
        public long SessionId { get; set; }
    }

    [MemoryPackable, Packet(PacketId.C_AddItemRequest)]
    public partial class C_AddItemRequest : IPacket
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
    }

    [MemoryPackable, Packet(PacketId.S_UpdateItemResponse)]
    public partial class S_UpdateItemResponse : IPacket
    {
        public EResultCode Result { get; set; }
        public List<ItemChangeInfo>? ItemChangeInfos { get; set; }
    }

    [MemoryPackable, Packet(PacketId.C_GachaDrawRequest)]
    public partial class C_GachaDrawRequest : IPacket
    {
        public int GachaId { get; set; }    // 뽑을 풀 ID
        public int DrawCount { get; set; }  // 1(단차) 또는 10(10연차)
    }

    [MemoryPackable, Packet(PacketId.S_GachaDrawResponse)]
    public partial class S_GachaDrawResponse : IPacket
    {
        public EResultCode Result { get; set; }
        public List<GachaRewardInfo>? Rewards { get; set; }  // 연출용 — 뽑힌 순서대로 (델타)
        public List<ItemChangeInfo>? ItemChangeInfos { get; set; }  // 인벤토리 반영용 — 갱신 후 누적 총량
    }

    [MemoryPackable, Packet(PacketId.S_InventoryResponse)]
    public partial class S_InventoryResponse : IPacket
    {
        public List<ItemInfo>? Items { get; set; }  // 로그인 시 인벤토리 전체 스냅샷
    }

    // ───────────────────── 작업슬롯 (WorkStationSlot) ─────────────────────

    [MemoryPackable, Packet(PacketId.C_WorkStationAssignRequest)]
    public partial class C_WorkStationAssignRequest : IPacket
    {
        public int  SlotIndex   { get; set; }  // 배치할 슬롯 번호
        public byte Industry    { get; set; }  // 지정 산업 = GameData.ItemType (0=해제)
        public long CharacterId { get; set; }  // 배치할 캐릭터 (0=해제)
    }

    [MemoryPackable, Packet(PacketId.S_WorkStationAssignResponse)]
    public partial class S_WorkStationAssignResponse : IPacket
    {
        public EResultCode          Result { get; set; }
        public WorkStationSlotInfo? Slot   { get; set; }  // 변경된 슬롯의 최신 상태
    }

    [MemoryPackable, Packet(PacketId.S_WorkStationSlotsResponse)]
    public partial class S_WorkStationSlotsResponse : IPacket
    {
        public List<WorkStationSlotInfo>? Slots { get; set; }  // 로그인 시 슬롯 전체 스냅샷
    }

    /// <summary>
    /// 채취 결과 푸시. 클라이언트 요청 없이 <b>서버가 판정 후 밀어 준다</b>(서버 권위).
    ///
    /// <para>
    /// 도착 간격은 <b>슬롯마다 다르다</b> — 캐릭터 적성·버프가 슬롯별 채취 속도를 정하기 때문이다.
    /// 서버는 1초 해상도로 깨어나 그 시점까지 완성된 판정만 담아 보내므로, 이 패킷이 안 온다고
    /// 진행이 멈춘 것은 아니다. 슬롯 변경 시에는 그때까지의 구간을 한 번에 정산해 보낸다.
    /// </para>
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_GatherResultResponse)]
    public partial class S_GatherResultResponse : IPacket
    {
        public int                   SlotIndex   { get; set; }  // 어느 슬롯에서 나왔는지
        public int                   JudgeCount  { get; set; }  // 이번에 정산된 판정 횟수(연출용)
        public List<ItemChangeInfo>? ItemChanges { get; set; }  // 인벤토리 갱신분
    }

    // ───────────────────────── 재화 (Currency) ─────────────────────────

    /// <summary>
    /// 재화 보유량 통지. <b>로그인 스냅샷과 변경 푸시가 같은 패킷을 쓴다.</b>
    ///
    /// <para>
    /// 아이템처럼 스냅샷/델타 패킷을 나누지 않는 이유는 <see cref="CurrencyInfo.Amount"/>가
    /// 증감이 아니라 <b>확정된 잔액</b>이기 때문이다. 클라이언트는 두 경우 모두
    /// <b>재화 종류로 덮어쓰기</b>만 하면 되므로 처리 경로가 하나로 끝난다.
    /// </para>
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_CurrencyResponse)]
    public partial class S_CurrencyResponse : IPacket
    {
        public List<CurrencyInfo>? Currencies { get; set; }
    }

    // ───────────────────────── 캐릭터 (Character) ─────────────────────────

    /// <summary>
    /// 보유 캐릭터 전체 스냅샷(로그인 시). 클라이언트는 여기서 받은 <c>CharacterId</c>로
    /// <see cref="C_WorkStationAssignRequest"/>의 배치 대상을 지정한다 —
    /// 이 패킷 없이는 클라이언트가 자기 캐릭터의 개체 PK를 알 방법이 없다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_CharacterListResponse)]
    public partial class S_CharacterListResponse : IPacket
    {
        public List<CharacterInfo>? Characters { get; set; }
    }



}