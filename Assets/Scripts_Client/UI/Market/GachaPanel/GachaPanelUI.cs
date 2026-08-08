using MikaNetwork;
using MikaProtocol;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가챠 패널 — 거래 열의 뽑기 화면. 지금은 요청 버튼 두 개(1회 · 10연차)뿐이다.
///
/// <para>
/// ■ 왜 거래 열인가<br/>
/// 가챠는 기획상 <b>상점</b>에 속한다 — 가챠 티켓이 골드 상점 품목이다.
/// → GameDesign/기획/거래/README.md 3.2
/// </para>
///
/// <para>
/// ■ 결과는 여기서 보지 않는다<br/>
/// 뽑힌 보상·인벤토리 반영은 <see cref="PlayerDataManager"/>가 처리하고
/// <c>PlayerDataLogger</c>가 콘솔에 풀어 준다. 이 클래스는 보내는 일만 한다.
/// <b>결과 팝업이 생기면 그것이 이 패널의 자식으로 붙는다</b> — 일감 "가챠 결과 팝업".
/// </para>
/// </summary>
public class GachaPanelUI : MonoBehaviour
{
    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("단차(1회) 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button singleDrawButton = null!;

    [SerializeField, Tooltip("10연차 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button tenDrawButton = null!;

    [CenterHeader("< 설정 >")]
    [SerializeField, Tooltip("뽑을 가챠 풀 Id")]
    private int gachaId = 1;

    private PlayerDataManager _data    = null!;
    private NetworkManager    _network = null!;

    // 참조 확보 → 초기화 순서로 진행한다 (클라 공통 규약. 구독할 이벤트가 없다)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(singleDrawButton, nameof(singleDrawButton));
        this.RequireRef(tenDrawButton,    nameof(tenDrawButton));

        _data    = Services.Get<PlayerDataManager>();
        _network = NetworkManager.Instance;

        singleDrawButton.onClick.AddListener(() => Draw(1));
        tenDrawButton.onClick.AddListener(() => Draw(10));
    }

    /// <summary>
    /// 가챠를 <paramref name="drawCount"/>회 요청한다 (버튼 OnClick에 코드로 연결).
    ///
    /// <para>
    /// 로그인 전에 보내면 <b>서버가 User를 못 찾아 조용히 버린다</b> — 클라 입장에선 응답도 오류도
    /// 없어서 "눌렀는데 아무 일도 안 일어난다"로만 보인다. 보내기 전에 여기서 끊고 이유를 남긴다.
    /// </para>
    /// </summary>
    private void Draw(int drawCount)
    {
        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "가챠 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");
            return;
        }

        _network.Send(new C_GachaDrawRequest
        {
            GachaId   = gachaId,
            DrawCount = drawCount
        });

        ClientLogger.Info(ClientLogger.Send, $"가챠 요청 — 풀={gachaId}, {drawCount}회");
    }
}
