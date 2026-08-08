using GameData;

namespace WSGameServer;

/// <summary>
/// 유저가 가진 재화 전부. <b>재화는 유저 소유</b>다(캐릭터가 아니라).
///
/// <para>
/// 보유량을 <b><see cref="long"/></b>으로 다룬다. 인벤토리 수량은 <c>int</c>지만 재화는 다르다 —
/// 거래소가 붙으면 누적 골드가 <c>int</c> 상한(약 21억)을 넘길 수 있고,
/// 넘치는 순간 조용히 음수가 되어 되돌릴 수 없다.
/// </para>
///
/// <para>
/// 보유하지 않은 재화는 <b>항목이 없는 것</b>으로 두고 0으로 읽는다. 가입 시 0짜리 행을 만들어 두면
/// 재화 종류가 늘 때마다 기존 유저 전원에게 백필이 필요해진다.
/// </para>
/// </summary>
public sealed class CurrencyWallet
{
    private readonly Dictionary<CurrencyType, long> _amounts = new();

    /// <summary>DB에서 읽은 보유량을 적재한다(로그인 시 1회).</summary>
    public void Load(IReadOnlyDictionary<CurrencyType, long> amounts)
    {
        _amounts.Clear();
        foreach (var (type, amount) in amounts)
            _amounts[type] = amount;
    }

    /// <summary>보유량. 가진 적이 없으면 0.</summary>
    public long Get(CurrencyType type) => _amounts.GetValueOrDefault(type);

    public bool CanAfford(CurrencyType type, long amount) => Get(type) >= amount;

    /// <summary>
    /// 재화를 더한다. <paramref name="amount"/>는 양수여야 한다 —
    /// 음수를 허용하면 "지급" 경로로 차감이 일어나 잔액 검사를 우회하게 된다.
    /// </summary>
    /// <returns>변경 후 보유량.</returns>
    public long Gain(CurrencyType type, long amount)
    {
        if (type == CurrencyType.None)
            throw new ArgumentException("재화 종류가 지정되지 않았습니다.", nameof(type));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        var current = Get(type);

        // 오버플로는 음수로 뒤집혀 잔액이 통째로 사라진다. 터뜨리는 편이 낫다.
        _amounts[type] = checked(current + amount);
        return _amounts[type];
    }

    /// <summary>
    /// 재화를 차감한다. <b>잔액이 모자라면 아무것도 바꾸지 않고 false를 돌려준다</b> —
    /// 부분 차감하면 호출자가 실패를 알아채기 어렵다.
    /// </summary>
    /// <returns>차감에 성공했으면 true.</returns>
    public bool TrySpend(CurrencyType type, long amount, out long remain)
    {
        if (type == CurrencyType.None)
            throw new ArgumentException("재화 종류가 지정되지 않았습니다.", nameof(type));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        var current = Get(type);
        if (current < amount)
        {
            remain = current;
            return false;
        }

        remain = current - amount;
        _amounts[type] = remain;
        return true;
    }

    /// <summary>보유 중인 재화 전부. 0인 항목은 담기지 않는다.</summary>
    public IReadOnlyDictionary<CurrencyType, long> Snapshot() => _amounts;
}
