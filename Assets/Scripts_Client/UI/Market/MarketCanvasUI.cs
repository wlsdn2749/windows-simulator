using UnityEngine;

/// <summary>
/// 거래 열 — 거래소 · 상점 <b>2탭</b>이 들어간다. 결과물의 출구(거래소)와 입구(상점)다.
///
/// <para>
/// ■ 지금은 자리만 잡는다<br/>
/// 내용이 하나도 없다. 이 클래스는 <b>열 전체를 여닫는 손잡이</b>이자, 3열 구조가 코드에도
/// 드러나게 하는 자리 표시다. 탭이 붙으면 탭 전환이 여기로 들어온다.
/// </para>
///
/// <para>
/// ⚠️ <b>거래소·상점은 기획 수치가 전부 미정이다.</b> 골드 상점이라는 방향만 잡혀 있고
/// 수수료·한도·품목·가격은 정해지지 않았다 — 임의로 정하지 않는다.
/// → GameDesign/기획/거래/README.md 3.2
/// </para>
/// </summary>
public class MarketCanvasUI : MonoBehaviour
{
    /// <summary>이 열을 열고 닫는다 (UIManager가 호출).</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }
}
