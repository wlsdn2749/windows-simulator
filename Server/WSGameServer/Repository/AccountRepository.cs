namespace WSGameServer;

public class AccountRepository : IRepository
{
    private long _userId;
    private bool _isNewbie;

    private AccountResultRow? _resultRow;

    public AccountRepository(User user)
    {
        User = user;
    }

    public long Key { get => User.SessionId; }

    public User User { get; init; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        var row = await connection.QueryFirstOrDefaultAsync<AccountQueryRow>(
            "SELECT user_id FROM t_user WHERE provider_id = @pid",
        new {pid = User.Pid});

        // 2) 없으면 자동 가입
        if (row is null)
        {
            _userId = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO t_user (provider_id, nickname) VALUES (@pid, @nickname) RETURNING user_id",
                new { pid = User.Pid, nickname = User.NickName });

            _isNewbie = true;
        }
        else
        {
            _userId = row.user_id; // 있으면 _userId에 넣어주기
        }


        // 3) AccountResultRow 채워주기
        _resultRow = await connection.QueryFirstOrDefaultAsync<AccountResultRow>(
            "SELECT user_id, nickname, admin_level, is_deleted, is_banned FROM t_user WHERE user_id = @userId",
            new {userId = _userId});
    }

    public void Apply()
    {
        if (_resultRow == null)
        {
            return;
        }

        // 밴
        if (_resultRow.is_banned == 1)
        {
            // Log
            return;
        }

        // 삭제
        if (_resultRow.is_deleted == 1)
        {
            //Log
            return;
        }

        User.Initialize(_userId, _resultRow.nickname, _resultRow.admin_level, _isNewbie);

    }

    // Row 프로퍼티 = DB 컬럼명 그대로 — RepositoryContracts.cs 상단 주석 참조
    private sealed record AccountQueryRow
    {
        public long user_id { get; init; }
    }

    private sealed record AccountResultRow
    {
        public long user_id { get; init; }
        public required string nickname { get; init; }
        public int admin_level { get; init; }
        public int is_deleted { get; init; }
        public int is_banned { get; init; }
    }
}
