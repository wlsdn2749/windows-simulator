using UnityEngine;

/// <summary>
/// 창고 열 — 3열 중 <b>왼쪽에서 시작하는 재료 쪽</b>. 캐릭터 · 장비 · 자원 · 특성 <b>4탭</b>이 들어간다.
///
/// <para>
/// ■ 지금은 자리만 잡는다<br/>
/// 실제 내용은 아직 <see cref="InventoryPanelUI"/>(자원 탭) 하나뿐이고, 그건 이 열의 자식으로 붙어
/// 자기 갱신을 스스로 한다. 이 클래스는 <b>열 전체를 여닫는 손잡이</b>다 — 탭이 늘어나면
/// 탭 전환이 여기로 들어온다.
/// </para>
///
/// <para>
/// ■ 왜 작업슬롯 옆에 붙어 있는가<br/>
/// 창고는 작업슬롯에 <b>끌어다 넣는 재료</b>라 드래그 거리가 곧 조작 비용이다.
/// 그래서 <c>WidgetPositionLayout</c>이 위젯 위치와 무관하게 창고를 항상 작업슬롯 옆에 둔다.
/// → GameDesign/기획/게임UI/README.md 2.1
/// </para>
/// </summary>
public class StorageCanvasUI : MonoBehaviour
{
    /// <summary>이 열을 열고 닫는다 (UIManager가 호출).</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }
}
