using MikaNetwork;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 클라이언트 요청 핸들러. 전부 <b>로직 스레드</b>에서 실행된다.
///
/// <para>
/// "무슨 패킷이 언제 어느 스레드로 들어왔는가"는 패킷 로그(<see cref="PacketLogger"/>)가 이미 남긴다.
/// 여기서는 <b>요청의 내용</b>만 덧붙인다 — 같은 정보를 두 줄로 찍지 않는다.
/// </para>
/// </summary>
public static class ClientPacketHandler
{
    [PacketHandler]
    public static void Handle_C_EchoRequest(ISession session, C_EchoRequest req)
    {
        ServerLog.Debug("에코", $"sid={session.SessionId} \"{req.Message}\"");

        // 응답도 객체로 송신 (직렬화/프레이밍은 SendPacket이 처리)
        session.SendPacket(new S_EchoResponse { Message = req.Message });
    }

    [PacketHandler]
    public static void Handle_C_PingRequest(ISession session, C_PingRequest req)
    {
        session.SendPacket(new S_PongResponse());
    }


    [PacketHandler]
    public static void Handle_C_LoginRequest(ISession session, C_LoginRequest req)
    {
        // DB 스레드에서 조회/자동가입 → 로직 스레드에서 User 등록·응답 (LoginRepository.Apply)
        ServerLog.Info("로그인", $"요청 Id={req.Id} sessionId={session.SessionId}");

        UserManager.Instance.CreateUser(session, req.Id, req.Id);
    }

    [PacketHandler]
    public static void Handle_C_AddItemRequest(ISession session, C_AddItemRequest req)
    {
        ServerLog.Debug("아이템", $"지급 요청 ItemId={req.ItemId} 개수={req.Count} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_UpdateItemResponse { Result = EResultCode.NotLoggedIn });
            return;
        }

        user.AddItem(req.ItemId, req.Count);
    }

    [PacketHandler]
    public static void Handle_C_GachaDrawRequest(ISession session, C_GachaDrawRequest req)
    {
        ServerLog.Debug("가챠", $"뽑기 요청 GachaId={req.GachaId} 횟수={req.DrawCount} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_GachaDrawResponse { Result = EResultCode.NotLoggedIn });
            return;
        }

        GachaService.Instance.Draw(user, req.GachaId, req.DrawCount);
    }

    /// <summary>
    /// 작업슬롯에 산업·캐릭터를 배치한다.
    /// <b>클라이언트가 요청하는 것은 "배치"뿐이고, 무엇이 몇 개 나오는지는 서버가 정한다.</b>
    /// </summary>
    [PacketHandler]
    public static void Handle_C_WorkStationAssignRequest(ISession session, C_WorkStationAssignRequest req)
    {
        ServerLog.Debug("작업슬롯",
            $"배치 요청 슬롯={req.SlotIndex} 산업={(GameData.ItemType)req.Industry} " +
            $"캐릭터={req.CharacterId} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_WorkStationAssignResponse { Result = EResultCode.NotLoggedIn });
            return;
        }

        user.AssignWorkStation(req.SlotIndex, (GameData.ItemType)req.Industry, req.CharacterId, DateTime.UtcNow);
    }
}
