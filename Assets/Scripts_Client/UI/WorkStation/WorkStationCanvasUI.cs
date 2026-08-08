using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작업슬롯 열 — 이 캔버스 안의 두 화면을 <b>서로 갈아 끼우는 손잡이</b>다.
///
/// <para>
/// ■ 두 화면은 같은 자리를 나눠 쓴다<br/>
/// <code>
/// 목록  WorkStationScrollViewPanelUI   칸 8개를 보여 준다
///   │  칸을 누르면
///   ▼
/// 선택  WorkStationSelectPanelUI       그 칸을 배치하거나 해제한다
///   │  확인 · 뒤로
///   ▼
/// 목록
/// </code>
/// 나란히 두지 않고 갈아 끼우는 이유는 <b>열 폭이 좁기 때문</b>이다. 같은 자리를 다 쓰면
/// 드롭다운과 버튼이 열 너비를 그대로 받아 창을 1x로 줄여도 읽힌다.
/// </para>
///
/// <para>
/// ■ 두 패널은 서로를 모른다<br/>
/// 목록은 <c>SlotClicked</c>를, 선택 패널은 <c>Closed</c>를 쏘기만 하고 <b>무엇을 열지는 여기서 정한다.</b>
/// 화면이 늘어나도 참조가 그물처럼 얽히지 않는다 — <c>UIManager</c>가 3열에 대해 하는 일과 같다.
/// </para>
///
/// <para>
/// ⚠️ <b>이 스크립트는 작업슬롯 캔버스에 붙는다 — 열(Column)이 아니다.</b>
/// 열에 붙이면 닫는 순간 같은 열의 자식인 <b>위젯까지 사라져</b> 다시 열 버튼이 없어진다.
/// 캔버스에 두면 닫힐 때 목록 패널도 같이 멈춰 카운트다운 계산이 사라지는 이득도 있다 —
/// 상시 실행 앱에서 안 보이는 것을 계산할 이유가 없다.
/// </para>
/// </summary>
public class WorkStationCanvasUI : MonoBehaviour
{
    [CenterHeader("< 갈아 끼우는 두 패널 >")]
    [SerializeField, Tooltip("슬롯 목록 — WorkStation Scroll View Panel")]
    private WorkStationScrollViewPanelUI slotListPanel = null!;

    [SerializeField, Tooltip("배치/해제 화면 — Select Panel")]
    private WorkStationSelectPanelUI selectPanel = null!;

    // 하단 버튼 줄(기획 2.4) — 본체 안쪽 아래에서 좌우 열을 연다.
    // ※ 선택 참조다. 비워 두면 그 버튼이 없는 것으로 보고 넘어간다.
    [CenterHeader("< Menu Panel 버튼 >")]
    [SerializeField, Tooltip("창고 열기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button? storageButton;

    [SerializeField, Tooltip("거래 열기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button? marketButton;

    private UIManager _ui = null!;
    private bool      _isSubscribed;
    private bool      _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    //
    // ★ 여기서는 UIManager와 달리 시작 화면을 직접 정한다.
    //   UIManager가 다루는 3열은 "무엇을 열어 둘 것인가"가 취향이라 씬 저장 상태를 존중하지만,
    //   이 두 패널은 같은 자리를 나눠 쓰므로 <b>둘 중 하나만 켜져 있어야 한다</b>는 불변식이 있다.
    //   씬에 둘 다 켜진 채(또는 둘 다 꺼진 채) 저장되면 화면이 깨지므로 코드가 못박는다.
    //   슬롯을 골라야 설정 화면으로 갈 수 있으니 시작은 언제나 목록이다.
    private void Start()
    {
        this.RequireRef(slotListPanel, nameof(slotListPanel));
        this.RequireRef(selectPanel,   nameof(selectPanel));

        _ui = Services.Get<UIManager>();

        Subscribe();

        if (storageButton != null)
            storageButton.onClick.AddListener(_ui.ToggleStorage);

        if (marketButton != null)
            marketButton.onClick.AddListener(_ui.ToggleMarket);

        ShowSlotList();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
            Subscribe();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>작업슬롯 본체를 열고 닫는다 (UIManager가 호출).</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }

    #region 구독

    // 두 패널이 보내는 신호를 받는다 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed             = true;
        slotListPanel.SlotClicked += ShowSelect;
        selectPanel.Closed        += ShowSlotList;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed             = false;
        slotListPanel.SlotClicked -= ShowSelect;
        selectPanel.Closed        -= ShowSlotList;
    }

    #endregion

    #region 화면 전환

    /// <summary>
    /// 그 칸의 배치/해제 화면으로 갈아 끼운다 (목록의 SlotClicked 구독).
    /// <b>배치 여부와 상관없이 열린다</b> — 빈 칸이면 배치, 찬 칸이면 해제가 뜬다.
    /// </summary>
    public void ShowSelect(int slotIndex)
    {
        slotListPanel.gameObject.SetActive(false);
        selectPanel.Open(slotIndex); // 켜는 일까지 Open이 한다 — 슬롯 번호가 먼저 들어가야 해서다
    }

    /// <summary>목록으로 되돌린다 (선택 패널의 Closed 구독).</summary>
    public void ShowSlotList()
    {
        selectPanel.gameObject.SetActive(false);
        slotListPanel.gameObject.SetActive(true);
    }

    #endregion
}
