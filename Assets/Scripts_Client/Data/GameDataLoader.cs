using System;
using System.Collections.Generic;
using System.IO;
using GameData;
using UnityEngine;

/// <summary>
/// 엑셀에서 생성된 게임 테이블(<see cref="GameTable"/>)을 StreamingAssets에서 읽어 적재한다.
///
/// ■ 왜 씬 오브젝트가 아니라 RuntimeInitializeOnLoadMethod 인가
///   테이블은 UI가 이름을 찍는 순간 이미 있어야 한다. 씬에 매니저로 두면 다른 컴포넌트의
///   Awake·OnEnable과 순서 경쟁이 생기고, 그 순서는 Unity가 보장해 주지 않는다.
///   BeforeSceneLoad 는 <b>씬의 어떤 Awake보다도 먼저</b> 실행되므로 순서 문제 자체가 없어진다.
///   (같은 부류의 함정 — MonoService 주석의 "조회는 Start에서" 규칙 참조)
///
/// ■ 왜 UnityWebRequest 를 쓰지 않는가
///   StreamingAssets를 파일로 직접 읽을 수 없는 플랫폼은 Android·WebGL이다.
///   이 게임은 Windows 데스크톱 전용이라 File 로 충분하고, 동기 로드라 순서가 단순해진다.
/// </summary>
public static class GameDataLoader
{
    // .bytes 들이 놓이는 StreamingAssets 하위 폴더 (generate-tables.ps1이 여기로 미러링한다)
    private const string DataFolderName = "Data";

    private static bool _isLoaded;

    /// <summary>테이블이 적재됐는지. 실패 시 false로 남아 있다.</summary>
    public static bool IsLoaded => _isLoaded;

    // 씬 로드 전에 테이블을 적재한다 (Unity 런타임 초기화 훅)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStartup()
    {
        Load();
    }

    /// <summary>
    /// 모든 테이블을 적재한다. 이미 적재됐으면 아무것도 하지 않는다.
    /// 파일이 없거나 깨졌으면 <b>예외를 그대로 올린다</b> — 이름이 빈 채로 게임이 도는 것보다
    /// 그 자리에서 멈추는 편이 원인을 찾기 쉽다(fail-fast).
    /// </summary>
    public static void Load()
    {
        if (_isLoaded)
            return;

        string dataPath = Path.Combine(Application.streamingAssetsPath, DataFolderName);

        try
        {
            GameTable.LoadAll(fileName => File.ReadAllBytes(Path.Combine(dataPath, fileName)));
        }
        catch (Exception e)
        {
            // 예외는 그대로 올린다(fail-fast). 다만 원인 지점을 먼저 말해 준다 —
            // 스택만 보면 "파일이 없다"까지는 알아도 어느 폴더를 봐야 하는지, 무엇이 그 폴더를
            // 채우는지가 안 나온다.
            ClientLogger.Error(ClientLogger.Data,
                $"테이블 적재 실패 — {dataPath}\n" +
                $"    StreamingAssets에 .bytes가 없거나 깨졌다. GameDesign/generate-tables.ps1을 실행해 생성물을 갱신할 것.\n" +
                $"    {e.GetType().Name}: {e.Message}");
            throw;
        }

        _isLoaded = true;
        ClientLogger.Info(ClientLogger.Data, $"테이블 적재 완료 — 아이템 {GameTable.ItemTable.Count}종, 캐릭터 {GameTable.CharacterTable.Count}종");
    }

    // 이미 경고한 Id. 매 프레임 갱신되는 UI에서 같은 경고가 쏟아지는 것을 막는다.
    private static readonly HashSet<int> _warnedItemIds      = new HashSet<int>();
    private static readonly HashSet<int> _warnedCharacterIds = new HashSet<int>();

    /// <summary>
    /// 아이템 이름을 조회한다. 표시용이라 예외를 던지지 않고 <c>?#Id</c>로 떨어진다.
    /// <b>대신 처음 한 번은 경고를 남긴다</b> — 조용히 Id만 보여 주면 "이름이 안 나온다"로만 보이고
    /// 원인(테이블에 없는 Id가 오고 있다)이 드러나지 않는다.
    /// </summary>
    public static string GetItemName(int itemId)
    {
        if (GameTable.ItemTable.TryGet(itemId, out var row))
            return row.Name;

        WarnUnknownId("아이템", itemId, _warnedItemIds);
        return $"?#{itemId}";
    }

    /// <summary>캐릭터 이름을 조회한다. 규칙은 <see cref="GetItemName"/>과 같다.</summary>
    public static string GetCharacterName(long characterId)
    {
        if (GameTable.CharacterTable.TryGet((int)characterId, out var row))
            return row.Name;

        WarnUnknownId("캐릭터", (int)characterId, _warnedCharacterIds);
        return $"?#{characterId}";
    }

    // 테이블에 없는 Id를 처음 만났을 때만 경고한다 (GetItemName·GetCharacterName에서 호출)
    private static void WarnUnknownId(string kind, int id, HashSet<int> warned)
    {
        if (!warned.Add(id))
            return;

        ClientLogger.Warn(ClientLogger.Data, $"{kind} 테이블에 없는 Id {id}가 들어왔다. " +
                                       $"서버가 보내는 Id와 엑셀 데이터가 어긋났는지 확인할 것.");
    }
}
