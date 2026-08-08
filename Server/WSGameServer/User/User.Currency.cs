using GameData;
using MikaProtocol;
using WSGameServer;

namespace WSGameServer;

public partial class User
{
    /// <summary>유저가 가진 재화. <b>캐릭터가 아니라 유저 소유다.</b></summary>
    private CurrencyWallet Wallet { get; } = new();

    /// <summary>DB에서 읽은 재화 Row를 적재한다(로그인 시 1회). 행이 없는 재화는 0으로 본다.</summary>
    private void LoadCurrencies(IReadOnlyList<CurrencyRow> rows)
    {
        Wallet.Load(rows.ToDictionary(r => (CurrencyType)r.currency_type, r => r.amount));
    }

    /// <summary>보유량 조회. 가진 적이 없으면 0.</summary>
    public long GetCurrency(CurrencyType type) => Wallet.Get(type);

    /// <summary>보유 재화 전체를 보낸다(로그인 직후).</summary>
    public void SendCurrencies()
    {
        Send(new S_CurrencyResponse
        {
            Currencies = Wallet.Snapshot()
                .Select(kv => new CurrencyInfo { CurrencyType = (byte)kv.Key, Amount = kv.Value })
                .ToList(),
        });
    }

    /// <summary>재화를 지급하고 저장·통지한다.</summary>
    /// <returns>변경 후 보유량.</returns>
    public long GainCurrency(CurrencyType type, long amount)
    {
        var remain = Wallet.Gain(type, amount);
        SaveAndNotifyCurrency(type, remain);
        return remain;
    }

    /// <summary>
    /// 재화를 차감하고 저장·통지한다. <b>잔액이 모자라면 아무것도 바꾸지 않는다.</b>
    /// </summary>
    /// <returns>차감에 성공했으면 true.</returns>
    public bool TrySpendCurrency(CurrencyType type, long amount)
    {
        if (!Wallet.TrySpend(type, amount, out var remain))
            return false;

        SaveAndNotifyCurrency(type, remain);
        return true;
    }

    /// <summary>바뀐 재화 하나만 저장하고 통지한다. 잔액은 이미 확정된 값이다.</summary>
    private void SaveAndNotifyCurrency(CurrencyType type, long remain)
    {
        PostDBTask(new SaveCurrencyRepository(this, type, remain));

        Send(new S_CurrencyResponse
        {
            Currencies = new List<CurrencyInfo>
            {
                new() { CurrencyType = (byte)type, Amount = remain },
            },
        });
    }
}
