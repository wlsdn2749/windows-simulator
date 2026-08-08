using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 부모의 높이에 맞춰 <b>정사각형이 되는 가로 길이</b>를 레이아웃에 알려 줍니다.
/// 가로 줄(HorizontalLayoutGroup) 안의 아이콘 버튼처럼 "높이만큼 넓으면 되는" 칸에 붙입니다.
///
/// <para>
/// ■ 왜 컴포넌트가 필요한가<br/>
/// UGUI는 <b>가로를 먼저 다 정하고 세로를 정한다</b>(CalculateLayoutInputHorizontal → SetLayoutHorizontal
/// → CalculateLayoutInputVertical → SetLayoutVertical). 그래서 "내 높이만큼 넓게"를 기본 컴포넌트로는
/// 표현할 수 없다 — 가로를 정할 시점에 자기 높이가 아직 없다.
/// </para>
///
/// <para>
/// ■ 그래서 부모에게 묻는다<br/>
/// 자기 높이 대신 <b>부모의 높이</b>를 본다. 부모가 <see cref="LayoutElement.preferredHeight"/>로
/// 높이를 못박아 뒀으면 그 값을 쓴다 — 이건 가로 패스에서도 이미 확정된 값이라 흔들리지 않는다.
/// 못박지 않았으면 부모의 현재 rect 높이를 쓴다(창 크기가 바뀐 직후 한 프레임 늦을 수 있다).
/// </para>
///
/// <para>
/// ※ 같은 오브젝트에 <see cref="LayoutElement"/>를 함께 두지 않는다. 둘 다 가로를 주장해
/// 어느 쪽이 이기는지가 <c>layoutPriority</c>에 좌우된다.
/// </para>
/// </summary>
[AddComponentMenu("Layout/Square Layout Element")]
[RequireComponent(typeof(RectTransform))]
public class SquareLayoutElement : UIBehaviour, ILayoutElement
{
    [CenterHeader("※ 부모 높이만큼의 가로를 요구해 정사각형을 만든다")]
    [SerializeField, Tooltip("정사각형에서 더하거나 뺄 여백. 음수면 그만큼 작아진다")]
    private float sizeAdjust = 0f;

    // ─── ILayoutElement — 가로만 주장한다. 세로는 부모 레이아웃이 정하는 대로 둔다 ───
    public float minWidth       => -1f;
    public float preferredWidth => CalculateSide();
    public float flexibleWidth  => 0f; // 남는 폭을 받으면 정사각형이 깨진다

    public float minHeight       => -1f;
    public float preferredHeight => -1f;
    public float flexibleHeight  => -1f;

    // LayoutElement의 기본 우선순위(1)보다 높게 둬서, 같이 붙어 있어도 이쪽이 이긴다
    public int layoutPriority => 2;

    // 값을 그때그때 계산하므로 미리 모아 둘 것이 없다 (UGUI 레이아웃 시스템이 호출)
    public void CalculateLayoutInputHorizontal() { }
    public void CalculateLayoutInputVertical() { }

    /// <summary>한 변의 길이 — 부모 높이에서 부모의 위아래 패딩을 뺀 값.</summary>
    private float CalculateSide()
    {
        var parent = transform.parent as RectTransform;
        if (parent == null)
            return -1f; // 부모가 없으면 주장하지 않는다

        float height = parent.rect.height;

        // 부모가 높이를 못박아 뒀으면 그 값이 더 믿을 만하다 — 가로 패스에서 이미 확정돼 있다.
        var parentElement = parent.GetComponent<LayoutElement>();
        if (parentElement != null && parentElement.preferredHeight >= 0f)
            height = parentElement.preferredHeight;

        // 부모가 가로/세로 레이아웃 그룹이면 위아래 패딩만큼은 내 몫이 아니다
        var group = parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (group != null)
            height -= group.padding.vertical;

        return Mathf.Max(0f, height + sizeAdjust);
    }

    #region 다시 계산해야 할 때

    // 크기가 바뀌면 부모 줄을 다시 재게 한다 (Unity 메시지)
    protected override void OnRectTransformDimensionsChange() => MarkDirty();

    // 켜질 때 · 부모가 바뀔 때도 다시 잰다 (Unity 메시지)
    protected override void OnEnable()            { base.OnEnable();            MarkDirty(); }
    protected override void OnTransformParentChanged() { base.OnTransformParentChanged(); MarkDirty(); }
    protected override void OnDidApplyAnimationProperties() => MarkDirty();

#if UNITY_EDITOR
    // 인스펙터에서 값을 바꿔 보며 확인할 수 있게 (Unity 에디터가 호출)
    protected override void OnValidate() { base.OnValidate(); MarkDirty(); }
#endif

    private void MarkDirty()
    {
        if (!IsActive())
            return;

        // 내가 아니라 부모 줄이 다시 계산돼야 내 폭이 반영된다
        var parent = transform.parent as RectTransform;
        LayoutRebuilder.MarkLayoutForRebuild(parent != null ? parent : (RectTransform)transform);
    }

    #endregion
}
