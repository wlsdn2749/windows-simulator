using GameData;

namespace WSGameServer;

/// <summary>
/// 유저의 재화 보유량을 DB에 반영한다.
///
/// <para>
/// <b>델타가 아니라 확정된 잔액을 쓴다.</b> 잔액 계산은 로직 스레드의 <c>CurrencyWallet</c>이
/// 이미 끝냈고, 델타를 DB에서 다시 더하면 재시도·중복 전송이 곧 재화 복제가 된다.
/// </para>
/// </summary>
public sealed class SaveCurrencyRepository : IRepository
{
    private readonly CurrencyType _type;
    private readonly long _amount;

    public SaveCurrencyRepository(User user, CurrencyType type, long amount)
    {
        User    = user;
        _type   = type;
        _amount = amount;
    }

    public long Key => User.SessionId;

    public User User { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            @"INSERT INTO t_user_currency (user_id, currency_type, amount)
              VALUES (@userId, @currencyType, @amount)
              ON CONFLICT (user_id, currency_type) DO UPDATE SET amount = excluded.amount;",
            new { userId = User.Uid, currencyType = (int)_type, amount = _amount });
    }

    public void Apply()
    {
    }
}
