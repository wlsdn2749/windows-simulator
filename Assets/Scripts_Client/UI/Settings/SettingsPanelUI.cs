using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 설정 패널 — 사용자가 바꿀 수 있는 값을 <b>입력받아 넘기는 곳</b>. 여기 모아 둔다.
///
/// <para>
/// ■ 두 종류를 한 화면에 담되 안에서는 나눈다<br/>
/// <b>창 제어</b>는 Win32 네이티브 창 자체를 건드린다(타이틀바·투명·항상위·클릭스루·크기·9분할 위치).
/// <b>일반 설정</b>은 게임 UI 안의 값이다(위젯 6칸 위치). 사용자에겐 한 화면이지만
/// 넘기는 대상이 달라서(<c>WindowManager</c> vs <c>WidgetPositionLayout</c>) 인스펙터를 헤더로 갈랐다.
/// </para>
///
/// <para>
/// ■ 여기는 "입력"만 한다<br/>
/// 값을 저장하지도, 레이아웃을 계산하지도 않는다. 저장은 <c>WindowManager</c>·
/// <c>WidgetPositionLayout</c>이 각자 <c>WindowSettings</c>로 하고, 배치 계산은 레이아웃이 한다.
/// <b>이 패널이 꺼져도 그것들은 계속 동작해야 하므로</b> 로직을 여기 두지 않는다.
/// </para>
///
/// <para>
/// ■ 드롭다운 옵션은 코드가 채운다<br/>
/// 라벨을 인스펙터에 손으로 넣으면 enum이 바뀔 때 조용히 어긋난다.
/// </para>
///
/// <para>
/// ⚠️ 창 관련 변화는 <b>빌드(.exe)에서만</b> 실제로 일어난다(<c>WindowManager</c>가 <c>#if !UNITY_EDITOR</c> 가드).
/// 에디터에서는 값만 바뀌고 창은 그대로다.
/// </para>
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    // ※ 아래는 인스펙터에서 반드시 연결해야 하는 "필수" 참조다.
    //   nullable:enable 상태라 ? 없이 두면 "생성자 종료 시 non-null" 검사(CS8618)에 걸리는데,
    //   ? 로 두면 미연결 시 조용히 무시돼 "왜 안 되지?"가 되어버린다. 그래서 = null! 로 non-null
    //   타입을 유지(경고 제거)하고, Start()에서 == null 검사로 미연결을 예외로 즉시 드러낸다(fail-fast).
    [CenterHeader("< 창 제어 (Win32 네이티브) >")]
    [SerializeField] private Toggle titleBarToggle            = null!; // OS 타이틀바+테두리 표시 토글(이 바로 창 드래그)
    [SerializeField] private Toggle transparentToggle         = null!; // 투명 배경 토글
    [SerializeField] private Toggle topmostToggle             = null!; // 항상 위 토글
    [SerializeField] private Toggle dynamicClickThroughToggle = null!; // 동적 클릭 통과 토글

    [SerializeField] private TMP_Dropdown sizeDropdown           = null!; // 창 크기 배율 프리셋
    [SerializeField] private TMP_Dropdown windowPositionDropdown = null!; // 창의 데스크톱 위치 (9분할 앵커)

    [CenterHeader("< 일반 설정 >")]
    [SerializeField] private TMP_Dropdown widgetPositionDropdown = null!; // 위젯의 창 안 위치 (6칸)

    // ※ WidgetPositionLayout은 Services에 등록되지 않는다([ExecuteAlways] 레이아웃 컴포넌트라
    //   에디터에서도 돌아야 해서 서비스 로케이터에 묶지 않았다). 그래서 인스펙터로 직접 받는다.
    [SerializeField, Tooltip("위젯 위치를 실제로 반영할 레이아웃 컴포넌트")]
    private WidgetPositionLayout widgetLayout = null!;

    // 토글/드롭다운을 현재 값으로 맞추고, 조작을 각 담당자에게 연결한다 (Unity 메시지)
    private void Start()
    {
        // 필수 참조 검증 — 미연결(null)이면 조용히 넘어가지 않고 즉시 예외로 어떤 참조인지 알린다.
        this.RequireRef(titleBarToggle,            nameof(titleBarToggle));
        this.RequireRef(transparentToggle,         nameof(transparentToggle));
        this.RequireRef(topmostToggle,             nameof(topmostToggle));
        this.RequireRef(dynamicClickThroughToggle, nameof(dynamicClickThroughToggle));
        this.RequireRef(sizeDropdown,              nameof(sizeDropdown));
        this.RequireRef(windowPositionDropdown,    nameof(windowPositionDropdown));
        this.RequireRef(widgetPositionDropdown,    nameof(widgetPositionDropdown));
        this.RequireRef(widgetLayout,              nameof(widgetLayout));

        var window = Services.Get<WindowManager>();

        // ─── 창 제어 ───
        // 초기값은 WindowManager가 저장에서 복원해 둔 "현재" 값이다.
        BindToggle(titleBarToggle,            window.TitleBar,            window.SetTitleBar);
        BindToggle(transparentToggle,         window.Transparent,         window.SetTransparent);
        BindToggle(topmostToggle,             window.Topmost,             window.SetTopmost);
        BindToggle(dynamicClickThroughToggle, window.DynamicClickThrough, window.SetDynamicClickThrough);

        BindDropdown(sizeDropdown,           window.GetSizeLabels(),   window.SizeIndex,   window.SetWindowSizeByIndex);
        BindDropdown(windowPositionDropdown, window.GetAnchorLabels(), window.AnchorIndex, window.SetAnchorByIndex);

        // ─── 일반 설정 ───
        BindDropdown(widgetPositionDropdown, GetWidgetPositionLabels(), (int)widgetLayout.Position,
                     index => widgetLayout.SetPosition((WidgetPosition)index));
    }

    // 위젯 6칸 라벨을 WidgetPosition enum 순서 그대로 만든다 (Start에서 호출)
    // ※ 창의 9분할 앵커와 나열 규칙이 같다 — index%3 = 가로, index/3 = 세로.
    private static List<string> GetWidgetPositionLabels() => new List<string>
    {
        "Upper Left", "Upper Center", "Upper Right",
        "Lower Left", "Lower Center", "Lower Right",
    };

    // 토글을 시작값으로 세팅(알림 없이)하고, 값 변경 시 창 제어 메서드를 호출하도록 연결
    private void BindToggle(Toggle toggle, bool startValue, UnityAction<bool> onChanged)
    {
        toggle.SetIsOnWithoutNotify(startValue);   // 시작 상태에 맞춰 체크(콜백 없이)
        toggle.onValueChanged.AddListener(onChanged);
    }

    // 드롭다운 옵션을 채우고 시작 인덱스로 세팅(알림 없이)한 뒤, 선택 변경 시 창 제어 메서드를 호출하도록 연결
    private void BindDropdown(TMP_Dropdown dropdown, List<string> options, int startIndex, UnityAction<int> onChanged)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(startIndex); // 시작 인덱스에 맞춤(콜백 없이)
        dropdown.onValueChanged.AddListener(onChanged);
    }
}
