using MikaProtocol;

namespace WSGameServer;

public class AddItemRepository : IRepository
{
    // DBExecutor 파티션 키 — 같은 유저의 DB 작업(로그인 로드 등)과 직렬로 처리돼야 한다.
    public long Key => User.SessionId;

    public User User { get; init; }
    public ItemChangeInfo ItemChangeInfo { get; init; }

    public AddItemRepository(User user, ItemChangeInfo itemChangeInfo)
    {
        User = user;
        ItemChangeInfo = itemChangeInfo;
    }

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            @"INSERT INTO t_user_inventory (user_id, item_id, count)
              VALUES (@userId, @itemId, @count)
              ON CONFLICT (user_id, item_id) DO UPDATE SET count = excluded.count;",
            new { userId = User.Uid, itemId = ItemChangeInfo.ItemId, count = ItemChangeInfo.Count });
    }

    public void Apply()
    {

    }
}
