using UnityEngine;

/// <summary>
/// 창 설정을 <see cref="PlayerPrefs"/>에 저장하고 되읽는다.
///
/// <para>
/// ■ 인스펙터 값의 의미가 바뀐다<br/>
/// <c>WindowManager</c>의 <c>setStart*</c> 필드는 이제 <b>"저장된 값이 없을 때 쓰는 공장 초기값"</b>이다.
/// 한 번이라도 사용자가 토글·드롭다운을 만지면 그 값이 저장되고, 다음 실행부터는 저장값이 이긴다.
/// </para>
///
/// <para>
/// ■ 왜 껐다 켤 때마다 되돌아가면 안 되는가<br/>
/// 이건 바탕화면에 상주하는 앱이다. "항상 위"·"크기"·"위치"는 <b>사용자의 작업 환경</b>이지
/// 게임 옵션이 아니다. 매번 다시 맞춰야 하면 상주 앱으로 쓸 수 없다.
/// </para>
///
/// <para>
/// ■ 저장 시점<br/>
/// 값이 <b>실제로 바뀔 때만</b> 즉시 기록한다(<see cref="PlayerPrefs.Save"/>).
/// Unity는 정상 종료 시에만 자동 저장하는데, 상주 앱은 작업 관리자로 끄거나 절전에 들어가는 일이
/// 흔해서 그것만 믿을 수 없다. 대신 같은 값을 다시 쓰지 않아 시작 시 불필요한 기록이 없다.
/// </para>
/// </summary>
public static class WindowSettings
{
    // 키에 접두사를 붙여 다른 설정(사운드 등)이 생겨도 섞이지 않게 한다.
    private const string Prefix = "Window.";

    public const string TitleBarKey            = Prefix + "TitleBar";
    public const string TransparentKey         = Prefix + "Transparent";
    public const string TopmostKey             = Prefix + "Topmost";
    public const string DynamicClickThroughKey = Prefix + "DynamicClickThrough";
    public const string ScaleKey               = Prefix + "Scale";
    public const string AnchorKey              = Prefix + "Anchor";

    // 위젯이 창 안 어느 칸(6칸)에 놓이는가. 창을 데스크톱 어디에 두는가(AnchorKey, 9분할)와는 다른 축이다.
    public const string WidgetPositionKey      = "Widget.Position";

    /// <summary>저장된 bool을 읽는다. 키가 없으면(첫 실행) <paramref name="fallback"/>을 돌려준다.</summary>
    public static bool LoadBool(string key, bool fallback)
    {
        return PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;
    }

    /// <summary>저장된 int를 읽는다. 키가 없으면(첫 실행) <paramref name="fallback"/>을 돌려준다.</summary>
    public static int LoadInt(string key, int fallback)
    {
        return PlayerPrefs.GetInt(key, fallback);
    }

    /// <summary>bool을 저장한다. PlayerPrefs에 bool 타입이 없어 0/1 int로 넣는다.</summary>
    public static void SaveBool(string key, bool value)
    {
        SaveInt(key, value ? 1 : 0);
    }

    /// <summary>int를 저장한다. 값이 그대로면 기록하지 않는다.</summary>
    public static void SaveInt(string key, int value)
    {
        // 시작 시 불러온 값을 그대로 다시 적용하는 경로가 있어(InitializeWindow), 같은 값 쓰기를 걸러 낸다.
        if (PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key) == value)
            return;

        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }
}
