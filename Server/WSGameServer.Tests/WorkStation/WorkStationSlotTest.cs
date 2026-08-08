using GameData;
using WSGameServer;

namespace WSGameServer;

/// <summary>
/// 작업슬롯 정산 검증.
///
/// 방치형의 진행은 <b>시각의 함수</b>라 여기서 실수가 나면 재화가 새거나 사라진다.
/// 특히 <b>자투리 이월</b>, <b>속도가 바뀔 때의 자투리 보존</b>, <b>비활성 슬롯의 시간 누적</b>을
/// 집중적으로 본다.
/// </summary>
public class WorkStationSlotTest
{
    private static readonly DateTime Base = new(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

    private static WorkStationSlot ActiveSlot(
        DateTime startedAt,
        int currentWorkSpeed = WorkStationSlot.DefaultWorkSpeed)
        => new(slotIndex: 0, ItemType.Fishing, characterId: 100, startedAt, currentWorkSpeed);

    [Fact]
    public void 주기가_안_찼으면_판정하지_않는다()
    {
        var slot = ActiveSlot(Base);

        slot.ConsumeJudgeCount(Base.AddSeconds(29)).ShouldBe(0);

        // 판정은 없지만 29초어치 작업량은 남아 있어야 한다(이월).
        slot.ProgressUnits.ShouldBe(29L * 1000 * WorkStationSlot.DefaultWorkSpeed);
    }

    [Theory]
    [InlineData(30, 1)]
    [InlineData(59, 1)]
    [InlineData(60, 2)]
    [InlineData(90, 3)]
    [InlineData(3600, 120)]        // 1시간
    [InlineData(86400, 2880)]      // 24시간 = 기준 속도의 하루 판정 수
    public void 경과_시간을_주기로_나눈_만큼_판정한다(int elapsedSeconds, int expected)
    {
        ActiveSlot(Base).ConsumeJudgeCount(Base.AddSeconds(elapsedSeconds)).ShouldBe(expected);
    }

    [Fact]
    public void 자투리_시간은_다음으로_이월된다()
    {
        var slot = ActiveSlot(Base);

        // 89초 = 2회 판정 + 29초 남음
        slot.ConsumeJudgeCount(Base.AddSeconds(89)).ShouldBe(2);
        slot.ProgressUnits.ShouldBe(29L * 1000 * WorkStationSlot.DefaultWorkSpeed);

        // 남은 29초 + 1초 = 30초 → 곧바로 1회 더
        slot.ConsumeJudgeCount(Base.AddSeconds(90)).ShouldBe(1);
        slot.ProgressUnits.ShouldBe(0);
    }

    [Fact]
    public void 여러_번_나눠_정산해도_총_판정_수는_같다()
    {
        // 1초마다 푸시하든 5분 만에 한 번 정산하든 결과가 같아야 한다.
        // 누적에 나눗셈이 없으므로 쪼개도 잘려 나가는 값이 없다.
        var stepwise = ActiveSlot(Base);
        var total = 0;
        for (var i = 1; i <= 300; i++)
            total += stepwise.ConsumeJudgeCount(Base.AddSeconds(i));

        var atOnce = ActiveSlot(Base).ConsumeJudgeCount(Base.AddSeconds(300));

        total.ShouldBe(atOnce);
        total.ShouldBe(10);
    }

    [Fact]
    public void 나누어떨어지지_않는_속도에서도_쪼갠_정산이_손해가_없다()
    {
        // 1.333배. 자투리가 매번 남는 값이라 나눗셈이 끼어 있으면 여기서 손실이 드러난다.
        const int speed = 1333;

        var stepwise = ActiveSlot(Base, speed);
        var total = 0;
        for (var i = 1; i <= 300; i++)
            total += stepwise.ConsumeJudgeCount(Base.AddSeconds(i));

        var atOnce = ActiveSlot(Base, speed).ConsumeJudgeCount(Base.AddSeconds(300));

        total.ShouldBe(atOnce);
    }

    // ─────────────────────────── 속도 ───────────────────────────

    [Fact]
    public void 속도가_2배면_같은_시간에_판정도_2배다()
    {
        var normal = ActiveSlot(Base);
        var fast   = ActiveSlot(Base, WorkStationSlot.DefaultWorkSpeed * 2);

        normal.ConsumeJudgeCount(Base.AddSeconds(300)).ShouldBe(10);
        fast.ConsumeJudgeCount(Base.AddSeconds(300)).ShouldBe(20);
    }

    [Fact]
    public void 속도가_바뀌어도_진행_중이던_자투리는_그대로_유지된다()
    {
        // 이것이 주기가 아니라 속도를 가변으로 두는 이유다.
        // 자투리를 초로 들고 있으면 "15초 남음"이 주기에 따라 0.5판정도 0.75판정도 되지만,
        // 작업량으로 들고 있으면 재환산이 필요 없다.
        var slot = ActiveSlot(Base);

        slot.ConsumeJudgeCount(Base.AddSeconds(15)).ShouldBe(0);   // 정확히 절반
        slot.ProgressUnits.ShouldBe(WorkStationSlot.JudgeCost / 2);

        slot.ApplyWorkSpeed(WorkStationSlot.DefaultWorkSpeed * 2).ShouldBeTrue();
        slot.ProgressUnits.ShouldBe(WorkStationSlot.JudgeCost / 2);  // 속도를 바꿔도 그대로

        // 남은 절반을 2배 속도로 채우면 7.5초. 7.4초에는 아직 아니다.
        slot.ConsumeJudgeCount(Base.AddSeconds(15).AddMilliseconds(7400)).ShouldBe(0);
        slot.ConsumeJudgeCount(Base.AddSeconds(15).AddMilliseconds(7500)).ShouldBe(1);
    }

    [Fact]
    public void 속도_변경은_이미_정산된_이전_구간에_소급되지_않는다()
    {
        var slot = ActiveSlot(Base);

        // 정산 → 속도 변경 순서를 지키면 이전 구간은 이전 속도로 확정된다.
        slot.ConsumeJudgeCount(Base.AddSeconds(60)).ShouldBe(2);
        slot.ApplyWorkSpeed(WorkStationSlot.DefaultWorkSpeed * 4);

        // 이후 30초는 4배 속도로만 계산된다.
        slot.ConsumeJudgeCount(Base.AddSeconds(90)).ShouldBe(4);
    }

    [Fact]
    public void 속도는_0_이하로_내려가지_않는다()
    {
        // 0이면 남은 시간 계산에서 0으로 나눈다. "정지"는 배치를 비우는 것으로 표현한다.
        var slot = ActiveSlot(Base);

        slot.ApplyWorkSpeed(0);
        slot.CurrentWorkSpeed.ShouldBe(WorkStationSlot.MinWorkSpeed);

        slot.ApplyWorkSpeed(-500);
        slot.CurrentWorkSpeed.ShouldBe(WorkStationSlot.MinWorkSpeed);
    }

    [Fact]
    public void 같은_속도로_바꾸면_변경으로_보지_않는다()
    {
        var slot = ActiveSlot(Base);

        slot.ApplyWorkSpeed(WorkStationSlot.DefaultWorkSpeed).ShouldBeFalse();
    }

    [Fact]
    public void 실효_주기는_속도에_반비례한다()
    {
        ActiveSlot(Base).EffectiveCycle.ShouldBe(TimeSpan.FromSeconds(30));
        ActiveSlot(Base, WorkStationSlot.DefaultWorkSpeed * 2).EffectiveCycle
            .ShouldBe(TimeSpan.FromSeconds(15));
    }

    // ─────────────────────── 비활성·경계 ───────────────────────

    [Fact]
    public void 캐릭터가_없으면_시간이_쌓이지_않는다()
    {
        var empty = new WorkStationSlot(0, ItemType.Fishing, characterId: 0, Base);

        empty.IsActive.ShouldBeFalse();

        // 하루가 지나도 판정은 0이고, LastTickAt은 현재를 따라간다.
        // 따라가지 않으면 캐릭터를 꽂는 순간 밀린 하루치가 한꺼번에 터진다.
        var now = Base.AddDays(1);
        empty.ConsumeJudgeCount(now).ShouldBe(0);
        empty.LastTickAt.ShouldBe(now);
        empty.ProgressUnits.ShouldBe(0);
    }

    [Fact]
    public void 산업이_미지정이면_돌지_않는다()
    {
        var slot = new WorkStationSlot(0, ItemType.None, characterId: 100, Base);

        slot.IsActive.ShouldBeFalse();
        slot.ConsumeJudgeCount(Base.AddHours(1)).ShouldBe(0);
    }

    [Fact]
    public void 배치를_바꾸면_진행_중이던_조각은_버린다()
    {
        var slot = ActiveSlot(Base);
        var now  = Base.AddSeconds(29);   // 29초 진행 중

        slot.Assign(ItemType.Mining, characterId: 200, now);

        slot.Industry.ShouldBe(ItemType.Mining);
        slot.CharacterId.ShouldBe(200);
        slot.LastTickAt.ShouldBe(now);
        slot.ProgressUnits.ShouldBe(0);

        // 이월하면 산업을 계속 갈아타며 29초 조각을 모으는 악용이 가능해진다.
        slot.ConsumeJudgeCount(now.AddSeconds(29)).ShouldBe(0);
    }

    [Fact]
    public void 시각이_거꾸로_가도_판정하지_않는다()
    {
        var slot = ActiveSlot(Base);

        slot.ConsumeJudgeCount(Base.AddSeconds(-100)).ShouldBe(0);
        slot.LastTickAt.ShouldBe(Base);
        slot.ProgressUnits.ShouldBe(0);
    }

    [Fact]
    public void 다음_판정까지_남은_시간을_알려준다()
    {
        var slot = ActiveSlot(Base);

        slot.TimeUntilNextJudge(Base).ShouldBe(TimeSpan.FromSeconds(30));
        slot.TimeUntilNextJudge(Base.AddSeconds(29)).ShouldBe(TimeSpan.FromSeconds(1));
        slot.TimeUntilNextJudge(Base.AddSeconds(45)).ShouldBe(TimeSpan.Zero);   // 음수가 되지 않는다
    }

    [Fact]
    public void 남은_시간은_속도를_반영한다()
    {
        var fast = ActiveSlot(Base, WorkStationSlot.DefaultWorkSpeed * 2);

        fast.TimeUntilNextJudge(Base).ShouldBe(TimeSpan.FromSeconds(15));
        fast.TimeUntilNextJudge(Base.AddSeconds(10)).ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void 비활성_슬롯은_남은_시간이_없다()
    {
        var empty = new WorkStationSlot(0, ItemType.Fishing, characterId: 0, Base);

        empty.TimeUntilNextJudge(Base.AddSeconds(10)).ShouldBe(TimeSpan.Zero);
    }

    // ─────────────────────────── WorkStation ───────────────────────────

    /// <summary>낚시 테이블 하나만 등록된 독립 카탈로그.</summary>
    private static DropTableCatalog BuildCatalog()
    {
        var catalog = new DropTableCatalog();
        catalog.Register(ItemType.Fishing, DropTable.From(
            "FishingBasicTable",
            new[] { (ItemTID: 1001, Weight: 100) },
            r => r.ItemTID, r => r.Weight));
        return catalog;
    }

    [Fact]
    public void 슬롯마다_독립적으로_정산한다()
    {
        var station = new WorkStation();
        station.Load(new[]
        {
            new WorkStationSlot(0, ItemType.Fishing, 100, Base),                  // 5분 경과 → 10회
            new WorkStationSlot(1, ItemType.Fishing, 200, Base.AddSeconds(240)),  // 1분 경과 → 2회
        });

        var harvests = station.Settle(Base.AddSeconds(300), BuildCatalog());

        harvests.Count.ShouldBe(2);
        harvests.Single(h => h.SlotIndex == 0).JudgeCount.ShouldBe(10);
        harvests.Single(h => h.SlotIndex == 1).JudgeCount.ShouldBe(2);

        // 회당 산출 1개 고정 → 판정 수와 획득 수가 같다
        harvests.Single(h => h.SlotIndex == 0).Gained[1001].ShouldBe(10);
    }

    [Fact]
    public void 슬롯마다_속도가_달라도_서로_간섭하지_않는다()
    {
        var station = new WorkStation();
        station.Load(new[]
        {
            new WorkStationSlot(0, ItemType.Fishing, 100, Base, WorkStationSlot.DefaultWorkSpeed),
            new WorkStationSlot(1, ItemType.Fishing, 200, Base, WorkStationSlot.DefaultWorkSpeed * 3),
        });

        var harvests = station.Settle(Base.AddSeconds(300), BuildCatalog());

        harvests.Single(h => h.SlotIndex == 0).JudgeCount.ShouldBe(10);
        harvests.Single(h => h.SlotIndex == 1).JudgeCount.ShouldBe(30);
    }

    [Fact]
    public void 수확이_없는_슬롯은_결과에_담기지_않는다()
    {
        var station = new WorkStation();
        station.Load(new[]
        {
            new WorkStationSlot(0, ItemType.Fishing, 100, Base),
            new WorkStationSlot(1, ItemType.Fishing, 0,   Base),   // 캐릭터 없음
        });

        var harvests = station.Settle(Base.AddSeconds(60), BuildCatalog());

        harvests.Count.ShouldBe(1);
        harvests[0].SlotIndex.ShouldBe(0);
    }

    [Fact]
    public void 드롭_테이블이_없는_산업은_건너뛴다()
    {
        var station = new WorkStation();
        station.Load(new[]
        {
            new WorkStationSlot(0, ItemType.Fishing, 100, Base),
            new WorkStationSlot(1, ItemType.Mining,  200, Base),   // 시트 미작성
        });

        // 예외로 죽지 않고, 등록된 슬롯만 정산된다.
        var harvests = station.Settle(Base.AddSeconds(60), BuildCatalog());

        harvests.Count.ShouldBe(1);
        harvests[0].SlotIndex.ShouldBe(0);
    }

    [Fact]
    public void 슬롯을_해금하면_현재_시각부터_시작한다()
    {
        var station = new WorkStation();

        var slot = station.Unlock(3, Base);

        slot.SlotIndex.ShouldBe(3);
        slot.LastTickAt.ShouldBe(Base);
        slot.IsActive.ShouldBeFalse();   // 산업·캐릭터가 아직 없다
        station.Count.ShouldBe(1);

        // 이미 있으면 덮어쓰지 않는다 — 진행도가 날아가면 안 된다
        station.Unlock(3, Base.AddHours(1)).LastTickAt.ShouldBe(Base);
    }
}
