using MikaNetwork;

namespace WSGameServer;

/// <summary>
/// 핸들러로 <see cref="ISession"/>만 들어왔을 때 SessionId로 User를 조회하는 헬퍼.
/// Session에 User 역참조 필드를 두지 않고, UserManager 딕셔너리를 session->user 맵으로 사용한다.
/// </summary>
public static class SessionUserExtensions
{
    public static User? GetUser(this ISession session)
        => UserManager.Instance.TryGetUserBySessionId(session.SessionId, out var user) ? user : null;
}
