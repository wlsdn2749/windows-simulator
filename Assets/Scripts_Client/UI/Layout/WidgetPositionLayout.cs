using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 위젯이 놓일 창 안 6칸(가로 3 × 세로 2). ScreenAnchor 와 같은 나열 순서를 써서
/// index % 3 = 가로(0=왼쪽,1=가운데,2=오른쪽), index / 3 = 세로(0=위,1=아래)로 계산할 수 있다.
///
/// ※ 창을 데스크톱 어디에 두는가(ScreenAnchor 9분할)와는 <b>다른 축</b>이다. 둘을 섞지 않는다.
/// </summary>
public enum WidgetPosition
{
    UpperLeft, UpperCenter, UpperRight,
    LowerLeft, LowerCenter, LowerRight,
}

/// <summary>
/// 위젯 위치(6칸)에 맞춰 3열 순서와 위젯/상태 패널의 위·아래 슬롯을 배치한다.
///
/// ■ "상단바"가 아니라 "상태 패널"인 이유
///   위젯이 위 칸으로 가면 이 패널은 <b>아래로 내려간다.</b> 위치를 이름에 담으면 절반은 거짓이 된다.
///   담는 것(계정 레벨·골드·시스템 아이콘 = 상태)으로 이름을 붙인다.
///
/// ■ 좌표를 계산하지 않는다
///   위치를 anchoredPosition 으로 옮기지 않고 <b>형제 순서(sibling index)와 자식 정렬만 바꾼다.</b>
///   실제 배치는 HorizontalLayoutGroup·VerticalLayoutGroup 이 계산하므로,
///   창 배율이 바뀌어도 좌표를 다시 잡을 필요가 없다.
///
/// ■ 가로 정렬은 건드리지 않는다
///   여닫을 때 <b>Column 이 아니라 그 안의 Canvas 만 끄기 때문</b>이다. 열 3개와 (Layout) 스페이서가
///   항상 남아 있어 3열 묶음의 폭이 변하지 않고, 위젯이 6칸 자리에서 움직이지 않는다.
///   Column 을 끄면 남은 열들이 가운데로 다시 몰려 위젯 가로 칸이 무의미해진다 — 그래서 안 끈다.
///
/// ■ 3열 순서 규칙 — 작업슬롯과 창고는 항상 붙어 있다
///   거래를 작업슬롯에서 <b>가장 먼 끝</b>에 두면 창고가 자동으로 사이에 남는다.
///   창고는 작업슬롯에 끌어다 넣는 재료라 드래그 거리가 곧 조작 비용이고,
///   거래는 한 번 갔다 오면 되는 곳이라 멀어도 손해가 적다.
///   → GameDesign/기획/게임UI/README.md 2.1
///
/// ■ ExecuteAlways
///   재생하지 않고 인스펙터에서 6칸을 바꿔 보며 확인하려고 에디터에서도 돈다.
/// </summary>
[ExecuteAlways]
public class WidgetPositionLayout : MonoBehaviour
{
    // ※ 전부 인스펙터 필수 참조다. nullable 경고를 피하려 = null! 로 두고, 미연결은 Apply 에서 경고로 드러낸다.
    //   (SettingsPanelUI 처럼 예외를 던지지 않는 이유 — 이 컴포넌트는 배선 도중인 에디터에서도 돌기 때문이다)
    [CenterHeader("< 열 참조 >")]
    [SerializeField] private RectTransform columns           = null!; // HorizontalLayoutGroup 을 가진 3열의 부모
    [SerializeField] private RectTransform storageColumn     = null!; // 창고 열
    [SerializeField] private RectTransform workstationColumn = null!; // 작업슬롯 열 — 위젯의 가로 칸을 따라간다
    [SerializeField] private RectTransform marketColumn      = null!; // 거래 열

    [CenterHeader("< 작업슬롯 열의 위·아래 슬롯 >")]
    [SerializeField] private RectTransform widgetPanel = null!; // 위젯 — 6칸의 세로가 이 패널의 슬롯을 정한다
    [SerializeField] private RectTransform statePanel  = null!; // 상태 패널(계정 레벨·골드·시스템 아이콘) — 항상 위젯의 반대편 슬롯

