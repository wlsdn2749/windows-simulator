using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 가챠 풀 항목 하나. 엑셀 <c>GachaTable</c>의 Row를 <b>추첨에 필요한 값만으로 정규화</b>한 것이다.
/// 등급은 여기 두지 않는다 — <b>등급의 원본은 <c>ItemTable</c> 하나다.</b>
/// 풀에 등급을 따로 적으면 아이템 쪽과 독립적으로 존재하다가 조용히 어긋난다(깃허브 이슈 #9 2-3).
/// </summary>
public readonly record struct GachaEntry(int GachaId, int ItemTID, int Count, int Weight);

/// <summary>
/// 가챠 풀 보관소. <b>GachaId별로</b> 추첨기를 찾아 준다.
///
/// <para>
/// 풀 데이터의 원본은 엑셀 <c>Gacha.xlsx → GachaTable</c>이다. 시트가 <c>ItemTID</c>에
/// <c>Ref: ItemTable.ItemTID</c>를 걸고 있어, 실재하지 않는 아이템을 주는 풀은
/// <c>generate-tables.ps1</c> 시점에 막힌다.
/// </para>
///
/// <para>
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고,
/// 이후에는 조회만 한다. 로드가 끝나면 불변이라 여러 스레드가 동시에 읽어도 안전하다.
/// </para>
/// </summary>
public sealed class GachaPoolCatalog : Singleton<GachaPoolCatalog>
{
    private Dictionary<int, WeightedPicker<GachaEntry>> _byPool = new();

    /// <summary>등록된 풀 수.</summary>
    public int Count => _byPool.Count;

    /// <summary>엑셀 <c>GachaTable</c> 전 행을 풀별 추첨기로 만든다.</summary>
    /// <remarks>반드시 <c>GameTable.LoadAll</c> 이후에 부른다. 테이블 데이터가 없으면 여기서 터진다.</remarks>
    public void LoadAll()
    {
        Load(GameTable.GachaTable.All,
             r => r.GachaId, r => r.ItemTID, r => r.Count, r => r.Weight);

        ServerLog.Info("데이터", $"가챠 풀 {Count}개 등록 완료");
    }

    /// <summary>Row 목록을 정규화해 풀별 추첨기를 만든다. 기존 등록은 전부 교체된다.</summary>
    public void Load<TRow>(
        IEnumerable<TRow> rows,
        Func<TRow, int>   gachaIdSelector,
        Func<TRow, int>   itemTidSelector,
        Func<TRow, int>   countSelector,
        Func<TRow, int>   weightSelector)
    {
        var entries = rows.Select(r => new GachaEntry(
            gachaIdSelector(r), itemTidSelector(r), countSelector(r), weightSelector(r)));

        _byPool = WeightedPicker.GroupBy(entries, e => e.GachaId, e => e.Weight);
    }

    public bool TryGet(int gachaId, out WeightedPicker<GachaEntry> pool)
        => _byPool.TryGetValue(gachaId, out pool!);
}
