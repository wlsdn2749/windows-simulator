namespace WSGameServer;

/// <summary>
/// 드롭 후보 한 줄. 테이블 Row가 산업마다 다른 타입이라(<c>FishingBasicTableRow</c>,
/// <c>MiningBasicTableRow</c> …) <b>공통 형태로 정규화</b>해서 담는다.
/// 드롭의 결과는 결국 "어떤 <c>ItemTID</c>가 나왔는가"뿐이므로 이 두 값이면 충분하다.
/// </summary>
public readonly record struct DropEntry(int ItemTID, int Weight);

/// <summary>
/// 드롭 테이블 하나. <see cref="WeightedPicker{T}"/>를 감싸 <b>결과를 <c>ItemTID</c>로 좁힌</b> 것이다.
///
/// <para>
/// 테이블 로드 시 한 번 만들어 <see cref="DropTableCatalog"/>에 등록하고, 판정마다 재사용한다.
/// 판정할 때마다 새로 만들면 누적 가중치를 미리 쌓아 두는 이점이 사라진다.
/// </para>
/// </summary>
public sealed class DropTable
{
    private readonly WeightedPicker<DropEntry> _picker;

    private DropTable(string name, WeightedPicker<DropEntry> picker)
    {
        Name    = name;
        _picker = picker;
    }

    /// <summary>진단·로그용 이름. 보통 시트 이름을 그대로 쓴다.</summary>
    public string Name { get; }

    /// <summary>후보 수. 가중치 0이라 빠진 항목은 세지 않는다.</summary>
    public int Count => _picker.Count;

    /// <summary>전체 가중치 합.</summary>
    public int TotalWeight => _picker.TotalWeight;

    /// <summary>
    /// 테이블 Row 목록에서 드롭 테이블을 만든다.
    /// Row 타입을 모르므로 <c>ItemTID</c>·<c>Weight</c>를 어떻게 꺼낼지만 받는다.
    /// </summary>
    public static DropTable From<TRow>(
        string           name,
        IEnumerable<TRow> rows,
        Func<TRow, int>  itemTidSelector,
        Func<TRow, int>  weightSelector)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(itemTidSelector);
        ArgumentNullException.ThrowIfNull(weightSelector);

        var entries = rows.Select(r => new DropEntry(itemTidSelector(r), weightSelector(r)));
        return new DropTable(name, WeightedPicker.From(entries, e => e.Weight));
    }

    /// <summary>이미 정규화된 후보로 만든다.</summary>
    public static DropTable From(string name, IEnumerable<DropEntry> entries)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return new DropTable(name, WeightedPicker.From(entries, e => e.Weight));
    }

    /// <summary>한 번 굴려 나온 <c>ItemTID</c>를 반환한다.</summary>
    public int Roll(Random? random = null) => _picker.Pick(random).ItemTID;

    /// <summary>
    /// count회 굴려 <c>ItemTID</c>별 개수를 집계한다.
    /// 오프라인 정산이 쓰는 경로다 — 판정 수천 회의 결과를 리스트로 쌓지 않고 바로 센다.
    /// </summary>
    public Dictionary<int, int> RollMany(int count, Random? random = null)
    {
        var gained = new Dictionary<int, int>();
        _picker.PickMany(count, e => gained[e.ItemTID] = gained.GetValueOrDefault(e.ItemTID) + 1, random);
        return gained;
    }

    /// <summary>기존 집계에 count회 결과를 더한다. 여러 층(기본·특별·공통)을 한 사전에 모을 때 쓴다.</summary>
    public void RollManyInto(int count, Dictionary<int, int> gained, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(gained);
        _picker.PickMany(count, e => gained[e.ItemTID] = gained.GetValueOrDefault(e.ItemTID) + 1, random);
    }

    /// <summary>후보 <paramref name="index"/>가 뽑힐 확률(0~1). 밸런스 검증용.</summary>
    public double ProbabilityOf(int index) => _picker.ProbabilityOf(index);

    public override string ToString() => $"{Name}(후보 {Count}개, 합 {TotalWeight})";
}
