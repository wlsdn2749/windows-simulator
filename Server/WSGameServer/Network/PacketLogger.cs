using System.Diagnostics;
using MikaNetwork;
using MikaProtocol;
using WSGameServer;

namespace WSGameServer;

/// <summary>
/// 프레임워크·프로토콜 계층이 뚫어 둔 패킷 훅을 <see cref="ServerLog"/>에 연결한다.
///
/// <para>
/// 훅을 채우는 코드를 여기 한 곳에 모아 둔다 — 로그 형식을 바꾸고 싶을 때 찾아갈 파일이 하나여야 한다.
/// 훅이 정적이라 프로세스당 한 번만 연결하면 되고, 서버와 더미 클라이언트는 별도 프로세스라 겹치지 않는다.
/// </para>
/// </summary>
public static class PacketLogger
{
    private const string Category = "패킷";

    public static void Attach()
    {
        // 수신: 핸들러 직전 = 실제로 처리하는 스레드에서 찍힌다.
        MikaPacketManager.Dispatching = (session, id, bodySize, arrivedAt) =>
        {
            var waitMs = (Stopwatch.GetTimestamp() - arrivedAt) * 1000.0 / Stopwatch.Frequency;

            ServerLog.Debug(Category,
                $"수신 {Name(id)}({id}) sid={session.SessionId} {bodySize}B 대기 {waitMs:F1}ms");
        };

        MikaPacketManager.UnknownReceived = (session, id) =>
            ServerLog.Warn(Category, $"미처리 패킷 id={id} sid={session.SessionId} — 핸들러 누락이거나 버전 불일치");

        // 송신: 직렬화까지 끝난 뒤라 프레임 전체 크기를 그대로 쓸 수 있다.
        MikaSessionPacketExtensions.Sent = (session, id, size) =>
            ServerLog.Debug(Category, $"송신 {Name(id)}({id}) sid={session.SessionId} {size}B");
    }

    /// <summary>
    /// 패킷 id를 이름으로. <b>정의되지 않은 값은 이름 자리에 숫자를 찍지 않는다</b> —
    /// enum 캐스팅은 미정의 값도 그대로 통과시켜서, 그냥 ToString하면 <c>17(17)</c> 같은 줄이 나온다.
    /// </summary>
    private static string Name(ushort id)
    {
        var packetId = (PacketId)id;
        return Enum.IsDefined(packetId) ? packetId.ToString() : "Unknown";
    }
}
