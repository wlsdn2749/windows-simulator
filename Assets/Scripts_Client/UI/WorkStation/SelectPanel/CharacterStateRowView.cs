using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 목록의 한 줄. 캐릭터 하나의 상태를 보여 주고 배치/해제 버튼을 갖는다.
///
/// <para>
/// ■ 왜 줄마다 스크립트인가<br/>
/// <b>같은 것이 N개 반복되고 각 줄이 서로 다른 캐릭터에 묶이기 때문</b>이다.
/// <see cref="InventorySlotView"/>·<see cref="WorkStationSlotView"/>와 같은 자리다 —
/// 패널이 버튼 N개를 전부 들고 있으면 캐릭터가 늘 때마다 패널을 고쳐야 한다.
/// </para>
///
/// <para>
/// ■ 스스로 보내지 않는다<br/>
/// 눌렸다고 <see cref="AssignClicked"/>만 쏜다. 슬롯 번호도, 고른 산업도, 로그인 여부도
/// 이 줄은 모른다 — 그건 <see cref="WorkStationSelectPanelUI"/>가 아는 것들이다.
/// </para>
///
/// <para>
/// ■ 표시할 값은 받아서 그린다<br/>
/// 캐릭터 이름은 개체 번호로 테이블을 찾아선 안 나오고 보유 목록을 거쳐야 한다.
/// 그 변환은 세션을 아는 패널의 몫이라 <see cref="Bind"/>로 완성된 문구를 받는다.
/// </para>
/// </summary>
public class CharacterStateRowView : MonoBehaviour
{
    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("캐릭터 이름·레벨·적성 등 한 줄 설명")]
    private TMP_Text infoText = null!;

    [SerializeField, Tooltip("배치/해제 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button assignButton = null!;

    [SerializeField, Tooltip("배치 버튼 라벨 — '배치' / '해제'로 바뀐다")]
    private TMP_Text assignLabel = null!;

    /// <summary>이 줄의 배치/해제를 눌렀다 (<see cref="WorkStationSelectPanelUI"/>가 구독).</summary>
    public event Action<CharacterStateRowView>? AssignClicked;

    /// <summary>이 줄이 그리고 있는 캐릭터 개체 번호. 미바인딩이면 0.</summary>
    public long CharacterId { get; private set; }

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 패널의 Start가 Bind를 부르기 전에 이미 연결돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(infoText,     nameof(infoText));
        this.RequireRef(assignButton, nameof(assignButton));
        this.RequireRef(assignLabel,  nameof(assignLabel));

        assignButton.onClick.AddListener(() => AssignClicked?.Invoke(this));
    }

    /// <summary>
    /// 이 줄이 그릴 캐릭터를 정한다 (<see cref="WorkStationSelectPanelUI"/>가 호출).
    ///
    /// <para>
    /// ※ 이 줄은 <b>배치만</b> 한다 — 해제는 3단계 <c>Character Setting Panel</c>의 몫이라
    /// 라벨이 "해제"로 바뀌는 경우가 없다. 라벨을 코드가 쥐고 있는 건 곧 붙을
    /// "적성이 없어 배치 불가" 표시 때문이다 → 일감 B-2.
    /// </para>
    /// </summary>
    /// <param name="characterId">서버가 발급한 개체 번호. 배치 요청에 그대로 실린다</param>
    /// <param name="info">이름·레벨·적성처럼 이미 완성된 표시 문구</param>
    public void Bind(long characterId, string info)
    {
        CharacterId      = characterId;
        infoText.text    = info;
        assignLabel.text = "배치";
    }

    /// <summary>버튼을 잠그거나 푼다 (다른 슬롯에 이미 배치된 캐릭터 등).</summary>
    public void SetAssignable(bool on)
    {
        assignButton.interactable = on;
    }
}
