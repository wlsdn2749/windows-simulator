using WSGameServer;

namespace WSGameServer;

/// <summary>
/// 작업속도 보정 합성 검증 — <b>속도 = 기본값 × (1 + Σ가산) × Π승산</b>.
///
/// 속도는 재화 생성량에 직접 곱해지므로 여기서 실수가 나면 경제 총량이 어긋난다.
/// 특히 <b>가산이 서로 곱해지지 않는지</b>(보정원이 늘 때 지수로 튀는 문제)와
/// <b>붙이는 순서가 결과를 바꾸지 않는지</b>를 집중적으로 본다.
/// </summary>
public class WorkSpeedTest
{
    /// <summary>적성 4의 속도(1800천분율 = 1.8배). 나누어떨어지지 않는 값이라 절단 실수가 드러난다.</summary>
    private const int Aptitude4 = 1800;

    private const int Base = WorkStationSlot.DefaultWorkSpeed;   // 1000 = 1.0배

    [Fact]
    public void 보정이_없으면_기본값_그대로다()
    {
        WorkSpeed.From(Aptitude4).Resolve().ShouldBe(Aptitude4);
    }

    [Fact]
    public void 가산은_기본값에_비율로_더한다()
    {
        // 특성 "작업속도 +25%" → 1800 × 1.25
        WorkSpeed.From(Aptitude4).Add(250).Resolve().ShouldBe(2250);
    }

    [Fact]
    public void 가산끼리는_합한_뒤_한_번만_적용된다()
    {
        // 특성 +25% + 부스트 +100% = +125% → 1800 × 2.25 = 4050
        // 서로 곱하면 1800 × 1.25 × 2.0 = 4500이 된다. 그 차이가 이 테스트의 전부다.
        WorkSpeed.From(Aptitude4)
            .Add(250)
            .Add(1000)
            .Resolve()
            .ShouldBe(4050);
    }

    [Fact]
    public void 같은_가산을_두_번_붙이면_정확히_두_배만_늘어난다()
    {
        // +25%가 둘이면 +50%다. 곱셈이면 +56.25%가 되어 보정원이 늘수록 값이 튄다.
        WorkSpeed.From(Base).Add(250).Add(250).Resolve().ShouldBe(1500);
    }

    [Fact]
    public void 승산은_결과_전체에_곱해진다()
    {
        WorkSpeed.From(Aptitude4).Multiply(2.0).Resolve().ShouldBe(3600);
        WorkSpeed.From(Aptitude4).Multiply(1500).Resolve().ShouldBe(2700);
    }

    [Fact]
    public void 승산끼리는_곱해진다()
    {
        WorkSpeed.From(Base).Multiply(2.0).Multiply(3.0).Resolve().ShouldBe(6000);
    }

    [Fact]
    public void 가산을_먼저_적용한_뒤_승산을_곱한다()
    {
        // 적성4(1800) + 특성 +25% + 부스트 +100% → 4050, 여기에 전역 배수 6.0
        WorkSpeed.From(Aptitude4)
            .Add(250)
            .Add(1000)
            .Multiply(6.0)
            .Resolve()
            .ShouldBe(24300);
    }

    [Fact]
    public void 붙이는_순서가_결과를_바꾸지_않는다()
    {
        // 가산은 합, 승산은 곱이므로 순서에 독립적이다.
        // 이 성질이 깨지면 "어느 시스템이 먼저 계산되는가"가 밸런스에 끼어든다.
        var forward = WorkSpeed.From(Aptitude4).Add(250).Multiply(6.0).Add(1000).Resolve();
        var reverse = WorkSpeed.From(Aptitude4).Add(1000).Add(250).Multiply(6.0).Resolve();

        forward.ShouldBe(reverse);
    }

    [Fact]
    public void 감소_보정은_음수_가산으로_표현한다()
    {
        WorkSpeed.From(Base).Add(-500).Resolve().ShouldBe(500);

        // +100%와 -50%가 함께 붙으면 +50%다
        WorkSpeed.From(Base).Add(1000).Add(-500).Resolve().ShouldBe(1500);
    }

    [Fact]
    public void 감소_보정이_겹쳐도_속도는_하한을_지킨다()
    {
        // -100%를 넘어가면 음수 속도가 되어 시간이 거꾸로 흐른다. 하한에서 막혀야 한다.
        WorkSpeed.From(Base).Add(-1000).Resolve().ShouldBe(WorkStationSlot.MinWorkSpeed);
        WorkSpeed.From(Base).Add(-3000).Resolve().ShouldBe(WorkStationSlot.MinWorkSpeed);
    }

    [Fact]
    public void 적성_0인_캐릭터는_보정을_붙여도_움직이지_않는다()
    {
        // 기본값이 0이면 가산은 0의 비율이라 늘어날 것이 없다.
        // "적성 0 = 그 산업을 다루지 못한다"가 보정으로 뚫리면 배치 제한이 무의미해진다.
        WorkSpeed.From(0).Add(5000).Multiply(6.0).Resolve()
            .ShouldBe(WorkStationSlot.MinWorkSpeed);
    }

    [Fact]
    public void 승산_0_이하는_무시한다()
    {
        // 속도 0은 "채취 정지"가 아니라 배치를 비우는 것으로 표현한다.
        // 배수 0을 허용하면 보정 하나가 슬롯을 조용히 멈춰 세운다.
        WorkSpeed.From(Aptitude4).Multiply(0.0).Resolve().ShouldBe(Aptitude4);
        WorkSpeed.From(Aptitude4).Multiply(-2.0).Resolve().ShouldBe(Aptitude4);
    }

    [Fact]
    public void 확정된_속도는_실효_주기로_이어진다()
    {
        // 계산식과 슬롯이 실제로 맞물리는지 확인한다. 1800 × 2.25 = 4050 → 30초 / 4.05
        var speed = WorkSpeed.From(Aptitude4).Add(250).Add(1000).Resolve();
        var slot  = new WorkStationSlot(
            slotIndex: 0,
            GameData.ItemType.Fishing,
            characterId: 100,
            new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc),
            speed);

        slot.CurrentWorkSpeed.ShouldBe(4050);
        slot.EffectiveCycle.TotalSeconds.ShouldBe(30.0 / 4.05, tolerance: 0.001);
    }
}
