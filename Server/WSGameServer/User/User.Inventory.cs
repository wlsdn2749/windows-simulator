using MikaProtocol;
using WSGameServer;

namespace WSGameServer;

public partial class User
{
    /// <summary>DB에서 읽은 인벤토리 Row를 도메인(Item)으로 변환해 적재한다(로그인 시 1회).</summary>
    private void LoadInventory(IReadOnlyList<InventoryRow> rows)
    {
        Inventory.Load(rows.Select(r => new Item(r.item_id, r.count)));
    }

    // 현재 인벤토리 전체 스냅샷을 클라이언트로 전송한다.
    public void SendInventory()
    {
        Send(new S_InventoryResponse { Items = Inventory.Snapshot() });
    }

    // 패킷 전송 없이 메모리 갱신 + DB 반영만 수행하고 변경분을 반환한다.
    // 호출자(AddItem, GachaService 등)가 응답 구성을 담당한다.
    public ItemChangeInfo GainItem(int itemId, int count)
    {
        var itemChangeInfo = Inventory.AddItem(itemId, count);

        PostDBTask<AddItemRepository>(new (this, itemChangeInfo));
        return itemChangeInfo;
    }

    public void AddItem(int itemId, int count)
    {
        var itemChangeInfo = GainItem(itemId, count);

        Send(new S_UpdateItemResponse { Result = EResultCode.Ok, ItemChangeInfos = [itemChangeInfo] });
    }
}