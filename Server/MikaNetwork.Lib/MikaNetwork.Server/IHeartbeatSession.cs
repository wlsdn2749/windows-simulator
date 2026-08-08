namespace MikaNetwork.Server;

/// <summary>
/// 유휴 판정에 필요한 것만 노출하는 세션 면.
///
/// <para>
/// <b>왜 <c>ISession</c>에 얹지 않는가</b> — <c>ISession</c>은
/// <c>Assets/Scripts_Server/Network/MikaNetwork.Core</c>에 <b>수동 사본</b>이 있고
/// (자동 미러 대상이 아니다), 유휴 판정은 <b>서버만의 관심사</b>다. 클라 세션은 자기가
/// 끊긴 것을 소켓 오류로 알면 된다. 여기에 두면 Unity 사본과 갈라질 일이 없다.
/// </para>
///
/// <para>
/// 소켓을 요구하지 않으므로 <b>목으로 갈아 끼울 수 있다</b> —
/// <c>MikaServer.DisconnectIdle</c>이 실제 연결 없이 검증된다.
/// </para>
/// </summary>
public interface IHeartbeatSession
{
    long SessionId { get; }

    bool IsConnected { get; }

    /// <summary>
    /// 이 세션에서 <b>마지막으로 바이트를 받은</b> 시각(UTC).
    ///
    /// <para>
    /// 송신이 아니라 <b>수신</b>이 기준이다. 송신은 큐 적재라 소켓이 죽어도 성공하므로
    /// 살아 있음의 증거가 되지 못한다(이슈 #10). 무엇을 받았는지도 보지 않는다 —
    /// 하트비트든 일반 요청이든 바이트가 왔다는 사실만이 연결의 증거다.
    /// </para>
    /// </summary>
    DateTime LastReceivedAt { get; }

    void Disconnect();
}
