using UnityEditor;
using UnityEngine;

// ── CenterHeaderDrawer : 위 표식을 "실제로 그리는 로직" 역할 ──
//   상속: GUIDrawer(Unity 내부) → DecoratorDrawer(Unity) → CenterHeaderDrawer(내 코드)
//     · DecoratorDrawer : 특정 필드 데이터가 아니라, [Header]·[Space]처럼 "장식"만 그리는 베이스
//   역할: CenterHeaderAttribute 표식을 굵게+가운데+색상+구분선으로 그린다.
//
// [CustomPropertyDrawer(typeof(CenterHeaderAttribute))]
//   → 표식(Attribute)과 그리는 로직(Drawer)을 이어주는 "등록(바인딩)".
//     "CenterHeaderAttribute 표식을 만나면, 그릴 때 이 CenterHeaderDrawer를 써라"는 뜻.
//
// ── 전체 흐름 ──
//   1) 코드에 [CenterHeader("창 상태")] 작성 → 생성자 호출로 Attribute 인스턴스 생성(text 저장)
//   2) Unity가 인스펙터를 그리다 이 표식 발견 → 위 등록표에서 담당 Drawer 조회
//   3) CenterHeaderDrawer를 new 해서 OnGUI() 호출 (내부 attribute 로 1)의 인스턴스에 접근)
//   4) "< 창 상태 >" 를 굵게+코랄+가운데+구분선으로 그림
[CustomPropertyDrawer(typeof(CenterHeaderAttribute))]
public class CenterHeaderDrawer : DecoratorDrawer
{
    private const float TopPadding    = 20f; // 위 섹션과 시각적으로 떨어뜨리는 여백
    private const float LineSpacing   = 4f;  // 텍스트와 아래 구분선 사이 간격
    private const float LineThickness = 1f;  // 구분선 두께
    private const int   FontSize      = 13;  // 헤더 글자 크기(기본 라벨보다 살짝 크게)

    // 헤더 글자 색 — 눈에 띄지만 부담 없는 연한 코랄(다크 테마에서 잘 보인다)
    private static readonly Color HeaderColor = new Color(0.95f, 0.55f, 0.45f);
    // 구분선 색 — 같은 코랄을 옅게(알파 0.35) 깔아 단순 구획용으로만 보이게
    private static readonly Color LineColor   = new Color(0.95f, 0.55f, 0.45f, 0.35f);

    // 인스펙터가 이 헤더를 그릴 때마다(매 리페인트) Unity가 자동 호출한다. position = 그릴 영역.
    public override void OnGUI(Rect position)
    {
        var centerHeader = (CenterHeaderAttribute)attribute; // 짝꿍 표식 인스턴스(text 보유)

        Rect rect = position;
        rect.yMin += TopPadding; // 위 섹션과 간격 띄우기

        // EditorStyles.boldLabel : '굵게(Bold)'를 담당하는 기본 스타일.
        //   이 스타일을 복사(new GUIStyle(...))해서 정렬·크기·색만 덮어쓴다.
        //   → 내부적으로 fontStyle = FontStyle.Bold 가 이미 적용돼 있어 글자가 굵게 나온다.
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter, // 가운데 정렬
            fontSize  = FontSize,                // 글자 크기
        };
        style.normal.textColor = HeaderColor;    // 글자 색(코랄)

        // 양옆을 < > 로 감싸 헤더처럼 보이게 한다. (속성 문자열은 그대로 두고 여기서만 장식)
        string label = $"< {centerHeader.text} >";

        // 라벨은 딱 한 줄 높이만 차지하도록 영역을 잘라서 그린다.
        Rect labelRect = rect;
        labelRect.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(labelRect, label, style);

        // 라벨 바로 아래에 옅은 가로 구분선을 그린다.
        Rect lineRect = new Rect(rect.x, labelRect.yMax + LineSpacing, rect.width, LineThickness);
        EditorGUI.DrawRect(lineRect, LineColor);
    }

    // OnGUI가 차지할 세로 높이를 Unity에 미리 알려준다(레이아웃 계산용, OnGUI보다 먼저 호출됨).
    public override float GetHeight()
    {
        // 위 여백 + 글자 한 줄 + 선 간격 + 구분선 두께를 모두 더한 전체 높이
        return TopPadding + EditorGUIUtility.singleLineHeight + LineSpacing + LineThickness;
    }
}