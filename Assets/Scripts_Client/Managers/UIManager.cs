using UnityEngine;

/// <summary>
/// 화면 골격의 단일 출입구 — 3열 + 위젯을 참조로 들고, 무엇을 열고 닫을지 결정한다.
///
/// <para>
/// ■ 왜 필요한가<br/>
/// 패널을 여는 쪽(위젯의 <c>↑</c> 버튼, 작업슬롯 하단의 창고·거래 버튼)과 열리는 쪽이 서로를
/// 직접 알면 화면이 늘어날 때마다 참조가 그물처럼 얽힌다. <b>여는 결정은 여기 하나로 모은다.</b>
/// </para>
///
/// <para>
/// ■ 시작할 때 아무것도 건드리지 않는다<br/>
/// 초기 표시 상태는 <b>씬에 저장된 그대로</b>다. 여기서 켜고 끄면 씬을 열어 본 모습과
/// 재생했을 때가 달라져, 디자이너가 씬에서 확인한 것을 믿을 수 없게 된다.
/// </para>
///
/// <para>
/// ■ 진입 순서 (기획 2.0)<br/>
/// <code>
/// 위젯 [열기/닫기] ──→ 작업슬롯 본체 + 상태 패널
///                        └─ 하단 버튼 ──→ 창고 / 거래
/// 위젯 [열기/닫기] 다시 ──→ 위젯만 남기고 전부 닫힘
/// </code>
/// 창고·거래는 <b>위젯에서 직접 열지 않는다.</b> 작업슬롯을 거쳐야 재료→작업→시장 동선이 유지된다.
/// → GameDesign/기획/게임UI/README.md 2.0
/// </para>
/// </summary>
public class UIManager : MonoService<UIManager>
{
    // ※ 여닫는 대상은 전부 Canvas다 — Column이 아니다.
    //   Column을 끄면 남은 열들이 Horizental Columns 안에서 가운데로 다시 몰려 위젯 가로 칸이 어긋난다.
    //   Canvas만 끄면 Column 3개와 (Layout) 스페이서가 남아 폭이 그대로라 위젯이 제자리에 있는다.
    [CenterHeader("<메인 3개 캔버스 >")]
    [SerializeField, Tooltip("창고 열의 본체 — 3 Storage Column 안의 Storage Canvas")]
    private StorageCanvasUI storageCanvas = null!;

    [SerializeField, Tooltip("작업슬롯 열의 본체 — 3 WorkStation Column 안의 Workstation Canvas")]
    private WorkStationCanvasUI workStationCanvas = null!;

    [SerializeField, Tooltip("거래 열의 본체 — 3 Market Column 안의 Market Canvas")]
    private MarketCanvasUI marketCanvas = null!;

    [CenterHeader("< 사이드 2개 캔버스 >")]
    [SerializeField, Tooltip("상태 캔버스 — 위젯의 반대편. 작업슬롯과 함께 여닫힌다")]
    private StateCanvasUI stateCanvas = null!;

    [SerializeField, Tooltip("바탕화면에 항상 떠 있는 위젯. 여닫지 않고 참조만 들고 있는다")]
    private WidgetCanvasUI widgetCanvas = null!;

    // ※ 로그인은 3열·위젯과 다른 축이다 — 게임에 들어오기 전까지 이것만 보이고, 성공하면 다시 안 나온다.
    //   그래서 ToggleAll·CloseAllExceptWidget의 대상에 넣지 않는다.
    [CenterHeader("< 로그인 캔버스 >")]
    [SerializeField, Tooltip("로그인 열. 다른 UI보다 앞에 오도록 Override Sorting을 켜고 Sorting Order를 크게 준다")]
    private LoginCanvasUI loginCanvas = null!;

    // ─── 참조 ───
    public StorageCanvasUI     Storage     => storageCanvas;
    public WorkStationCanvasUI WorkStation => workStationCanvas;
    public MarketCanvasUI      Market      => marketCanvas;
    public WidgetCanvasUI      Widget      => widgetCanvas;

