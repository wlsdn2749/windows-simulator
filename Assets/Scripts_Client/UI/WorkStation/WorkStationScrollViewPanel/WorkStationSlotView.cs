using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작업슬롯 한 칸의 표시. 프리팹에 붙는다.
///
/// ■ 스스로 시간을 세지 않는다
///   Update·코루틴을 두지 않고 <see cref="WorkStationScrollViewPanelUI"/>가 계산해 넘겨 준 값만 그린다.
///   상시 실행 앱이라 칸마다 루프를 돌리면 슬롯 수만큼 낭비가 곱해진다.
///
/// ■ 표시는 값의 진실이 아니다
///   카운트다운은 연출이고 판정은 서버가 한다. 어긋나도 다음 스냅샷이 교정한다.
/// </summary>
public class WorkStationSlotView : MonoBehaviour
{
    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("슬롯 번호·산업·캐릭터·속도")]
    private TMP_Text slotText = null!;

    [SerializeField, Tooltip("다음 수확까지 남은 시간")]
    private TMP_Text remainText = null!;

    [SerializeField, Tooltip("판정 진행도 (0~1). 표시 전용이라 interactable은 꺼 둔다")]
    private Slider progressSlider = null!;

    private WorkStationSlotInfo? _slot;

    /// <summary>이 뷰가 그리고 있는 슬롯 번호. 미바인딩이면 -1.</summary>
    public int SlotIndex => _slot?.SlotIndex ?? -1;

    /// <summary>배치돼 있고 속도가 0이 아니어서 카운트다운을 돌릴 수 있는가.</summary>
    public bool IsRunning => _slot != null && _slot.CharacterId != 0 && _slot.CurrentWorkSpeed > 0;

    /// <summary>슬롯 스냅샷을 반영한다 (WorkStationScrollViewPanelUI가 호출).</summary>
    /// <param name="characterName">
    /// 배치된 캐릭터의 표시 이름. <b>뷰가 직접 조회하지 않는다</b> —
    /// <c>slot.CharacterId</c>는 개체 번호라 테이블에서 이름이 안 나오고, 보유 목록을 거쳐야 한다.
    /// 그 변환은 세션을 아는 패널의 몫이다.
    /// </param>
    public void Bind(WorkStationSlotInfo slot, string characterName)
    {
        _slot = slot;

        string industry  = ((GameData.ItemType)slot.Industry).ToString();
        string character = slot.CharacterId != 0 ? characterName : "-";

        if (!IsRunning)
        {
            slotText.text          = $"슬롯 {slot.SlotIndex} · 대기";
            remainText.text        = "배치 없음";
            progressSlider.value   = 0f;
            return;
        }

        // 천분율 → 배율 (1000 = 1.0배)
        float speedMultiplier = slot.CurrentWorkSpeed / 1000f;
        slotText.text = $"슬롯 {slot.SlotIndex} · {industry} · {character} · {speedMultiplier:0.00}배";
    }

    /// <summary>진행도와 남은 시간을 갱신한다 (WorkStationScrollViewPanelUI의 Update가 매 프레임 호출).</summary>
    public void Tick(float progress, float remainSeconds)
    {
        progressSlider.value = progress;
        remainText.text      = $"{remainSeconds:0.0}초 후 수확";
    }
}
