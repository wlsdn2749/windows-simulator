using WSGameServer;

namespace WSGameServer;

/// <summary>
/// <see cref="WeightedPicker{T}"/> 검증.
///
/// 추첨 결과는 난수라 "돌려 보고 대충 맞으면 통과" 식으로 짜기 쉬운데, 그러면
/// 이진 탐색의 <b>구간 경계 실수(off-by-one)</b>를 놓친다. 경계는 난수를 목으로 고정해
/// 결정적으로 확인하고, 분포는 별도로 큰 표본에서 본다.
/// </summary>
public class WeightedPickerTest
{
    /// <summary>테스트용 후보. 실제 테이블 Row 대신 쓴다 — 추첨기는 항목의 내용을 모른다.</summary>
    private sealed record Entry(string Name, int Weight);

    /// <summary>가중치 790/150/50/10 → 누적 [790, 940, 990, 1000].</summary>
    private static WeightedPicker<Entry> BuildPicker() => WeightedPicker.From(
        new[]
        {
            new Entry("Common",    790),
            new Entry("Rare",      150),
            new Entry("Epic",       50),
            new Entry("Legendary",  10),
        },
        e => e.Weight);

    /// <summary>지정한 값만 내놓는 난수원. 구간 경계를 정확히 겨냥할 때 쓴다.</summary>
    private static Random FixedRoll(int value)
    {
        var random = new Mock<Random>();
        random.Setup(r => r.Next(It.IsAny<int>())).Returns(value);
        return random.Object;
    }

    [Fact]
    public void 전체_가중치는_모든_항목의_합이다()
    {
        var picker = BuildPicker();

        picker.TotalWeight.ShouldBe(1000);
        picker.Count.ShouldBe(4);
    }

    [Theory]
    // 각 구간의 시작과 끝을 정확히 겨냥한다. 누적 = [790, 940, 990, 1000]
    [InlineData(0,   "Common")]
    [InlineData(789, "Common")]      // 첫 구간의 마지막
    [InlineData(790, "Rare")]        // 두 번째 구간의 시작 — 여기서 off-by-one이 드러난다
    [InlineData(939, "Rare")]
    [InlineData(940, "Epic")]
    [InlineData(989, "Epic")]
    [InlineData(990, "Legendary")]
    [InlineData(999, "Legendary")]   // 마지막 구간의 끝 = TotalWeight - 1
    public void 난수값이_속한_가중치_구간의_항목을_고른다(int roll, string expected)
    {
        var picker = BuildPicker();

        picker.Pick(FixedRoll(roll)).Name.ShouldBe(expected);
    }

    [Fact]
    public void 난수는_전체_가중치_범위로_요청된다()
    {
        var random = new Mock<Random>();
        random.Setup(r => r.Next(It.IsAny<int>())).Returns(0);

        BuildPicker().Pick(random.Object);

        // [0, TotalWeight) 로 요청해야 한다. TotalWeight를 넘겨 뽑으면 마지막 항목 확률이 어긋난다.
        random.Verify(r => r.Next(1000), Times.Once);
    }

    [Fact]
    public void 가중치가_0인_항목은_후보에서_빠진다()
    {
        var picker = WeightedPicker.From(
            new[]
            {
                new Entry("살아있음", 100),
                new Entry("막아둠",     0),   // 기획이 당분간 막아 둔 항목
            },
            e => e.Weight);

        picker.Count.ShouldBe(1);
        picker.TotalWeight.ShouldBe(100);
        picker.Items.ShouldNotContain(e => e.Name == "막아둠");
    }

    [Fact]
    public void 가중치가_음수면_데이터_오류로_막는다()
    {
        Should.Throw<ArgumentException>(() => WeightedPicker.From(
            new[] { new Entry("잘못됨", -1) }, e => e.Weight));
    }

    [Fact]
    public void 뽑을_후보가_없으면_예외를_던진다()
    {
        Should.Throw<ArgumentException>(() => WeightedPicker.From(
            Array.Empty<Entry>(), e => e.Weight));

        // 항목은 있지만 전부 0이면 뽑을 게 없는 것과 같다.
        Should.Throw<ArgumentException>(() => WeightedPicker.From(
            new[] { new Entry("막아둠", 0) }, e => e.Weight));
    }

