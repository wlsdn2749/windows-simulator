using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그리드 셀의 가로 : 세로 비율. 가로 길이는 열 개수에 맞춰 자동으로 정해지고, 세로는 이 비율을 따라온다.
/// </summary>
public enum CellAspectRatio
{
    [InspectorName("1 : 1 (정사각형)")]       OneToOne,
    [InspectorName("1.5 : 1 (가로가 1.5배)")] OneAndHalfToOne,
    [InspectorName("1 : 1.5 (세로가 1.5배)")] OneToOneAndHalf,
    [InspectorName("2 : 1 (가로가 2배)")]     TwoToOne,
    [InspectorName("1 : 2 (세로가 2배)")]     OneToTwo,
    [InspectorName("3 : 1 (가로가 3배)")]     ThreeToOne,
    [InspectorName("1 : 3 (세로가 3배)")]     OneToThree,
}

/// <summary>
/// 열 개수를 고정한 채, 자기 너비에 맞춰 셀 크기를 자동으로 역산하는 그리드 레이아웃입니다.
/// Cell Size 는 레이아웃마다 덮어쓰므로 인스펙터 값은 사용되지 않습니다.
/// Constraint 가 Fixed Column Count 일 때만 동작하며, 그 외 모드에서는 GridLayoutGroup 과 동일합니다.
///
/// ※ 인스펙터는 FlexibleGridLayoutGroupEditor 가 그린다. 기본 에디터를 그대로 두면
///   여기서 추가한 필드가 인스펙터에 아예 나오지 않기 때문이다(그쪽 주석 참조).
/// </summary>
[AddComponentMenu("Layout/Flexible Grid Layout Group")]
public class FlexibleGridLayoutGroup : GridLayoutGroup
{
    [CenterHeader("※ GridLayoutGroup 상속을 통해, 확장한 스크립트. 자동 셀 사이즈 조정 포함.")]
    [SerializeField, Tooltip("셀의 가로 : 세로 비율입니다. 가로는 열 개수에 맞춰 자동 계산되고, 세로가 이 비율로 정해집니다.")]
    private CellAspectRatio _cellAspectRatio = CellAspectRatio.OneToOne;

    // 가로 배치 입력 계산 (UGUI 레이아웃 시스템이 호출)
    public override void CalculateLayoutInputHorizontal()
    {
        ResizeCellToFitWidth();

        base.CalculateLayoutInputHorizontal();
    }

    // 가로 배치 확정 — 이 시점의 rect.width 가 최종 너비다 (UGUI 레이아웃 시스템이 호출)
    public override void SetLayoutHorizontal()
    {
        ResizeCellToFitWidth();

        base.SetLayoutHorizontal();
    }

    /// <summary>
    /// 현재 너비에서 좌우 패딩과 열 사이 간격을 뺀 나머지를 열 수로 나눠 셀 가로 길이를 정하고,
    /// 세로는 지정한 비율로 맞춥니다.
    /// </summary>
    private void ResizeCellToFitWidth()
    {
        // 열 수가 고정되지 않은 모드는 셀 크기가 배치의 입력이므로 건드리지 않는다
        if (m_Constraint != Constraint.FixedColumnCount)
        {
            return;
        }

        int columnCount = Mathf.Max(1, m_ConstraintCount);
        // 간격은 열과 열 사이에만 들어가므로 (열 수 - 1) 개다
        float usableWidth = rectTransform.rect.width - padding.horizontal - spacing.x * (columnCount - 1);
        float cellWidth   = Mathf.Max(0f, usableWidth / columnCount);

        // cellSize 프로퍼티는 SetDirty 를 유발해 레이아웃 재계산 중에 쓰면 위험하므로 필드에 직접 대입한다
        m_CellSize = new Vector2(cellWidth, cellWidth * GetHeightPerWidth());
    }

    /// <summary>가로 1 에 대한 세로 배수를 돌려줍니다.</summary>
    private float GetHeightPerWidth()
    {
        return _cellAspectRatio switch
        {
            CellAspectRatio.OneToOne        => 1f,
            CellAspectRatio.OneAndHalfToOne => 1f / 1.5f,
            CellAspectRatio.OneToOneAndHalf => 1.5f,
            CellAspectRatio.TwoToOne        => 1f / 2f,
            CellAspectRatio.OneToTwo        => 2f,
            CellAspectRatio.ThreeToOne      => 1f / 3f,
            CellAspectRatio.OneToThree      => 3f,
            _                               => 1f,
        };
    }
}
