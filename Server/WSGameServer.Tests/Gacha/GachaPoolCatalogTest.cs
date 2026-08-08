namespace WSGameServer;

/// <summary>
/// <see cref="GachaPoolCatalog"/> 검증. 추첨의 정확성은 <see cref="WeightedPickerTest"/>가 담당하고,
/// 여기서는 <b>Row → GachaEntry 정규화</b>와 <b>GachaId별 풀 분리</b>를 본다.
/// </summary>
public class GachaPoolCatalogTest
{
    /// <summary>실제 GachaTable 시트와 같은 모양의 테스트용 Row.</summary>
    private sealed record Row(int GachaTID, int GachaId, int ItemTID, int Count, int Weight);

    private static readonly Row[] Rows =
    {
        new(1001, 1, 100001, 1, 700),
        new(1002, 1, 100002, 3, 300),
        new(2001, 2, 100003, 1, 100),
    };

    private static GachaPoolCatalog BuildCatalog()
    {
        // Singleton이지만 new()가 가능하므로 테스트마다 독립 인스턴스를 쓴다.
        // Instance를 공유하면 테스트 순서에 따라 등록 상태가 새어 나간다.
        var catalog = new GachaPoolCatalog();
        catalog.Load(Rows, r => r.GachaId, r => r.ItemTID, r => r.Count, r => r.Weight);
        return catalog;
    }

    /// <summary>지정한 값만 내놓는 난수원.</summary>
    private static Random FixedRoll(int value)
    {
        var random = new Mock<Random>();
        random.Setup(r => r.Next(It.IsAny<int>())).Returns(value);
        return random.Object;
    }

    [Fact]
    public void GachaId별로_풀이_나뉜다()
    {
        var catalog = BuildCatalog();

        catalog.Count.ShouldBe(2);

        catalog.TryGet(1, out var pool1).ShouldBeTrue();
        catalog.TryGet(2, out var pool2).ShouldBeTrue();

        // 풀 1에는 두 항목(가중치 합 1000), 풀 2에는 한 항목만 들어간다.
        pool1.TotalWeight.ShouldBe(1000);
        pool2.TotalWeight.ShouldBe(100);
        pool2.Pick(FixedRoll(0)).ItemTID.ShouldBe(100003);
    }

    [Fact]
    public void 추첨_결과는_ItemTID와_Count를_그대로_담는다()
    {
        var catalog = BuildCatalog();
        catalog.TryGet(1, out var pool);

        // GachaTID는 시트의 키일 뿐 추첨 결과와 무관하다 — 결과는 (ItemTID, Count)다.
        pool.Pick(FixedRoll(0)).ShouldBe(new GachaEntry(1, 100001, 1, 700));
        pool.Pick(FixedRoll(699)).ItemTID.ShouldBe(100001);

        var rare = pool.Pick(FixedRoll(700));
        rare.ItemTID.ShouldBe(100002);
        rare.Count.ShouldBe(3);
    }

    [Fact]
    public void 없는_풀은_TryGet이_false다()
    {
        var catalog = BuildCatalog();

        catalog.TryGet(999, out _).ShouldBeFalse();
    }

    [Fact]
    public void 다시_로드하면_기존_풀을_전부_교체한다()
    {
        var catalog = BuildCatalog();

        // 풀 2가 사라진 데이터로 재로드 — 남아 있으면 이전 확률표가 유령으로 산다.
        var replaced = new[] { new Row(1001, 1, 100001, 1, 100) };
        catalog.Load(replaced, r => r.GachaId, r => r.ItemTID, r => r.Count, r => r.Weight);

        catalog.Count.ShouldBe(1);
        catalog.TryGet(2, out _).ShouldBeFalse();
    }
}
