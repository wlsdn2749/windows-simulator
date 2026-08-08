namespace WSGameServer;

/// <summary>
/// 가중치 기반 추첨기. "여러 후보 중 하나를 확률로 고르는" 모든 곳에서 쓴다
/// (채취 드롭 테이블, 희귀도 롤, 가챠 풀 …).
///
/// <para>
/// <b>후보 목록이 고정되면 한 번 만들어 두고 재사용한다.</b> 생성 시 누적 가중치를 미리 쌓아 두고
/// 추첨은 이진 탐색 O(log n)으로 끝낸다. 채취 오프라인 정산은 접속 한 번에 판정을
/// 수천 회(24시간 = 2,880회) 몰아서 돌리므로, 매 추첨마다 가중치 합을 다시 더하면 그게 그대로 비용이 된다.
/// </para>
///
/// <para>
/// 생성 후 <b>불변</b>이라 여러 스레드가 동시에 <see cref="Pick(Random?)"/>해도 안전하다.
/// 난수원만 주의하면 된다 — 기본값 <see cref="Random.Shared"/>는 스레드 안전하고,
/// <see cref="Random"/>을 직접 넘길 때는 그 인스턴스를 공유하지 않아야 한다.
/// </para>
///
/// <para>
/// 재화가 생성되는 지점이므로 <b>추첨은 서버에서만</b> 돈다(게임기획코어 P4).
/// </para>
/// </summary>
/// <typeparam name="T">후보 항목. 테이블 Row든 무엇이든 상관없다 — 이 클래스는 항목의 내용을 모른다.</typeparam>
public sealed class WeightedPicker<T>
{
    private readonly T[] _items;

    /// <summary>_items와 같은 길이의 누적 가중치(오름차순). 마지막 원소가 곧 전체 합이다.</summary>
    private readonly int[] _cumulative;

    private WeightedPicker(T[] items, int[] cumulative)
    {
        _items      = items;
        _cumulative = cumulative;
    }

    /// <summary>추첨 후보 수. 가중치 0이라 제외된 항목은 세지 않는다.</summary>
    public int Count => _items.Length;

    /// <summary>전체 가중치 합. 100%를 뜻하는 값이며 100으로 맞출 필요는 없다.</summary>
    public int TotalWeight => _cumulative[^1];

    /// <summary>원본 순서를 유지한 후보 목록(가중치 0 항목 제외).</summary>
    public IReadOnlyList<T> Items => _items;

    /// <summary>
    /// 후보와 가중치 선택자로 추첨기를 만든다.
    /// </summary>
    /// <remarks>
    /// <b>가중치 0인 항목은 후보에서 빼고, 음수는 예외로 막는다.</b>
    /// 0은 "당분간 안 나오게 막아 둔다"는 흔한 기획 의도라 정상 입력으로 받아들이지만,
    /// 음수는 어떤 의도로도 해석되지 않으므로 데이터 오류로 본다.
    /// </remarks>
    /// <exception cref="ArgumentException">후보가 없거나, 가중치가 전부 0이거나, 합이 int를 넘을 때.</exception>
    public static WeightedPicker<T> From(IEnumerable<T> items, Func<T, int> weightSelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weightSelector);

        var picked     = new List<T>();
        var cumulative = new List<int>();
        var running    = 0L;   // 오버플로를 검사하려고 long으로 누적한다

        foreach (var item in items)
        {
            var weight = weightSelector(item);
            if (weight < 0)
                throw new ArgumentException($"[{typeof(T).Name}] 가중치가 음수입니다: {weight}", nameof(items));
            if (weight == 0)
                continue;   // 뽑히지 않는 항목 — 후보에서 제외한다

            running += weight;
            if (running > int.MaxValue)
                throw new ArgumentException($"[{typeof(T).Name}] 가중치 합이 int 범위를 넘습니다.", nameof(items));

            picked.Add(item);
            cumulative.Add((int)running);
        }

        if (picked.Count == 0)
            throw new ArgumentException(
                $"[{typeof(T).Name}] 뽑을 수 있는 후보가 없습니다(비었거나 가중치가 전부 0).", nameof(items));

