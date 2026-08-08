using MikaProtocol;

namespace WSGameServer;

public sealed class Inventory
{
    private Dictionary<int, Item> _items = new();

    // 로그인 시 아이템을 적재한다. Row → Item 변환은 호출자(User.OnLoginDataLoaded)가 끝냈다 —
    // 인벤토리는 Repository의 Row도 네트워크 DTO도 모른다.
    public void Load(IEnumerable<Item> items)
    {
        _items = items.ToDictionary(item => item.Id);
    }

    // 현재 인벤토리 전체를 네트워크 전송용 ItemInfo 목록으로 변환한다.
    public List<ItemInfo> Snapshot()
        => _items.Values
            .Select(item => new ItemInfo { ItemId = item.Id, Count = item.Count })
            .ToList();
    
    public ItemChangeInfo AddItem(int itemId, int count)
    {
        if (_items.TryGetValue(itemId, out var item))
        {
            item.Count += count;
            return new ItemChangeInfo
            {
                ItemId = itemId, Count = item.Count, Kind = EItemChangeKind.Update
            };
        }

        var added = new Item(itemId, count);
        _items[itemId] = added;
        return new ItemChangeInfo
        {
            ItemId = itemId, Count = added.Count, Kind = EItemChangeKind.Add
        };
    }
}