    [CenterHeader("< 위젯 위치 >")]
    [SerializeField] private WidgetPosition position = WidgetPosition.LowerCenter;

    // ※ 정렬을 바꿀 대상은 따로 배선하지 않는다 — 위 열 참조 3개에서 VerticalLayoutGroup을 직접 꺼낸다.
    //   예전엔 배열로 따로 받았는데, 인스펙터를 비워 두면 아무 일도 안 일어나면서 원인이 안 보였다.
    //   같은 오브젝트를 두 번 배선할 이유가 없다.
    [CenterHeader("< 위·아래에 따라 바꿀 열의 자식 정렬 >")]
    [SerializeField, Tooltip("위젯이 '위' 칸일 때 열의 자식 정렬 — 내용을 위로 붙여 위젯이 창 위 가장자리에 온다")]
    private TextAnchor upperAlignment = TextAnchor.UpperCenter;

    [SerializeField, Tooltip("위젯이 '아래' 칸일 때 열의 자식 정렬 — 내용을 아래로 붙인다")]
    private TextAnchor lowerAlignment = TextAnchor.LowerCenter;

    public WidgetPosition Position => position;

    /// <summary>
    /// 위젯 위치를 바꾸고 즉시 반영한다 (설정 드롭다운이 호출). 재생 중이면 저장까지 한다.
    /// </summary>
    public void SetPosition(WidgetPosition value)
    {
        position = value;
        Apply();

        // ⚠️ 저장은 재생 중에만 — 이유는 LoadSavedPosition 주석 참조.
        if (Application.isPlaying)
            WindowSettings.SaveInt(WindowSettings.WidgetPositionKey, (int)value);
    }

    // 배선이 끝난 뒤 씬을 열거나 재생을 시작하면 현재 위치를 반영한다 (Unity 메시지)
    private void OnEnable()
    {
        LoadSavedPosition();
        Apply();
    }

    /// <summary>
    /// 저장된 위치를 읽어 온다. 없으면 인스펙터 값을 그대로 쓴다 (공장 초기값).
    ///
    /// <para>
    /// ⚠️ <b>재생 중일 때만 읽는다.</b> 이 컴포넌트는 <c>[ExecuteAlways]</c>라 에디터에서도 도는데,
    /// 거기서 <c>PlayerPrefs</c>를 읽으면 인스펙터로 6칸을 바꿔 보는 순간 저장값이 그것을 덮어써
    /// <b>미리보기가 망가진다.</b> 에디터에서는 인스펙터가 진실이고, 빌드에서는 저장값이 진실이다.
    /// </para>
    /// </summary>
    private void LoadSavedPosition()
    {
        if (!Application.isPlaying)
            return;

        int saved = WindowSettings.LoadInt(WindowSettings.WidgetPositionKey, (int)position);
        position  = (WidgetPosition)Mathf.Clamp(saved, 0, (int)WidgetPosition.LowerRight);
    }

#if UNITY_EDITOR
    // 인스펙터에서 위치를 바꾸면 즉시 반영한다 (Unity 메시지)
    private void OnValidate()
    {
        // OnValidate 안에서 계층을 바꾸면 Unity 가 경고를 낸다 — 다음 에디터 틱으로 미룬다.
        UnityEditor.EditorApplication.delayCall += ApplyIfAlive;
    }

    // delayCall 사이에 오브젝트가 사라졌을 수 있어 살아 있을 때만 적용한다 (OnValidate 의 지연 콜백)
    private void ApplyIfAlive()
    {
        if (this == null)
            return;

        Apply();
    }
#endif

    /// <summary>현재 <see cref="position"/>을 3열 순서와 위·아래 슬롯에 반영한다.</summary>
    public void Apply()
    {
        if (!HasAllReferences())
            return;

        ApplyColumnOrder();
        ApplyVerticalSlot();

        LayoutRebuilder.MarkLayoutForRebuild(columns);
    }

