using MikaNetwork;
using MikaProtocol;
using UnityEngine;

/// <summary>
/// 서버와의 연결이 <b>살아 있는지</b>를 주기적으로 확인한다 (Ping/Pong).
///
/// <para>
/// ■ 왜 필요한가<br/>
/// TCP는 끊김을 즉시 알려주지 않는다. 소켓이 FIN/RST 없이 사라지는 종료
/// (유니티 플레이 중지·PC 절전·랜선 분리)에서는 <b>양쪽 다 상대가 죽은 걸 모른다.</b>
/// 서버는 그동안 접속 중으로 보고 채취를 계속 굴리고(깃허브 이슈 #10 — 좀비 유저),
/// 클라는 화면이 멀쩡해 보이는데 아무것도 반영되지 않는다.
/// </para>
///
/// <para>
/// ■ 이 클래스가 하는 것 / 하지 않는 것<br/>
/// 5초마다 Ping을 보내고 Pong이 돌아오는지 본다. 15초 넘게 무응답이면 <b>로그로 알린다.</b><br/>
/// 소켓을 끊지는 않는다 — 세션은 서버 담당 코드(<c>MikaClient</c>)의 것이라 여기서 손대지 않는다.
/// <b>좀비 세션을 실제로 정리하는 것은 서버 몫</b>이고(이슈 #10의 서버 파트), 이건 그 절반이다.
/// </para>
///
/// <para>
/// ■ 왜 송신이 여기 남아 있는가<br/>
/// 다른 요청 패킷은 전부 그 요청을 일으킨 UI가 직접 보낸다. Ping만 예외인데,
/// <b>사용자 조작이 아니라 연결 수명 관리</b>라서 누를 버튼도 보여 줄 화면도 없기 때문이다.
/// </para>
/// </summary>
public class PingManager : MonoService<PingManager>
{
    // Ping 주기. 짧을수록 끊김을 빨리 알지만 그만큼 패킷이 늘어난다.
    private const float PingIntervalSeconds = 5f;

    // 이 시간을 넘겨 Pong이 없으면 끊긴 것으로 본다.
    // ★ 서버가 같은 판정을 넣으면 이 값이 곧 "부당하게 적립되는 최대 채취 시간"이 된다.
    //   채취 1주기(30초)보다 짧게 잡아 손실 구간이 생기지 않게 했다.
    private const float ResponseTimeoutSeconds = 15f;

    // ─── 참조 캐시 ───
    private PlayerDataManager _playerData = null!; // 없으면 게임이 성립하지 않는다
    private NetworkManager    _network    = null!;

    // ─── 내부 상태 ───
    private float _nextPingTime;
    private float _lastPongTime;
    private bool  _isRunning;
    private bool  _isTimedOut;   // 무응답 로그를 1회만 남기기 위한 상태 (매 프레임 도배 방지)
    private bool  _isSubscribed;
    private bool  _isReady;      // Start 완료 여부 — OnEnable 재구독 가드

    /// <summary>
    /// 서버가 하트비트에 응답하고 있는가.
    /// ⏸ 아직 읽는 곳이 없다 — 연결 상태를 표시하는 위젯이 붙을 자리다.
    /// </summary>
    public bool IsServerResponding => !_isTimedOut;

    // ─── Unity 메시지 ───

    // 참조 확보 → 구독 순서로 진행한다 (매니저 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        CacheReferences();
        Subscribe();
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
        _isRunning = false;
    }

    // 하트비트 주기 검사 (Unity 메시지)
    private void Update()
    {
        if (!_isRunning)
            return;

        // 타임스케일을 0으로 만들어도(일시정지 연출 등) 연결은 계속 살아 있어야 한다.
        float now = Time.unscaledTime;

        if (now >= _nextPingTime)
        {
            _network.Send(new C_PingRequest());
            _nextPingTime = now + PingIntervalSeconds;
        }

        WarnIfSilentTooLong(now);
    }

    #region 초기화

    // 다른 서비스를 확보해 캐시한다 (Start에서 호출)
    private void CacheReferences()
    {
        _playerData = Services.Get<PlayerDataManager>();
        _network    = NetworkManager.Instance;
    }

    // Pong 수신 + 로그인 완료 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed = true;

        ServerPacketHandler.PongReceived += OnPongReceived;
        _playerData.LoginCompleted       += OnLoginCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed = false;

        ServerPacketHandler.PongReceived -= OnPongReceived;
        _playerData.LoginCompleted       -= OnLoginCompleted;
    }

    #endregion

    #region 하트비트 진행

    // 로그인 결과 도착 — 성공했을 때만 하트비트를 시작한다 (PlayerDataManager.LoginCompleted 구독)
    // 로그인 전에는 서버에 User가 없어 감시할 대상 자체가 없다.
    private void OnLoginCompleted(bool isSuccess)
    {
        if (!isSuccess)
            return;

        float now = Time.unscaledTime;

        _lastPongTime = now; // 시작하자마자 무응답으로 판정되지 않도록 기준을 지금으로 잡는다
        _nextPingTime = now;
        _isTimedOut   = false;
        _isRunning    = true;

        ClientLogger.Info(ClientLogger.Network,
            $"하트비트 시작 — {PingIntervalSeconds:F0}초마다 확인, {ResponseTimeoutSeconds:F0}초 무응답이면 경고");
    }

    // Pong 도착 — 마지막 응답 시각을 갱신한다 (ServerPacketHandler.PongReceived 구독)
    // 평시엔 로그를 남기지 않는다. 5초마다 찍으면 정작 봐야 할 로그가 밀려난다.
    private void OnPongReceived()
    {
        float now = Time.unscaledTime;

        // 끊겼다고 알렸던 연결이 돌아왔을 때만 말한다 — 상태가 바뀐 순간이 유일하게 알릴 가치가 있다.
        if (_isTimedOut)
        {
            ClientLogger.Info(ClientLogger.Network, $"서버 응답 복구 — {now - _lastPongTime:F0}초 만에 돌아왔다");
            _isTimedOut = false;
        }

        _lastPongTime = now;
    }

    // 무응답이 판정 시간을 넘겼는지 검사한다 (Update에서 호출)
    private void WarnIfSilentTooLong(float now)
    {
        if (_isTimedOut)
            return;

        float silentSeconds = now - _lastPongTime;
        if (silentSeconds < ResponseTimeoutSeconds)
            return;

        _isTimedOut = true;

        ClientLogger.Error(ClientLogger.Network,
            $"서버 무응답 {silentSeconds:F0}초 — 연결이 끊긴 것으로 본다. " +
            $"지금 화면의 인벤토리·슬롯은 서버 상태와 다를 수 있다(재접속 필요).");
    }

    #endregion
}
