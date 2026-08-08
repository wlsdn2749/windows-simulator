using TMPro;
using UnityEngine;

/// <summary>
/// 상태 패널 — 계정 이름과 골드를 표시한다.
///
/// <para>
/// 작업슬롯 열의 위·아래 슬롯 중 <b>위젯의 반대편</b>에 들어간다(WidgetPositionLayout).
/// 위치가 바뀌므로 "상단바"가 아니라 담는 것(상태)으로 이름을 붙였다.
/// </para>
///
/// <para>
/// 닉네임은 아직 서버가 돌려주지 않는다. 로그인에 쓴 Id(<see cref="PlayerDataManager.LoginId"/>)를
/// 그대로 보여 주고, 닉네임 패킷이 생기면 그때 바꾼다.
/// </para>
/// </summary>
public class StateCanvasUI : MonoBehaviour
{
    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("계정 이름. 지금은 로그인 Id를 그대로 표시한다")]
    private TMP_Text nickNameText = null!;

    [SerializeField, Tooltip("골드 보유량")]
    private TMP_Text goldText = null!;

    private PlayerDataManager _data = null!;
    private bool              _isSubscribed;
    private bool              _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        // 필수 참조 검증 — 미연결이면 여기서 멈춘다(SettingsPanelUI와 같은 규칙).
        this.RequireRef(nickNameText, nameof(nickNameText));
        this.RequireRef(goldText,     nameof(goldText));

        _data = Services.Get<PlayerDataManager>();
        Subscribe();
        Refresh(); // 이미 통지를 받은 뒤에 켜졌을 수 있다

        _isReady = true;
    }

    /// <summary>상태 패널을 열고 닫는다 (UIManager가 호출). 위젯과 함께 여닫힌다.</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 온 재화 변경을 놓쳤기 때문이다.
    //   캐시는 계속 살아 있으므로 다시 그리기만 하면 즉시 맞는다.
    private void OnEnable()
    {
        if (!_isReady)
            return;

        Subscribe();
        Refresh();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 로그인·재화 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed         = true;
        _data.CurrencyChanged += Refresh;
        _data.LoginCompleted  += OnLoginCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed         = false;
        _data.CurrencyChanged -= Refresh;
        _data.LoginCompleted  -= OnLoginCompleted;
    }

    // 로그인 결과 도착 — 성공했을 때만 이름을 갱신한다 (LoginCompleted 구독)
    private void OnLoginCompleted(bool success)
    {
        if (success)
            Refresh();
    }

    // 이름·골드를 현재 값으로 갱신한다 (CurrencyChanged 구독 · 로그인 시)
    private void Refresh()
    {
        nickNameText.text = string.IsNullOrEmpty(_data.LoginId) ? "-" : _data.LoginId;
        goldText.text     = _data.Gold.ToString("N0"); // 천 단위 구분
    }
}
