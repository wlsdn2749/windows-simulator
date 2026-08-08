using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 작업슬롯 한 칸. <b>산업 1개 + 캐릭터 1명</b>이 배치되면 독립적으로 채취를 돌린다.
///
/// <para>
/// <b>진행도는 접속 중에만 쌓이고 세션과 함께 사라진다.</b> 오프라인 진행이 폐지됐으므로
/// (게임기획코어 P2 재정의) 이 객체의 <see cref="LastTickAt"/>·<see cref="ProgressUnits"/>는
/// <b>세션 지역 상태</b>다. DB에 저장하지 않으며, 로그인할 때 항상 0에서 시작한다.
/// 슬롯 행이 DB에 남기는 것은 "어떤 산업에 누구를 배치했는가"라는 <b>설정</b>뿐이다.
/// </para>
///
/// <para>
/// <b>주기(period)가 아니라 속도(rate)를 가변으로 둔다.</b> 캐릭터 스탯·버프로 채취가 빨라지는데
/// <c>floor(경과 / 주기)</c> 형태를 유지하면 두 가지가 깨진다.
/// ① 세션 중 버프를 켜고 끄면 "이 구간은 몇 초 주기였나"를 표현할 수 없다.
/// ② 자투리가 초 단위라 주기가 바뀌면 그 자투리가 몇 판정어치인지 정해지지 않는다
///    (30초 주기의 15초는 0.5판정, 20초 주기에서는 0.75판정 — 주기를 흔들어 이득을 볼 수 있다).
/// 속도는 더할 수 있고 구간별로 쪼갤 수 있지만 주기는 그렇지 않다. 그래서 진행도의 단위를
/// 시간이 아닌 <b>작업량</b>으로 두고, 각 구간의 속도로 누적한다.
/// </para>
/// </summary>
public sealed class WorkStationSlot
{
    /// <summary>기준 채취 주기(초). 속도 배수가 1.0배일 때 판정 1회에 걸리는 시간이다.</summary>
    public const int BaseCycleSeconds = 30;

    /// <summary>
    /// 작업속도의 정수 표현 단위. <b>1000 = 1.0배(천분율)</b>.
    /// 이 프로젝트에서 "작업속도"라고 적힌 정수는 전부 이 단위다.
    /// </summary>
    public const int WorkSpeedScale = 1000;

    /// <summary>기준 작업속도(1.0배). 보정이 하나도 없을 때의 값.</summary>
    public const int DefaultWorkSpeed = WorkSpeedScale;

    /// <summary>
    /// 작업속도 하한. 0이면 <see cref="TimeUntilNextJudge"/>에서 0으로 나누게 되고,
    /// "채취 정지"는 속도 0이 아니라 <b>배치를 비우는 것</b>으로 표현한다.
    /// </summary>
    public const int MinWorkSpeed = 1;

    /// <summary>
    /// 판정 1회에 필요한 작업량. <c>기준주기(초) × 1000ms × 1000천분율</c>.
    ///
    /// <para>
    /// 이 단위를 쓰면 누적이 <c>경과ms × 속도천분율</c>로 <b>나눗셈 없이</b> 끝난다.
    /// 나눗셈을 끼우면 정산할 때마다 1 미만이 잘려 나가고, 푸시 주기가 짧을수록 손실이 커진다.
    /// </para>
    /// </summary>
    public const long JudgeCost = (long)BaseCycleSeconds * 1000 * WorkSpeedScale;

    /// <summary>판정 1회당 산출 개수. 현재 1개 고정이다(회당 산출 수치 미확정).</summary>
    public const int YieldPerJudge = 1;

    public WorkStationSlot(
        int slotIndex,
        ItemType industry,
        long characterId,
        DateTime startedAt,
        int currentWorkSpeed = DefaultWorkSpeed)
    {
        SlotIndex        = slotIndex;
        Industry         = industry;
        CharacterId      = characterId;
        LastTickAt       = startedAt;
        CurrentWorkSpeed = Math.Max(MinWorkSpeed, currentWorkSpeed);
    }

    public int SlotIndex { get; }

    /// <summary>지정된 산업. <see cref="ItemType.None"/>이면 미지정. <b>DB에 저장된다.</b></summary>
    public ItemType Industry { get; private set; }

    /// <summary>배치된 캐릭터. 0이면 비어 있다. <b>DB에 저장된다.</b></summary>
    public long CharacterId { get; private set; }

    /// <summary>
    /// 마지막으로 정산이 끝난 시각(UTC). 이 시각부터 다음 정산까지가 한 구간이다.
    /// <b>세션 지역 상태로 저장하지 않는다</b> — 로그인 시 접속 시각으로 시작한다.
    /// </summary>
    public DateTime LastTickAt { get; private set; }

    /// <summary>
    /// 판정에 못 미친 <b>남은 작업량</b>(0 이상 <see cref="JudgeCost"/> 미만).
    ///
    /// <para>
    /// 자투리를 초가 아니라 작업량으로 들고 있는 것이 요점이다. 초로 두면 속도가 바뀔 때
    /// "이 자투리는 몇 판정어치인가"가 달라져 재환산이 필요하고, 그 틈이 곧 익스플로잇이 된다.
    /// </para>
    ///
    /// <para><b>세션 지역 상태로 저장하지 않는다</b> — 접속이 끊기면 진행 조각은 버려진다.</para>
    /// </summary>
    public long ProgressUnits { get; private set; }

