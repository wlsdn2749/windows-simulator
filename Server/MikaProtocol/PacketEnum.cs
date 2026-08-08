namespace MikaProtocol
{
    /// <summary>
    /// 요청 1:1 응답 패킷의 처리 결과. 응답 패킷의 <b>첫 프로퍼티</b>로 들어간다.
    /// <b>Ok가 아니면 그 응답의 payload는 전부 null</b>이다 — 클라이언트는 읽지 않는다.
    /// 서버 푸시 패킷(스냅샷·채취 결과 등)에는 넣지 않는다(실패 개념이 없다).
    /// 값 대역: 1~99 공통 / 100~ 가챠 / 200~ 작업슬롯. 도메인이 늘면 대역을 이어서 판다.
    /// </summary>
    public enum EResultCode : ushort
    {
        Ok = 0,

        // ── 1~99: 공통 ──
        NotLoggedIn     = 1,    // 로그인(User 생성) 전에 보낸 요청
        AlreadyLoggedIn = 2,    // 이미 로그인된 세션이 로그인을 다시 요청

        // ── 100~: 가챠 ──
        InvalidDrawCount = 100, // 허용되지 않는 뽑기 횟수 (1·10만 허용)
        InvalidGachaId   = 101, // 존재하지 않는 가챠 풀

        // ── 200~: 작업슬롯 ──
        InvalidSlotIndex  = 200, // 보유하지 않은 슬롯 번호
        CharacterNotOwned = 201, // 미보유 캐릭터 배치 시도
        NoAptitude        = 202, // 해당 산업 적성이 0인 캐릭터 배치 시도
    }

    public enum EItemChangeKind : byte
    {
        None = 0,
        Add = 1,
        Update = 2,
        Remove = 3,
    }

    // 아이템 등급(전역 공통). GameData.GlobalRarity(Enum.xlsx)와 값이 1:1이어야 한다 —
    // 서버가 테이블 값을 byte 캐스팅으로 그대로 실어 보내기 때문이다.
    public enum EGlobalRarity : byte
    {
        None = 0,
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5,
        Mythic = 6,
    }
}