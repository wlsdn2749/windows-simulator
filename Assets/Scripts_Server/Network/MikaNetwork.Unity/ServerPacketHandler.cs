#if UNITY_5_3_OR_NEWER

using System;
using MikaProtocol;

namespace MikaNetwork
{
    /// <summary>
    /// 서버가 보낸 응답 패킷을 받는 진입점.
    /// - [PacketHandler] 메서드는 Source Generator가 파라미터 타입 기준으로 자동 등록한다.
    /// - 실제 실행은 NetworkMessageQueue를 거쳐 Unity 메인 스레드에서 이뤄지므로
    ///   여기서 이벤트 발행·UI 갱신을 그대로 해도 안전하다.
    /// - 이 클래스는 "얇게" 유지하고, 실제 처리는 이벤트를 구독하는 클라이언트 코드에서 한다.
    ///
    /// <para>
    /// <b>[로그인 1회 수신 세트]</b> — C_LoginRequest 하나를 보내면 서버가 아래를 <b>연달아 1회씩</b> 밀어준다.
    /// 클라가 따로 조회 요청을 보낼 패킷은 없다.
    /// <code>
    /// S_LoginResponse          로그인 결과
    /// S_InventoryResponse      인벤토리 전체 스냅샷
    /// S_GatherResultResponse   오프라인 누적 채취분 (있을 때만, 활성 슬롯당 1개)
    /// S_WorkStationSlotsResponse  작업슬롯 전체 스냅샷
    /// </code>
    /// UI 초기화는 이 네 개가 다 도착한 뒤를 기준으로 잡아야 한다.
    /// </para>
    /// </summary>
    public static class ServerPacketHandler
    {
        // 로그인 응답 도착 (Handle_S_LoginResponse에서 발행)
        public static event Action<S_LoginResponse>? LoginResponded;

        // 인벤토리 스냅샷 도착 — 로그인 직후 서버가 밀어줌 (Handle_S_InventoryResponse에서 발행)
        public static event Action<S_InventoryResponse>? InventoryReceived;

        // 가챠 결과 도착 (Handle_S_GachaDrawResponse에서 발행)
        public static event Action<S_GachaDrawResponse>? GachaDrawn;

        // 작업슬롯 배치 결과 도착 (Handle_S_WorkStationAssignResponse에서 발행)
        public static event Action<S_WorkStationAssignResponse>? WorkStationAssigned;

        // 작업슬롯 스냅샷 도착 — 로그인 직후 서버가 밀어줌 (Handle_S_WorkStationSlotsResponse에서 발행)
        public static event Action<S_WorkStationSlotsResponse>? WorkStationSlotsReceived;

        // 채취 결과 도착 — 서버가 주기적으로 밀어줌 (Handle_S_GatherResultResponse에서 발행)
        public static event Action<S_GatherResultResponse>? GatherResultReceived;

        // 재화 보유량 도착 — 로그인 스냅샷과 변경 푸시가 같은 패킷이다 (Handle_S_CurrencyResponse에서 발행)
        public static event Action<S_CurrencyResponse>? CurrencyReceived;

        // 아이템 증감 도착 (Handle_S_UpdateItemResponse에서 발행)
        public static event Action<S_UpdateItemResponse>? ItemUpdated;

        // 하트비트 응답 도착 — 연결이 살아 있다는 유일한 증거 (Handle_S_PongResponse에서 발행)
        public static event Action? PongReceived;

        // 보유 캐릭터 스냅샷 도착 — 로그인 직후 서버가 밀어줌 (Handle_S_CharacterListResponse에서 발행)
        public static event Action<S_CharacterListResponse>? CharacterListReceived;

        // 로그인 응답 — 성공 여부·세션ID를 이벤트로 전달 (S_LoginResponse 수신 시 자동 호출)
        // ※ 이 응답이 끝이 아니다. 뒤이어 인벤토리·(오프라인 채취분)·작업슬롯이 자동으로 따라온다.
        [PacketHandler]
        public static void Handle_S_LoginResponse(ISession session, S_LoginResponse res)
        {
            ClientLogger.Info(ClientLogger.Recv, $"로그인 결과={res.Result}, 세션ID={res.SessionId}");
            LoginResponded?.Invoke(res);
        }

        // 인벤토리 전체 스냅샷 (S_InventoryResponse 수신 시 자동 호출)
        // ★ 로그인 시 자동으로 1회 온다 — 요청 패킷 없이 서버가 S_LoginResponse 직후 밀어준다.
        //   단, 이 스냅샷은 오프라인 채취 정산 "전" 값이다. 뒤따라오는 S_GatherResultResponse의
        //   ItemChanges로 보정해야 실제 수량과 맞는다.
        [PacketHandler]
        public static void Handle_S_InventoryResponse(ISession session, S_InventoryResponse res)
        {
            int itemCount = res.Items?.Count ?? 0;
            ClientLogger.Info(ClientLogger.Recv, $"인벤토리 스냅샷 — 아이템 {itemCount}종");
            InventoryReceived?.Invoke(res);
        }

        // 가챠 결과 — 뽑힌 보상 목록 (S_GachaDrawResponse 수신 시 자동 호출)
        [PacketHandler]
        public static void Handle_S_GachaDrawResponse(ISession session, S_GachaDrawResponse res)
        {
            int rewardCount = res.Rewards?.Count ?? 0;
            int changeCount = res.ItemChangeInfos?.Count ?? 0;
            ClientLogger.Info(ClientLogger.Recv, $"가챠 결과={res.Result}, 보상 {rewardCount}건, 인벤토리 변경 {changeCount}건");
            GachaDrawn?.Invoke(res);
        }

