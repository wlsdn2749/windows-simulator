using MikaNetwork;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// <see cref="User"/>가 클라이언트와 이어져 있는 통로.
/// <b><see cref="ISession"/> 전체가 아니라 User가 실제로 쓰는 셋만</b> 노출한다.
///
/// <para>
/// 전송 계층을 잘라 낸 이유는 <c>SendPacket</c>이 <b>확장 메서드(static)</b>라서다 —
/// 정적 호출은 객체를 거치지 않으므로 <c>Mock&lt;ISession&gt;</c>이 가로챌 지점이 없고,
/// 목이 볼 수 있는 것은 프레이밍이 끝난 <c>byte[]</c>뿐이다. 그런데
/// <c>GeneratedPacketIds</c>는 <c>Get&lt;T&gt;()</c>(타입→id) <b>단방향만</b> 생성하므로
/// 바이트에서 타입을 되찾으려면 테스트가 id→타입 표를 손으로 유지해야 하고,
/// 그 표가 곧 새로운 드리프트 원천이 된다.
/// </para>
///
/// <para>
/// 직렬화·프레이밍은 <see cref="SessionClientChannel"/> 안에서만 일어난다.
/// 덕분에 테스트는 와이어 포맷을 통과하지 않고 <b>패킷 객체를 그대로</b> 본다 —
/// "무엇이 몇 개 어떤 순서로 나갔는가"를 그대로 단언할 수 있다.
/// </para>
/// </summary>
public interface IClientChannel
{
    long SessionId { get; }

    /// <summary>끊긴 통로로는 보내지 않는다. 로그인 진입에서 확인한다.</summary>
    bool IsConnected { get; }

    void Send<T>(T packet) where T : IPacket;

    /// <summary>
    /// 통로를 닫는다. 중복 로그인으로 밀려나는 세션을 정리할 때 쓴다 —
    /// <c>User.Destroy()</c>는 게임 상태만 정리할 뿐 소켓을 닫지 않는다.
    /// </summary>
    void Disconnect();
}

/// <summary>
/// 운영 구현. <see cref="ISession"/>을 감싸 <c>SendPacket</c> 확장 메서드로 위임한다.
/// <b>이 클래스가 유일하게 전송 계층을 아는 곳이다.</b>
/// </summary>
public sealed class SessionClientChannel : IClientChannel
{
    private readonly ISession _session;

    public SessionClientChannel(ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public long SessionId   => _session.SessionId;
    public bool IsConnected => _session.IsConnected;

    public void Send<T>(T packet) where T : IPacket => _session.SendPacket(packet);

    public void Disconnect() => _session.Disconnect();
}
