using GameData;

namespace WSGameServer;

/// <summary>
/// 유저가 소유한 캐릭터 <b>개체</b>. 같은 캐릭터(TID)를 여러 장 가질 수 있으므로
/// <see cref="Id"/>(DB 발급 PK)와 <see cref="Tid"/>(테이블 정의)는 반드시 구분한다.
///
/// <para>
/// <b>스탯을 들고 있지 않다.</b> 캐릭터 스탯 = 산업 적성이고, 적성은 TID별 고정값이라
/// <see cref="CharacterTableRow"/>에서 읽는다. 저장해 두면 엑셀에서 밸런스를 조정해도
/// DB가 낡은 값을 붙들게 된다. 개체가 들고 있는 것은 성장의 <b>입력</b>(레벨·경험치)뿐이다.
/// </para>
/// </summary>
public sealed class Character
{
    /// <summary>테이블 정의를 생성자로 받는다 — 정적 조회에 묶이지 않아 테스트에서 바로 만들 수 있다.</summary>
    public Character(long id, CharacterTableRow row, int level, int exp)
    {
        Id    = id;
        Row   = row;
        Level = level;
        Exp   = exp;
    }

    /// <summary>캐릭터 개체 PK (<c>t_character.character_id</c>). DB가 발급한다.</summary>
    public long Id { get; }

    /// <summary>캐릭터 종류 (<c>CharacterTable.CharacterTID</c>).</summary>
    public int Tid => Row.CharacterTID;

    public CharacterTableRow Row { get; }

    public string Name => Row.Name;

    public int Level { get; private set; }

    public int Exp { get; private set; }

    /// <summary>
    /// 이 산업에 대한 적성(0~10). <b>캐릭터 스탯이 곧 산업 적성이다.</b>
    /// 1차 산업이 아닌 값(<c>Misc</c>·<c>Special</c>·<c>None</c>)은 0이다.
    /// </summary>
    public int GetAptitude(ItemType industry) => industry switch
    {
        ItemType.Farming => Row.Farming,
        ItemType.Fishing => Row.Fishing,
        ItemType.Mining  => Row.Mining,
        ItemType.Logging => Row.Logging,
        ItemType.Hunting => Row.Hunting,
        _                => 0,
    };

    /// <summary>적성 0 = 그 산업을 다루지 못한다. 배치를 거절하는 기준이다.</summary>
    public bool CanWork(ItemType industry) => GetAptitude(industry) > 0;

    /// <summary>
    /// 이 산업에서의 <b>기본 작업속도</b>(천분율). 적성을 <c>WorkSpeedTable</c>로 변환한다.
    ///
    /// <para>
    /// 특성·부스트·장비 보정이 붙기 <b>전</b>의 값이며, 슬롯의 최종 확정값
    /// (<c>WorkStationSlot.CurrentWorkSpeed</c>)과 구분한다.
    /// </para>
    ///
    /// <para>
    /// 변환식을 코드에 두지 않는 이유는 이 값이 <b>재화 생성량에 직접 곱해지기</b> 때문이다.
    /// 테이블에 두면 밸런스 조정이 엑셀 수정만으로 끝난다.
    /// </para>
    /// </summary>
    public int GetBaseWorkSpeed(ItemType industry)
    {
        var aptitude = GetAptitude(industry);

        // Ref 검사가 CharacterTable의 적성을 WorkSpeedTable.WorkSpeedTID와 대조하므로
        // 정상 데이터에서는 반드시 찾아진다. 못 찾으면 데이터가 깨진 것이니 0으로 막는다.
        return GameTable.WorkSpeedTable.TryGet(aptitude, out var row) ? row.BaseWorkSpeedPermille : 0;
    }
}
