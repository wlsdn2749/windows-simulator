namespace WSGameServer;

/// <summary>
/// 캐릭터 한 장을 지급(INSERT)하고 발급된 개체 PK를 로직 스레드로 돌려준다.
/// 레벨·경험치는 DB 기본값(1·0)으로 시작한다.
/// </summary>
public sealed class GrantCharacterRepository : IRepository
{
    private readonly int _characterTid;
    private long _characterId;

    public long Key => User.SessionId;

    public User User { get; }

    public GrantCharacterRepository(User user, int characterTid)
    {
        User = user;
        _characterTid = characterTid;
    }

    // === DB 스레드에서 실행 ===
    public async Task ExecuteAsync(DbConnection connection)
    {
        _characterId = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO t_character (user_id, character_tid)
              VALUES (@userId, @tid) RETURNING character_id;",
            new { userId = User.Uid, tid = _characterTid });
    }

    // === 로직 스레드에서 실행 ===
    public void Apply()
    {
        User.OnDefaultCharacterGranted(_characterId, DateTime.UtcNow);
    }
}
