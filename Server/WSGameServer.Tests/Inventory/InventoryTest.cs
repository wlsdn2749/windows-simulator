using WSGameServer;

namespace WSGameServer.Tests.InventoryTests;

/// <summary>
/// Inventory.AddItem이 반환하는 ItemChangeInfo.Count의 의미를 지킨다 —
/// <b>델타가 아니라 갱신 후 누적 총량</b>이다.
/// 모든 지급 경로(AddItem·채취 정산·가챠)가 이 값을 그대로 패킷에 실어 보내고
/// 클라이언트는 덮어쓰기로 반영하므로, 이 규칙이 깨지면 클라 보유량이
/// 마지막 지급분으로 깎인다 (깃허브 이슈 #8).
/// </summary>
public class InventoryTest
{
    [Fact]
    public void 같은_아이템을_거듭_얻으면_Count는_누적_총량이다()
    {
        var inventory = new Inventory();

        inventory.AddItem(101, 3);
        var change = inventory.AddItem(101, 4);

        // 3 + 4 = 7. 델타(4)를 반환하면 클라 덮어쓰기에서 보유량이 4로 깎인다.
        change.Count.ShouldBe(7);
    }

    [Fact]
    public void 이미_있는_아이템의_변경_종류는_Update다()
    {
        var inventory = new Inventory();

        inventory.AddItem(101, 3);
        var change = inventory.AddItem(101, 4);

        change.Kind.ShouldBe(MikaProtocol.EItemChangeKind.Update);
    }

    [Fact]
    public void 처음_얻는_아이템은_지급량이_곧_총량이고_종류는_Add다()
    {
        var inventory = new Inventory();

        var change = inventory.AddItem(202, 5);

        change.Count.ShouldBe(5);
        change.Kind.ShouldBe(MikaProtocol.EItemChangeKind.Add);
    }
}
