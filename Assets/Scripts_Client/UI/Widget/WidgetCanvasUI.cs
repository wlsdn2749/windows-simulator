using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상주 위젯 — <b>바탕화면에 항상 떠 있는 유일한 것</b>이다. 3열 큰 창은 필요할 때만 연다.
///
/// <para>
/// ■ 그래서 여닫는 API가 없다<br/>
/// 다른 패널과 달리 <c>Show</c>를 두지 않았다. 이걸 끄면 게임이 화면에서 사라진다 —
/// 끄고 켜는 대상이 아니라 <b>기준점</b>이다. 위젯은 작업슬롯 열의 위·아래 슬롯 중 한 칸에 들어가고,
/// 반대편 칸에 상태 패널이 들어간다(<c>WidgetPositionLayout</c>).
/// </para>
///
/// <para>
/// ■ 지금은 자리만 잡는다. 들어올 것:<br/>
/// 상단 = 골드 · 가동 슬롯 · 시간당 산출 · 누적 수확<br/>
/// 스트립 = 미니 슬롯(캐릭터 + 수확 표시 + 게이지만. 텍스트 라벨은 넣지 않는다)<br/>
/// <c>열기/닫기</c> 버튼 = 작업슬롯을 열고, 열려 있으면 위젯만 남기고 접는다 → <c>UIManager.ToggleAll</c>
/// </para>
///
/// <para>
/// ⚠️ <b>연출을 여기서 돌리지 않는다.</b> 배경·캐릭터 모션은 큰 창 전용이다.
/// 상시 실행 앱에서 리소스는 기능이 아니라 <b>생존 조건</b>이고, 급격한 애니메이션은
/// P1(주의를 뺏지 않는다)을 정면으로 어긴다.
/// → GameDesign/기획/게임UI/README.md 2.1
/// </para>
/// </summary>
public class WidgetCanvasUI : MonoBehaviour
{
    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("열기/닫기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button toggleButton = null!;

    // 아직 표시할 내용(골드·가동 슬롯·미니 슬롯 스트립)은 붙지 않았다. 지금은 여닫기 배선만 한다.

    // 참조 확보 → 배선 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(toggleButton, nameof(toggleButton));

        var ui = Services.Get<UIManager>();
        toggleButton.onClick.AddListener(ui.ToggleAll);
    }
}