    [Fact]
    public void 후보가_하나뿐이면_항상_그것을_고른다()
    {
        var picker = WeightedPicker.From(new[] { new Entry("유일", 7) }, e => e.Weight);

        picker.Pick(FixedRoll(0)).Name.ShouldBe("유일");
        picker.Pick(FixedRoll(6)).Name.ShouldBe("유일");
    }

    [Fact]
    public void 항목별_확률을_계산한다()
    {
        var picker = BuildPicker();

        picker.ProbabilityOf(0).ShouldBe(0.79);
        picker.ProbabilityOf(1).ShouldBe(0.15);
        picker.ProbabilityOf(2).ShouldBe(0.05);
        picker.ProbabilityOf(3).ShouldBe(0.01);

        // 확률의 합은 1이다(부동소수 오차 허용).
        Enumerable.Range(0, picker.Count).Sum(picker.ProbabilityOf).ShouldBe(1.0, 1e-9);
    }

    [Fact]
    public void 같은_시드면_같은_결과가_나온다()
    {
        var picker = BuildPicker();

        var first  = picker.PickMany(100, new Random(1234));
        var second = picker.PickMany(100, new Random(1234));

        second.ShouldBe(first);   // 정산 재현·디버깅이 가능해야 한다
    }

    [Fact]
    public void 충분히_많이_뽑으면_가중치_비율에_수렴한다()
    {
        const int trials = 200_000;

        var picker = BuildPicker();
        var counts = new Dictionary<string, int>();

        // 콜백 버전 — 오프라인 정산이 쓰는 경로다. 중간 리스트를 만들지 않는다.
        picker.PickMany(trials, e => counts[e.Name] = counts.GetValueOrDefault(e.Name) + 1,
                        new Random(20260729));

        // 희귀할수록 표본 수가 적어 흔들림이 크므로 허용 오차를 넓게 잡는다.
        // 시드가 고정이라 결과는 결정적이다 — 오차는 "구현이 바뀌어도 통과할 여유"일 뿐이다.
        AssertNear(counts["Common"],    0.79, trials, tolerance: 0.03);
        AssertNear(counts["Rare"],      0.15, trials, tolerance: 0.05);
        AssertNear(counts["Epic"],      0.05, trials, tolerance: 0.08);
        AssertNear(counts["Legendary"], 0.01, trials, tolerance: 0.15);
    }

    /// <summary>관측 횟수가 기대 확률 × 시행 수에서 허용 오차 안에 있는지 본다.</summary>
    private static void AssertNear(int observed, double probability, int trials, double tolerance)
    {
        var expected = probability * trials;
        observed.ShouldBeInRange(
            (int)(expected * (1 - tolerance)),
            (int)(expected * (1 + tolerance)));
    }

    [Fact]
    public void PickMany는_요청한_횟수만큼_뽑는다()
    {
        var picker = BuildPicker();

        picker.PickMany(0).ShouldBeEmpty();
        picker.PickMany(5).Count.ShouldBe(5);

        Should.Throw<ArgumentOutOfRangeException>(() => picker.PickMany(-1));
    }

    [Fact]
    public void 그룹_축이_있는_테이블을_그룹별_추첨기로_나눈다()
    {
        // 드롭 시트의 실제 모양: (그룹 축, 아이템, 가중치). 그룹 축은 키가 아니라 일반 컬럼이다.
        var rows = new[]
        {
            (SpotTID: 1, Item: "붕어",     Weight: 70),
            (SpotTID: 1, Item: "잉어",     Weight: 30),
            (SpotTID: 2, Item: "심해어",   Weight: 50),
        };

        var bySpot = WeightedPicker.GroupBy(rows, r => r.SpotTID, r => r.Weight);

        bySpot.Count.ShouldBe(2);
        bySpot[1].TotalWeight.ShouldBe(100);
        bySpot[2].TotalWeight.ShouldBe(50);
        bySpot[2].Pick(FixedRoll(0)).Item.ShouldBe("심해어");
    }
}