    /// <summary>위젯 말고 하나라도 열려 있는가. <see cref="ToggleAll"/>의 방향을 정한다.</summary>
    public bool IsOpen =>
        workStationCanvas.gameObject.activeSelf ||
        stateCanvas.gameObject.activeSelf       ||
        storageCanvas.gameObject.activeSelf     ||
        marketCanvas.gameObject.activeSelf;

    // 필수 참조 검증 (Unity 메시지)
    // ※ 이 매니저는 다른 서비스를 조회하지 않는다 — 인스펙터 참조만 쓰므로 확보 단계가 없다.
    // ※ 여기서 패널을 켜거나 끄지 않는다 — 시작 화면은 씬에 저장된 그대로여야
    //   씬에서 확인한 모습과 재생했을 때가 같다.
    private void Start()
    {
        this.RequireRef(storageCanvas,     nameof(storageCanvas));
        this.RequireRef(workStationCanvas, nameof(workStationCanvas));
        this.RequireRef(stateCanvas,       nameof(stateCanvas));
        this.RequireRef(marketCanvas,      nameof(marketCanvas));
        this.RequireRef(widgetCanvas,      nameof(widgetCanvas));
        this.RequireRef(loginCanvas,       nameof(loginCanvas));
    }

    /// <summary>
    /// 위젯의 열기/닫기 버튼이 부른다 — <b>열려 있으면 전부 접고, 닫혀 있으면 작업슬롯을 연다.</b>
    /// 진입 순서(위젯 → 작업슬롯 → 창고·거래)의 되돌아오는 길이라, 어느 단계에서 눌러도 한 번에 접힌다.
    /// </summary>
    public void ToggleAll()
    {
        if (IsOpen)
            CloseAllExceptWidget();
        else
            OpenWorkStation();
    }

    /// <summary>작업슬롯 본체와 상태 패널을 연다. 창고·거래는 작업슬롯의 하단 버튼으로 연다.</summary>
    public void OpenWorkStation()
    {
        workStationCanvas.Show(true);
        stateCanvas.Show(true);
    }

    /// <summary>위젯을 뺀 전부를 닫는다. <b>위젯은 상주가 존재 이유라 건드리지 않는다.</b></summary>
    public void CloseAllExceptWidget()
    {
        storageCanvas.Show(false);
        marketCanvas.Show(false);
        workStationCanvas.Show(false);
        stateCanvas.Show(false);
    }

    /// <summary>창고 열을 열고 닫는다.</summary>
    public void ShowStorage(bool on) => storageCanvas.Show(on);

    /// <summary>거래 열을 열고 닫는다.</summary>
    public void ShowMarket(bool on) => marketCanvas.Show(on);

    /// <summary>
    /// 창고 열을 뒤집는다 (작업슬롯 하단 버튼).
    /// <b>토글이어야 하는 이유</b> — 여는 일만 하면 이미 열려 있을 때 눌러도 아무 변화가 없어
    /// 버튼이 고장 난 것처럼 보인다. 닫는 버튼을 따로 두지 않아도 된다.
    /// </summary>
    public void ToggleStorage() => ShowStorage(!storageCanvas.gameObject.activeSelf);

    /// <summary>거래 열을 뒤집는다 (작업슬롯 하단 버튼).</summary>
    public void ToggleMarket() => ShowMarket(!marketCanvas.gameObject.activeSelf);

    /// <summary>
    /// 로그인 열을 열고 닫는다 (<c>LoginPanelUI</c>가 로그인 성공 응답을 받고 부른다).
    ///
    /// <para>
    /// ⚠️ <b>버튼을 누른 시점이 아니라 성공 응답이 온 시점에 닫는다.</b> 서버는 같은 Id가 이미
    /// 접속 중이면 응답도 로그도 없이 요청을 버린다(이슈 #10). 누르자마자 닫으면 그때
    /// 아무것도 없는 화면에 갇혀 원인을 알 수 없다.
    /// </para>
    /// </summary>
    public void ShowLogin(bool on) => loginCanvas.Show(on);
}