        return new WeightedPicker<T>(picked.ToArray(), cumulative.ToArray());
    }

    /// <summary>한 번 추첨한다.</summary>
    /// <param name="random">테스트에서 시드를 고정하려면 넘긴다. 생략하면 <see cref="Random.Shared"/>.</param>
    public T Pick(Random? random = null)
    {
        var roll = (random ?? Random.Shared).Next(TotalWeight);   // [0, TotalWeight)

        // roll이 속한 누적 구간 = roll보다 큰 첫 원소의 위치.
        // BinarySearch는 값을 찾으면 그 인덱스를, 못 찾으면 삽입 위치의 보수(~i)를 준다.
        // 정확히 일치했다는 건 roll이 그 구간의 끝(= 다음 구간의 시작)이라는 뜻이라 +1 한다.
        var found = Array.BinarySearch(_cumulative, roll);
        return _items[found >= 0 ? found + 1 : ~found];
    }

    /// <summary>count회 독립 추첨해 뽑힌 순서대로 반환한다. 가챠 10연차처럼 연출에 순서가 필요할 때 쓴다.</summary>
    public List<T> PickMany(int count, Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var rng     = random ?? Random.Shared;
        var results = new List<T>(count);
        for (var i = 0; i < count; i++)
            results.Add(Pick(rng));

        return results;
    }

    /// <summary>
    /// count회 독립 추첨하되 결과를 모아 두지 않고 그때그때 넘긴다.
    /// 오프라인 정산처럼 <b>수천 회를 돌려 개수만 집계</b>하는 경우, 중간 리스트를 만들지 않으려고 쓴다.
    /// </summary>
    public void PickMany(int count, Action<T> onPicked, Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(onPicked);

        var rng = random ?? Random.Shared;
        for (var i = 0; i < count; i++)
            onPicked(Pick(rng));
    }

    /// <summary>
    /// 후보 <paramref name="index"/>가 뽑힐 확률(0~1). 밸런스 검증·테스트용이며 추첨 경로에서는 쓰지 않는다.
    /// </summary>
    public double ProbabilityOf(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _items.Length);

        var weight = index == 0 ? _cumulative[0] : _cumulative[index] - _cumulative[index - 1];
        return (double)weight / TotalWeight;
    }
}

/// <summary>
/// <see cref="WeightedPicker{T}"/> 생성 헬퍼.
/// 제네릭 인자를 적지 않아도 되도록 타입 추론이 되는 진입점을 제공한다.
/// </summary>
public static class WeightedPicker
{
    /// <summary>후보 목록으로 추첨기를 만든다. <c>WeightedPicker.From(rows, r =&gt; r.Weight)</c></summary>
    public static WeightedPicker<T> From<T>(IEnumerable<T> items, Func<T, int> weightSelector)
        => WeightedPicker<T>.From(items, weightSelector);

    /// <summary>
    /// 그룹 축이 있는 테이블을 <c>(그룹 키 → 추첨기)</c>로 나눠 만든다.
    ///
    /// <para>
    /// 드롭 시트는 제너레이터 제약(단일 컬럼 키) 때문에 첫 컬럼에 고유 <c>DropTID</c>를 두고
    /// 실제 그룹 축(<c>SpotTID</c>·<c>FieldTID</c>·깊이 구간)은 일반 컬럼으로 둔다.
    /// 그래서 <b>로드 후 서버가 그룹 인덱스를 만들어야</b> 하는데, 그 작업이 이것이다.
    /// 테이블 로드 시 한 번 만들어 캐시해 두고 재사용한다.
    /// </para>
    /// </summary>
    public static Dictionary<TGroup, WeightedPicker<T>> GroupBy<T, TGroup>(
        IEnumerable<T>   items,
        Func<T, TGroup>  groupSelector,
        Func<T, int>     weightSelector)
        where TGroup : notnull
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(groupSelector);

        var buckets = new Dictionary<TGroup, List<T>>();
        foreach (var item in items)
        {
            var group = groupSelector(item);
            if (!buckets.TryGetValue(group, out var bucket))
                buckets[group] = bucket = new List<T>();
            bucket.Add(item);
        }

        var result = new Dictionary<TGroup, WeightedPicker<T>>(buckets.Count);
        foreach (var (group, bucket) in buckets)
            result[group] = WeightedPicker<T>.From(bucket, weightSelector);

        return result;
    }
}