        // 작업슬롯 배치 결과 — 변경된 슬롯의 최신 상태 (S_WorkStationAssignResponse 수신 시 자동 호출)
        [PacketHandler]
        public static void Handle_S_WorkStationAssignResponse(ISession session, S_WorkStationAssignResponse res)
        {
            // 배치와 해제가 같은 패킷이라 "배치 결과"로 적으면 해제했을 때 말이 어긋난다.
            ClientLogger.Info(ClientLogger.Recv, $"작업슬롯 변경 결과={res.Result}");
            WorkStationAssigned?.Invoke(res);
        }

        // 작업슬롯 전체 스냅샷 (S_WorkStationSlotsResponse 수신 시 자동 호출)
        // ★ 로그인 시 자동으로 1회 온다 — 슬롯 조회 요청 패킷은 아예 없고, 이게 유일한 전체 스냅샷이다.
        //   이후 슬롯 상태는 S_WorkStationAssignResponse로 한 칸씩만 갱신된다.
        [PacketHandler]
        public static void Handle_S_WorkStationSlotsResponse(ISession session, S_WorkStationSlotsResponse res)
        {
            int slotCount = res.Slots?.Count ?? 0;
            ClientLogger.Info(ClientLogger.Recv, $"작업슬롯 스냅샷 — {slotCount}칸");
            WorkStationSlotsReceived?.Invoke(res);
        }

        // 채취 결과 — 서버가 요청 없이 30초 주기로 밀어 준다(서버 권위).
        // ★ 로그인 시에도 1회 올 수 있다 — 오프라인 동안 밀린 구간을 한 번에 정산해 보내므로
        //   그때는 JudgeCount가 2 이상이다(30초 × N). 수확이 없으면 아예 오지 않는다.
        // ※ ItemChanges의 Count는 델타가 아니라 "갱신 후 누적 총량"이다. 더하지 말고 덮어쓸 것.
        [PacketHandler]
        public static void Handle_S_GatherResultResponse(ISession session, S_GatherResultResponse res)
        {
            int changeCount = res.ItemChanges?.Count ?? 0;
            ClientLogger.Info(ClientLogger.Recv, $"채취 정산 — 슬롯 {res.SlotIndex}, 판정 {res.JudgeCount}회, 변경 {changeCount}건");
            GatherResultReceived?.Invoke(res);
        }

        // 재화 보유량 (S_CurrencyResponse 수신 시 자동 호출)
        // ★ 로그인 시 자동으로 1회 온다. 스냅샷과 변경 푸시가 같은 패킷이라 처리 경로가 하나다.
        // ※ Amount는 증감이 아니라 확정 잔액이다 — 재화 종류로 덮어쓰기만 하면 된다.
        [PacketHandler]
        public static void Handle_S_CurrencyResponse(ISession session, S_CurrencyResponse res)
        {
            int currencyCount = res.Currencies?.Count ?? 0;
            ClientLogger.Info(ClientLogger.Recv, $"재화 — {currencyCount}종");
            CurrencyReceived?.Invoke(res);
        }

        // 아이템 증감 (S_UpdateItemResponse 수신 시 자동 호출)
        // ※ S_GatherResultResponse의 ItemChanges와 같은 규칙 — Count는 갱신 후 누적 총량이다.
        [PacketHandler]
        public static void Handle_S_UpdateItemResponse(ISession session, S_UpdateItemResponse res)
        {
            int changeCount = res.ItemChangeInfos?.Count ?? 0;
            ClientLogger.Info(ClientLogger.Recv, $"아이템 증감 — 변경 {changeCount}건");
            ItemUpdated?.Invoke(res);
        }

        // 보유 캐릭터 전체 스냅샷 (S_CharacterListResponse 수신 시 자동 호출)
        // ★ 로그인 시 자동으로 1회 온다. 조회 요청 패킷은 없다.
        // ※ CharacterId는 캐릭터 종류(TID)가 아니라 내가 가진 그 한 마리의 개체 번호다.
        //   슬롯 배치에 넣어야 하는 값이 이것이다 — 자세한 건 PlayerDataManager.Characters 주석 참조.
        [PacketHandler]
        public static void Handle_S_CharacterListResponse(ISession session, S_CharacterListResponse res)
        {
            int characterCount = res.Characters?.Count ?? 0;
            ClientLogger.Info(ClientLogger.Recv, $"보유 캐릭터 — {characterCount}마리");
            CharacterListReceived?.Invoke(res);
        }

        // 하트비트 응답 (S_PongResponse 수신 시 자동 호출)
        // ※ 로그를 남기지 않는다 — 5초마다 오므로 찍으면 콘솔이 Pong으로 덮인다.
        //   판정과 로그는 PingManager가 상태가 바뀔 때만 담당한다.
        [PacketHandler]
        public static void Handle_S_PongResponse(ISession session, S_PongResponse res)
        {
            PongReceived?.Invoke();
        }

        #region 테스트용 (연결 확인) — 추후 필요 없어지면 삭제

        // 에코 응답 — 왕복 연결 테스트용 (S_EchoResponse 수신 시 자동 호출)
        [PacketHandler]
        public static void Handle_S_EchoResponse(ISession session, S_EchoResponse res)
        {
            ClientLogger.Info(ClientLogger.Recv, $"에코 — {res.Message}");
        }

        #endregion
    }
}

#endif
