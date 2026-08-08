using GameData;

namespace WSGameServer;

public partial class User
{

    /// <param name="now">
    /// 적재 기준 시각. 슬롯 진행도가 여기서부터 시작한다 —
    /// 호출자(<see cref="LoginRepository.Apply"/>)가 넘긴다.
    /// </param>
    public void OnLoginDataLoaded(PlayerLoginData data, DateTime now)
    {
        LoadPlayerData(data, now);

        EnsureDefaultSlots(now);

        if (_characters.Count == 0)
        {
            // 만약 캐릭터가 없을 경우, 발급 후 마무리를 이어감.
            // 마무리 시각은 지급이 끝난 뒤 GrantCharacterRepository가 새로 넘긴다 —
            // DB 왕복을 건너므로 여기 시각을 끌고 가면 낡은 값이 된다.
            GrantDefaultCharacter();
            return;
        }

        FinishLogin(now);
    }

    /// <summary>
    /// 조회 결과(Row)를 도메인으로 변환해 적재한다 — 적재만 하고 아무것도 판단하지 않는다.
    ///
    /// <para>
    /// <b>Row가 들어오는 것은 여기(User의 partial)까지다.</b> 순수 코어(Inventory·Wallet·WorkStation)에는
    /// 도메인 객체만 넘긴다 — 코어가 Repository 타입을 참조하면 의존 방향이 뒤집힌다.
    /// </para>
    /// </summary>
    private void LoadPlayerData(PlayerLoginData data, DateTime startedAt)
    {
        LoadInventory(data.InventoryRows);
        LoadCurrencies(data.CurrencyRows);

        // 캐릭터가 슬롯 속도의 근거이므로 슬롯보다 먼저 적재한다.
        LoadCharacters(data.CharacterRows);
        LoadWorkStation(data.WorkStationSlotRows, startedAt);
    }

    /// <summary>
    /// 기본 슬롯이 없으면 연다. 메모리에서 먼저 열고 저장은 흘려보낸다 —
    /// 같은 세션 키로 직렬이라 순서가 어긋나지 않는다.
    /// </summary>
    private void EnsureDefaultSlots(DateTime now)
    {
        if (WorkStation.Count > 0)
            return;

        for (var i = 0; i < DefaultSlotCount; i++)
            WorkStation.Unlock(i, now);

        SaveWorkStationSlots(Enumerable.Range(0, DefaultSlotCount));
    }

    /// <summary>기본 캐릭터 지급을 요청한다. 완료는 <see cref="OnDefaultCharacterGranted"/>로 돌아온다.</summary>
    private void GrantDefaultCharacter()
    {
        PostDBTask(new GrantCharacterRepository(this, DefaultCharacterTid));
    }

    /// <summary>기본 캐릭터 지급이 끝나면 불린다(로직 스레드). 적재 후 로그인을 마무리한다.</summary>
    public void OnDefaultCharacterGranted(long characterId, DateTime now)
    {
        // 테이블 검증은 LoadCharacters와 같은 정책이다 — 없다고 로그인을 막지 않는다.
        if (GameTable.CharacterTable.TryGet(DefaultCharacterTid, out var row))
        {
            _characters[characterId] = new Character(characterId, row, level: 1, exp: 0);
        }
        else
        {
            ServerLog.Warn("로그인", $"CharacterTable에 기본 캐릭터 TID가 없음: {DefaultCharacterTid}");
        }

        FinishLogin(now);
    }

    /// <summary>적재·지급이 모두 끝난 뒤 속도를 확정하고 응답을 보낸다.</summary>
    private void FinishLogin(DateTime now)
    {
        // 배치된 캐릭터의 적성으로 각 슬롯의 속도를 맞춘다.
        // 접속마다 다시 계산하므로 그동안 밸런스가 바뀌었어도 반영된다.
        RefreshWorkStationSpeed(now, notify: false);

        Login();   // S_LoginResponse + 인벤·재화·슬롯 스냅샷 전송
    }
}
