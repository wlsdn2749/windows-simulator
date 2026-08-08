namespace WSGameServer;

public static class Global
{
    private static ulong _seq = 1;

    public static ulong AllocKey64()
    {
        return Interlocked.Increment(ref _seq);
    }

    /// <summary>
    /// <b>채취 전역 속도 배수.</b> 2.0이면 모든 슬롯이 2배 빨리 캔다.
    ///
    /// <para>
    /// 기획 수치(<c>WorkStationSlot.BaseCycleSeconds</c> = 30초, <c>WorkSpeedTable</c>)는 그대로 두고
    /// 서버 전체를 이 값 하나로 당긴다. 기획 데이터를 확인 편의로 고치면 엑셀이 진짜 밸런스인지
    /// 테스트용 값인지 구분이 사라진다.
    /// </para>
    ///
    /// <para>
    /// 현재 <b>6.0배</b> — 기준 속도(적성 1 = 1000천분율) 슬롯이 30초가 아니라 <b>5초</b>에 한 번 판정한다.
    /// ⚠️ 확인용 설정이므로 배포 전에 1.0으로 되돌린다(1.0이 아니면 시작 시 경고를 찍는다).
    /// </para>
    /// </summary>
    public const double GatherSpeedMultiplier = 6.0;

    /// <summary>
    /// <b>무응답 판정 시간.</b> 이만큼 아무 바이트도 못 받은 세션은 끊는다.
    ///
    /// <para>
    /// 클라이언트는 5초마다 <c>C_PingRequest</c>를 보낸다 — 세 번 연속 놓쳐야 끊기는 값이다.
    /// </para>
    ///
    /// <para>
    /// <b>이 값이 곧 "공짜로 얻는 최대 오프라인 시간"이다.</b> 접속 판정이 재화 생성 조건이라
    /// (게임기획코어 P2), 끊긴 걸 서버가 모르는 구간만큼 부당 적립이 생긴다.
    /// 채취 기준 주기(<see cref="WorkStationSlot.BaseCycleSeconds"/> = 30초)보다 짧게 두어
    /// <b>부당 구간이 판정 1회를 못 채우게</b> 했다.
    /// </para>
    /// </summary>
    public static readonly TimeSpan SessionIdleTimeout = TimeSpan.FromSeconds(15);
}

