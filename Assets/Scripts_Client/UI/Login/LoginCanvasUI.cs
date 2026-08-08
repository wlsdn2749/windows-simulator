using UnityEngine;

/// <summary>
/// 로그인 열 — 게임에 들어오면 <b>가장 먼저, 그리고 이것만</b> 보이는 화면.
///
/// <para>
/// ■ 손잡이일 뿐이다<br/>
/// 다른 Canvas 스크립트(<c>StorageCanvasUI</c>·<c>MarketCanvasUI</c>)와 같이 여닫기만 한다.
/// 아이디 입력과 요청 전송은 자식의 <see cref="LoginPanelUI"/>가 맡는다.
/// </para>
///
/// <para>
/// ■ 스스로 닫지 않는다<br/>
/// 닫는 결정은 <c>UIManager</c>가 한다 — 여닫는 곳이 흩어지면 화면이 늘어날 때마다
/// 참조가 그물처럼 얽힌다(<c>UIManager</c> 클래스 주석).
/// </para>
///
/// <para>
/// ⚠️ 이 Canvas는 다른 UI보다 <b>앞에 나와야 한다.</b> 중첩 Canvas는
/// <c>Override Sorting</c>을 켜야 <c>Sorting Order</c>가 먹는다 — 끄면 숫자가 통째로 무시되고
/// 계층 순서로만 그려진다. 켠 Canvas는 <b>자기 GraphicRaycaster</b>도 있어야 클릭이 통한다.
/// </para>
/// </summary>
public class LoginCanvasUI : MonoBehaviour
{
    /// <summary>이 열을 열고 닫는다 (UIManager가 호출).</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }
}