    // 가로 칸(왼쪽·가운데·오른쪽)에 맞춰 세 열의 순서를 정한다
    private void ApplyColumnOrder()
    {
        int workstationIndex = (int)position % 3;                  // 작업슬롯은 위젯의 가로 칸 그대로
        int marketIndex      = workstationIndex == 2 ? 0 : 2;      // 거래는 작업슬롯에서 가장 먼 끝
                                                                   // (가운데면 양끝이 같으므로 기본 배치 — 거래 오른쪽)
        RectTransform[] order = new RectTransform[3];
        order[workstationIndex] = workstationColumn;
        order[marketIndex]      = marketColumn;

        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == null)
                order[i] = storageColumn; // 남은 한 칸이 창고 — 언제나 작업슬롯 옆이 된다
        }

        for (int i = 0; i < order.Length; i++)
        {
            order[i].SetSiblingIndex(i);
        }
    }

    // 세로 칸(위·아래)에 맞춰 위젯과 상태 패널을 서로 반대편 슬롯에 넣는다
    private void ApplyVerticalSlot()
    {
        bool isUpper   = (int)position / 3 == 0;
        int  lastIndex = workstationColumn.childCount - 1;

        widgetPanel.SetSiblingIndex(isUpper ? 0 : lastIndex);
        statePanel.SetSiblingIndex(isUpper ? lastIndex : 0);

        ApplyChildAlignment(isUpper);
    }

    /// <summary>
    /// 위젯이 어느 칸이냐에 따라 <b>3열의 자식 정렬을 뒤집는다</b> (<see cref="ApplyVerticalSlot"/>에서 호출).
    ///
    /// <para>
    /// 내용이 열 높이를 다 채우지 않을 때 <b>남는 공간이 어디로 가는지</b>를 정하는 값이다.
    /// 위젯이 <b>위</b> 칸이면 내용을 위로 붙여 위젯이 창 위 가장자리에 오고, <b>아래</b> 칸이면 반대다.
    /// 값 자체는 인스펙터에서 바꿀 수 있다 — 열 구성이 달라지면 원하는 조합도 달라진다.
    /// </para>
    ///
    /// <para>
    /// 세 열을 <b>전부</b> 바꾼다 — 작업슬롯 열만 뒤집으면 창고·거래의 배너 줄 높이가 어긋난다
    /// (기획 2장 "세 창의 정렬 규칙": 세 배너 줄이 같은 높이에 와야 한다).
    /// </para>
    /// </summary>
    private void ApplyChildAlignment(bool isUpper)
    {
        TextAnchor alignment = isUpper ? upperAlignment : lowerAlignment;

        SetColumnAlignment(storageColumn,     alignment);
        SetColumnAlignment(workstationColumn, alignment);
        SetColumnAlignment(marketColumn,      alignment);
    }

    // 열의 VerticalLayoutGroup 정렬을 바꾼다. 그룹이 없는 열은 건너뛴다 (ApplyChildAlignment에서 호출)
    private static void SetColumnAlignment(RectTransform column, TextAnchor alignment)
    {
        if (column == null)
            return;

        var group = column.GetComponent<VerticalLayoutGroup>();
        if (group != null)
            group.childAlignment = alignment;
    }

    // 필수 참조가 전부 연결됐는지 확인한다 (Apply 진입 가드)
    private bool HasAllReferences()
    {
        RectTransform[] references = { columns, storageColumn, workstationColumn, marketColumn, widgetPanel, statePanel };

        int linked = 0;
        foreach (var reference in references)
        {
            if (reference != null)
                linked++;
        }

        // 일부만 연결된 상태만 경고한다 — 전부 비어 있으면 컴포넌트를 막 붙여 배선 전인 정상 상황이다.
        if (linked > 0 && linked < references.Length)
            ClientLogger.Warn(ClientLogger.UI, "인스펙터 참조가 일부만 연결돼 배치를 건너뛴다.", this);

        return linked == references.Length;
    }
}
