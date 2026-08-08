using GameData;
using MikaProtocol;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 가챠 뽑기 흐름을 조립하는 서비스 계층(Singleton).
/// 추첨은 <see cref="GachaPoolCatalog"/>(엑셀 <c>GachaTable</c> 기반)에 위임하고,
/// 인벤토리/DB 부수효과는 <see cref="User"/>에 위임한다. 여기서는 검증·조립·응답만 담당한다.
/// </summary>
public sealed class GachaService : Singleton<GachaService>
{
    // 허용하는 뽑기 횟수(단차 / 10연차)
    private const int SingleDraw = 1;
    private const int MultiDraw = 10;

    public void Draw(User user, int gachaId, int drawCount)
    {
        // 1) 검증: 뽑기 횟수와 풀 존재 여부 — 실패해도 반드시 응답한다(코드로 이유를 구분)
        if (drawCount != SingleDraw && drawCount != MultiDraw)
        {
            user.Send(new S_GachaDrawResponse { Result = EResultCode.InvalidDrawCount });
            return;
        }

        if (!GachaPoolCatalog.Instance.TryGet(gachaId, out var pool))
        {
            user.Send(new S_GachaDrawResponse { Result = EResultCode.InvalidGachaId });
            return;
        }

        // 2) 순수 추첨(뽑힌 순서대로)
        var entries = pool.PickMany(drawCount);

        // 3) 연출용 결과: 뽑힌 순서/개별 항목 그대로. 등급은 ItemTable에서 읽는다(단일 원본)
        var rewards = new List<GachaRewardInfo>(entries.Count);
        foreach (var entry in entries)
            rewards.Add(new GachaRewardInfo
            {
                ItemId = entry.ItemTID,
                Count = entry.Count,
                Rarity = RarityOf(entry.ItemTID),
            });

        // 4) 인벤토리 반영: itemId별 수량을 합산해 아이템당 한 번만 갱신(UPSERT 최소화)
        var gained = new Dictionary<int, int>();
        foreach (var entry in entries)
            gained[entry.ItemTID] = gained.GetValueOrDefault(entry.ItemTID) + entry.Count;

        var changes = new List<ItemChangeInfo>(gained.Count);
        foreach (var (itemId, count) in gained)
            changes.Add(user.GainItem(itemId, count));

        // 5) 뽑기 결과 응답 — Rewards는 연출용(델타), ItemChangeInfos는 인벤토리 반영용(누적 총량)
        user.Send(new S_GachaDrawResponse
        {
            Result = EResultCode.Ok,
            Rewards = rewards,
            ItemChangeInfos = changes,
        });
    }

    // GameData.GlobalRarity와 프로토콜 EGlobalRarity는 값이 1:1이라 byte 캐스팅으로 옮긴다.
    // 풀의 ItemTID는 시트 Ref 검사로 실재가 보장되므로 None은 실제로는 나오지 않는다.
    private static EGlobalRarity RarityOf(int itemTid)
        => GameTable.ItemTable.TryGet(itemTid, out var item)
            ? (EGlobalRarity)(byte)item.GlobalRarity
            : EGlobalRarity.None;
}
