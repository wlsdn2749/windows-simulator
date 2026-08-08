namespace WSGameServer;

/// <summary>
/// 작업속도 보정치를 <b>가산(add)과 승산(mul)으로 분류해 모아 두는</b> 누산기.
/// <b>모으는 것과 적용하는 것을 분리한다</b> — 보정원이 몇 개든 실제 계산은 <see cref="Resolve"/>에서 한 번뿐이다.
///
/// <para>
/// <b>속도 = 적성기본값 × (1 + Σ가산) × Π승산</b>
/// </para>
///
/// <para>
/// <b>기본값은 적성이다.</b> 캐릭터의 해당 산업 적성을 <c>WorkSpeedTable</c>로 변환한 값이 출발점이고,
/// 나머지 보정은 전부 그 기본값을 기준으로 말한다.
/// </para>
///
/// <para>
/// <b>대부분의 보정은 가산이다.</b> "작업속도 +25%"는 <b>기본값의 25%</b>를 더한다는 뜻이며,
/// 다른 보정이 이미 붙어 있어도 그 몫은 달라지지 않는다. 특성 패시브·액티브 부스트·장비가 모두 여기 들어간다.
/// 이것들을 서로 곱해 버리면 보정원이 늘어날 때마다 값이 지수로 튄다 —
/// <c>+25%</c> 둘이 <c>+56%</c>가 되고, 항을 하나 추가할 때마다 밸런스를 전부 다시 잡아야 한다.
/// 가산으로 두면 <b>보정원의 개수와 무관하게</b> 각 보정의 몫이 적힌 그대로 유지된다.
/// </para>
///
/// <para>
/// <b>승산은 "총 작업속도 증가"처럼 결과 전체에 배수로 얹히는 소수의 보정</b>이다.
/// 현재는 서버 전역 배수(<c>Global.GatherSpeedMultiplier</c>) 하나뿐이며, 앞으로도 드물 것이다.
/// 승산은 가산 전체를 함께 부풀리므로 밸런스에 미치는 영향이 가산과 비교가 안 된다 —
/// 새 보정을 넣을 때 <b>기본적으로 가산이고, 승산은 의도적으로 고를 때만</b> 쓴다.
/// </para>
///
/// <para>
/// 값 타입이며 각 메서드는 새 값을 돌려준다. 누적 순서가 결과를 바꾸지 않으므로
/// (가산은 합, 승산은 곱) 호출 순서를 신경 쓸 필요가 없다.
/// </para>
/// </summary>
public readonly struct WorkSpeed
{
    /// <summary>적성 기본 작업속도(천분율)로 누산을 시작한다.</summary>
    public static WorkSpeed From(int baseWorkSpeed) => new(baseWorkSpeed, 0, 0);

    private WorkSpeed(int baseWorkSpeed, int addPermille, double mulRate)
    {
        _baseWorkSpeed = baseWorkSpeed;
        _addPermille  = addPermille;
        _mulRate      = mulRate;
    }

    private readonly int _baseWorkSpeed;

    /// <summary>가산 보정의 <b>합</b>(천분율). <c>+25%</c> → <c>250</c>. 기본값에 대한 비율이다.</summary>
    private readonly int _addPermille;

    /// <summary>
    /// 승산 보정의 <b>곱</b>. <b>0은 "승산 없음"(×1.0)을 뜻한다</b> —
    /// <see cref="Multiply(double)"/>가 0 이하를 거부하므로 0이 실제 배수로 들어올 일이 없다.
    /// </summary>
    private readonly double _mulRate;

    /// <summary>
    /// <b>가산</b> 보정을 더한다(천분율). <c>250</c>이면 기본값의 <c>+25%</c>.
    /// 특성·부스트·장비가 여기로 온다. 감소 보정은 음수로 넣는다.
    /// </summary>
    public WorkSpeed Add(int ratePermille)
        => new(_baseWorkSpeed, _addPermille + ratePermille, _mulRate);

    /// <summary><b>승산</b> 보정을 곱한다(천분율). <c>1500</c>이면 결과 전체에 <c>×1.5</c>.</summary>
    public WorkSpeed Multiply(int ratePermille)
        => Multiply((double)ratePermille / WorkStationSlot.WorkSpeedScale);

    /// <summary>
    /// <b>승산</b> 보정을 곱한다(배수). "총 작업속도 증가"처럼 결과 전체에 얹히는 보정에만 쓴다.
    ///
    /// <para>
    /// 0 이하는 무시한다. 속도 0은 "채취 정지"가 아니라 <b>배치를 비우는 것</b>으로 표현하며
    /// (<c>WorkStationSlot.MinWorkSpeed</c> 참조), 배수 0을 허용하면
    /// 보정 하나가 슬롯을 조용히 멈춰 세울 수 있다.
    /// </para>
    /// </summary>
    public WorkSpeed Multiply(double rate)
    {
        if (rate <= 0)
            return this;

        return new(_baseWorkSpeed, _addPermille, _mulRate == 0 ? rate : _mulRate * rate);
    }

    /// <summary>
    /// 모아 둔 보정을 적용해 최종 속도(천분율)를 확정한다.
    ///
    /// <para>
    /// <b>실수 연산은 여기서 한 번만 일어나고 <c>int</c>로 끝난다.</b> 확정된 속도는
    /// 진행도 누적(<c>ConsumeJudgeCount</c>)에서 정수로만 쓰이므로, 작업량은 끝까지 정수로 남는다.
    /// </para>
    /// </summary>
    public int Resolve()
    {
        // 가산은 (1 + Σ가산)을 천분율 그대로 곱한 뒤 나눈다 — 먼저 곱해야 1 미만이 잘려 나가지 않는다.
        // 감소 보정이 겹쳐 -100%를 넘어가면 0으로 막는다(음수 속도는 시간을 거꾸로 돌리는 셈이다).
        var addFactor = Math.Max(0, WorkStationSlot.WorkSpeedScale + _addPermille);
        var speed     = (long)_baseWorkSpeed * addFactor / WorkStationSlot.WorkSpeedScale;

        // 승산은 소수 배수라 마지막에 한 번만 곱한다.
        if (_mulRate != 0)
            speed = (long)(speed * _mulRate);

        return (int)Math.Clamp(speed, WorkStationSlot.MinWorkSpeed, int.MaxValue);
    }
}