    /// <summary>
    /// <b>현재 작업속도</b>(천분율, 1000 = 1.0배). 적성 기본값에 가산·승산 보정을 모두 적용한
    /// <b>확정값</b>이며(<see cref="WorkSpeed"/>) <b>저장하지 않는다</b> — 접속할 때마다 다시 계산한다.
    ///
    /// <para>
    /// 적성별 기본값(<c>WorkSpeedTable.BaseWorkSpeedPermille</c>)과 혼동하지 않는다.
    /// 그쪽은 계산의 <b>입력</b>이고 이 값은 <b>결과</b>다.
    /// </para>
    /// </summary>
    public int CurrentWorkSpeed { get; private set; }

    /// <summary>이 슬롯의 실효 채취 주기. 표시·로그용이며 계산에는 쓰지 않는다.</summary>
    public TimeSpan EffectiveCycle
        => TimeSpan.FromMilliseconds((double)JudgeCost / CurrentWorkSpeed);

    /// <summary>산업이 지정되고 캐릭터가 배치돼야 돌아간다. 둘 중 하나라도 비면 채취하지 않는다.</summary>
    public bool IsActive => Industry != ItemType.None && CharacterId != 0;

    /// <summary>
    /// 배치를 바꾼다. <b>호출 전에 반드시 정산을 끝내야 한다</b> —
    /// 바꾸기 전 구간은 이전 설정으로 계산돼야 하기 때문이다.
    /// </summary>
    public void Assign(ItemType industry, long characterId, DateTime now)
    {
        Industry    = industry;
        CharacterId = characterId;

        // 배치를 바꾸면 진행 중이던 조각은 버린다.
        // 이월하면 "산업을 계속 갈아타며 조각을 모으는" 악용이 가능해진다.
        LastTickAt    = now;
        ProgressUnits = 0;
    }

    /// <summary>
    /// 채취 속도를 바꾼다(캐릭터 교체·스탯 상승·버프 적용/만료).
    ///
    /// <para>
    /// ⚠️ <b>호출 전에 반드시 정산해야 한다.</b> 정산하지 않고 바꾸면 아직 정산되지 않은
    /// 이전 구간까지 새 속도로 계산된다. 정산 → 속도 변경 순서를 지키면 각 구간이 항상
    /// 단일 속도로 계산되고 소급이 원천적으로 불가능해진다.
    /// </para>
    /// </summary>
    /// <returns>값이 실제로 바뀌었으면 true.</returns>
    public bool ApplyWorkSpeed(int currentWorkSpeed)
    {
        var clamped = Math.Max(MinWorkSpeed, currentWorkSpeed);
        if (clamped == CurrentWorkSpeed)
            return false;

        CurrentWorkSpeed = clamped;
        return true;
    }

    /// <summary>
    /// 지난 구간의 작업량을 누적하고, 완성된 판정 횟수를 꺼낸다.
    /// <b>못 채운 자투리는 <see cref="ProgressUnits"/>에 남아 이월된다</b>
    /// (29초 경과 후 정산해도 손해가 없다).
    /// </summary>
    /// <returns>정산할 판정 횟수. 비활성 슬롯이거나 아직 1회를 못 채웠으면 0.</returns>
    public int ConsumeJudgeCount(DateTime now)
    {
        if (!IsActive)
        {
            // 비어 있는 동안 시간이 쌓이면 캐릭터를 꽂는 순간 한꺼번에 터진다. 시계만 따라가게 둔다.
            LastTickAt = now;
            return 0;
        }

        var elapsedMs = (long)(now - LastTickAt).TotalMilliseconds;
        if (elapsedMs <= 0)
            return 0;

        // 구간 전체를 현재 속도로 누적한다. 속도가 바뀌는 지점에서는 호출자가 먼저 정산하므로
        // (ApplyWorkSpeed 주석 참조) 한 구간 안에서 속도는 항상 하나다.
        LastTickAt     = now;
        ProgressUnits += elapsedMs * CurrentWorkSpeed;

        var judgeCount = ProgressUnits / JudgeCost;
        if (judgeCount <= 0)
            return 0;

        ProgressUnits -= judgeCount * JudgeCost;
        return (int)judgeCount;
    }

    /// <summary>
    /// 다음 판정까지 남은 시간. 클라이언트 카운트다운을 맞출 때 쓴다.
    /// 비활성 슬롯은 판정이 오지 않으므로 <see cref="TimeSpan.Zero"/>를 돌려준다.
    /// </summary>
    public TimeSpan TimeUntilNextJudge(DateTime now)
    {
        if (!IsActive)
            return TimeSpan.Zero;

        // ProgressUnits는 마지막 정산 시점의 값이므로, 그 뒤로 흐른 시간을 얹어야 지금 값이 된다.
        var elapsedMs = (long)(now - LastTickAt).TotalMilliseconds;
        if (elapsedMs < 0)
            elapsedMs = 0;

        var remain = JudgeCost - (ProgressUnits + elapsedMs * CurrentWorkSpeed);
        return remain > 0
            ? TimeSpan.FromMilliseconds((double)remain / CurrentWorkSpeed)
            : TimeSpan.Zero;
    }

    public WorkStationSlotInfo ToInfo() => new()
    {
        SlotIndex        = SlotIndex,
        Industry         = (byte)Industry,
        CharacterId      = CharacterId,
        LastTickAtUnix   = new DateTimeOffset(LastTickAt, TimeSpan.Zero).ToUnixTimeSeconds(),
        ProgressUnits    = ProgressUnits,
        CurrentWorkSpeed = CurrentWorkSpeed,
        JudgeCostUnits   = JudgeCost,
    };
}
