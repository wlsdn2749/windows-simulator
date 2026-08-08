using System.Collections;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그인 요청을 보내는 패널.
///
/// <para>
/// ■ 이 한 번이 전부다<br/>
/// 로그인 요청 하나면 서버가 인벤토리·작업슬롯·캐릭터·재화를 연달아 밀어준다.
/// 그것들을 따로 요청할 패킷은 서버에 없다 — 수신 세트는 <see cref="PlayerDataManager"/> 주석 참조.
/// </para>
///
/// <para>
/// ■ 무응답 감시가 여기 있는 이유<br/>
/// <b>서버는 실패해도 응답을 안 보내는 경우가 있다.</b> 같은 Id가 이미 접속 중이면
/// (끊겼는데 서버가 아직 모르는 좀비 세션 포함) 응답도 로그도 없이 요청을 버린다
/// — 깃허브 이슈 #10, <c>UserManager.CreateUser</c>의 pid 중복 분기.
/// 그러면 클라 화면에서는 <b>아무 일도 일어나지 않은 것</b>과 구분되지 않는다.
/// "응답이 없다"를 사용자에게 알릴 주체가 로그인 화면이므로 감시도 여기서 돈다.
/// </para>
///
/// <para>
/// ■ 아이디만 받는다<br/>
/// 서버는 <c>C_LoginRequest</c>에 Id 하나만 받는다 — 비밀번호도 계정 DB도 아직 없다.
/// 그래서 입력창도 하나뿐이고, 검사는 <b>비었는가</b>가 전부다.
/// </para>
///
/// <para>
/// ⚠️ <b>화면을 닫는 시점은 "버튼을 누른 때"가 아니라 "성공 응답이 온 때"다.</b>
/// 누르자마자 닫으면 위의 무응답 상황에서 아무것도 없는 화면에 갇혀 원인을 알 수 없다.
/// </para>
/// </summary>
public class LoginPanelUI : MonoBehaviour
{
    // 로그인 응답을 이만큼 기다려 본다. 넘기면 경고를 남긴다.
    private const float ResponseTimeoutSeconds = 5f;

    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("아이디 입력창. 엔터로도 로그인되도록 코드가 연결한다")]
    private TMP_InputField idInput = null!;

    [SerializeField, Tooltip("로그인 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button loginButton = null!;

    private PlayerDataManager _data    = null!;
    private NetworkManager    _network = null!;
    private UIManager         _ui      = null!;

    // 진행 중인 무응답 감시. 응답이 오거나 다시 로그인하면 취소한다.
    private Coroutine? _timeoutWatch;

    // 마지막으로 보낸 Id. 무응답 로그에 쓴다 — 그 사이 입력창이 바뀌었을 수 있어 따로 들고 있는다.
    private string _sentId = string.Empty;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(idInput,     nameof(idInput));
        this.RequireRef(loginButton, nameof(loginButton));

        _data    = Services.Get<PlayerDataManager>();
        _network = NetworkManager.Instance;
        _ui      = Services.Get<UIManager>();

        Subscribe();

        loginButton.onClick.AddListener(OnLoginButtonClicked);

        // 입력창에서 엔터를 쳐도 눌린 것으로 친다. 아이디 한 줄짜리 화면이라 마우스로 옮겨 갈 이유가 없다.
        idInput.onSubmit.AddListener(_ => OnLoginButtonClicked());

        // 빈 아이디로는 보낼 수 없으므로 버튼을 잠가 둔다. 입력이 생기면 열린다.
        idInput.onValueChanged.AddListener(_ => RefreshButton());
        RefreshButton();

        _isReady = true;
    }

    // 보낼 수 있을 때만 버튼을 연다 (Start · 입력 변경 시 호출)
    // ※ 요청을 보낸 뒤에는 응답이 오거나 감시가 끝날 때까지 잠가 둔다 — 연타로 중복 요청이 나가면
    //   서버가 같은 Id의 두 번째 접속을 응답 없이 버려(이슈 #10) 스스로 무응답을 만든다.
    private void RefreshButton()
    {
        loginButton.interactable = _timeoutWatch == null && !string.IsNullOrWhiteSpace(idInput.text);
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (!_isReady)
            return;

        Subscribe();
        RefreshButton(); // 꺼져 있는 동안 감시가 끊겼을 수 있다 — 입력 상태로 다시 맞춘다
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
        StopTimeoutWatch(); // 꺼진 오브젝트의 코루틴은 Unity가 멈추므로 핸들만 버린다
    }

    #region 구독

    // 로그인 응답 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed         = true;
        _data.LoginCompleted += OnLoginCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed         = false;
        _data.LoginCompleted -= OnLoginCompleted;
    }

    #endregion

    #region 송신

    // 로그인 요청 (loginButton OnClick · 입력창 엔터에 코드로 연결)
    private void OnLoginButtonClicked()
    {
        // 엔터는 버튼 잠금을 지나쳐 들어오므로 여기서 한 번 더 막는다.
        if (!loginButton.interactable)
            return;

        // 앞뒤 공백은 사용자가 의도한 글자가 아니다. 서버에는 다듬은 값을 보낸다.
        string id = idInput.text.Trim();
        if (id.Length == 0)
        {
            ClientLogger.Warn(ClientLogger.UI, "아이디가 비어 있어 로그인 요청을 보내지 않았다.", this);
            return;
        }

        _sentId = id;

        // 서버가 닉네임을 돌려주지 않으므로 보낸 Id를 표시용으로 넘겨 둔다.
        // 수신 전담 매니저는 무엇을 보냈는지 모르기 때문에 보낸 쪽이 알려 줘야 한다.
        _data.SetLoginId(id);

        _network.Send(new C_LoginRequest { Id = id });
        ClientLogger.Info(ClientLogger.Send, $"로그인 요청 — Id={id}");

        StartTimeoutWatch();
        RefreshButton(); // 응답을 기다리는 동안 잠근다
    }

    #endregion

    #region 무응답 감시

    // 감시 시작 — 이전 감시가 있으면 갈아탄다 (로그인 요청 시 호출)
    private void StartTimeoutWatch()
    {
        StopTimeoutWatch();
        _timeoutWatch = StartCoroutine(WatchResponse());
    }

    // 감시 중단 (응답 도착·재요청·비활성화 시 호출)
    private void StopTimeoutWatch()
    {
        if (_timeoutWatch == null)
            return;

        StopCoroutine(_timeoutWatch);
        _timeoutWatch = null;
    }

    // 제한 시간까지 응답이 없으면 경고를 남긴다 (StartTimeoutWatch가 시작)
    private IEnumerator WatchResponse()
    {
        yield return new WaitForSecondsRealtime(ResponseTimeoutSeconds);

        _timeoutWatch = null;

        ClientLogger.Warn(ClientLogger.Network,
            $"로그인 응답이 {ResponseTimeoutSeconds:F0}초 동안 없다 (Id={_sentId}). " +
            $"서버가 안 떠 있거나, 같은 Id가 이미 접속 중일 수 있다(서버 pid 중복 — 이슈 #10).");

        RefreshButton(); // 다시 시도할 수 있게 풀어 준다
    }

    // 응답이 왔으니 감시를 끝낸다. 성공이면 이 화면을 접는다 (PlayerDataManager.LoginCompleted 구독)
    //
    // ※ 닫는 일은 UIManager가 한다 — 여닫는 곳이 흩어지면 화면이 늘어날 때마다 참조가 얽힌다.
    private void OnLoginCompleted(bool success)
    {
        StopTimeoutWatch();

        if (!success)
        {
            // 실패 사유는 아직 이벤트에 실리지 않는다(EResultCode를 매니저가 로그로만 남기고 버린다).
            // 사유를 화면에 띄우려면 이벤트부터 넓혀야 한다 → 일감 "실패 알림 토스트".
            ClientLogger.Warn(ClientLogger.Network, $"로그인 실패 — Id={_sentId}");
            RefreshButton();
            return;
        }

        _ui.ShowLogin(false);
    }

    #endregion
}
