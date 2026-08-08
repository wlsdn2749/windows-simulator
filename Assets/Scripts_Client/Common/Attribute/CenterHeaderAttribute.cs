using UnityEngine;

// ── CenterHeaderAttribute : 인스펙터 헤더 "표식(데이터)" 역할 ──
//   상속: System.Attribute(C#) → PropertyAttribute(Unity) → CenterHeaderAttribute(내 코드)
//     · System.Attribute  : 모든 C# 어트리뷰트의 뿌리
//     · PropertyAttribute : 인스펙터에서 전용 Drawer로 그려질 수 있는 어트리뷰트의 베이스
//                           ([Header]·[Range] 등도 이 형제) → 이걸 상속해야 Unity가 인식함
// 
//   역할: "여기에 헤더가 있고 문구는 이것" 이라는 표식만 든다. 그리는 법은 모름.
//         (그리는 로직은 짝꿍 CenterHeaderDrawer 담당 / 둘은 [CustomPropertyDrawer]로 묶임)
//
// ※ 기본 [Header]는 좌측 정렬만 되므로, 가운데 정렬용으로 따로 만든 것.
public class CenterHeaderAttribute : PropertyAttribute
{
    public readonly string text; // 헤더에 표시할 문구

    // 생성자(이름은 클래스명과 반드시 일치).
    // [CenterHeader("...")]를 다는 순간 1회 자동 호출되어 동작.
    // 괄호 안 문자열을 text에 저장한다. (이 값은 디스크에 저장되지 않고 코드에서 매번 다시 생성됨)
    public CenterHeaderAttribute(string text)
    {
        this.text = text;
    }
}
