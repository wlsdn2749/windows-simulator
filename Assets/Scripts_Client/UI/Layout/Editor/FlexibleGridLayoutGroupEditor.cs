using UnityEditor;
using UnityEditor.UI;

/// <summary>
/// FlexibleGridLayoutGroup 전용 인스펙터.
///
/// ■ 왜 필요한가
///   UnityEditor.UI.GridLayoutGroupEditor 는 [CustomEditor(typeof(GridLayoutGroup), true)] 로 등록돼 있다.
///   두 번째 인자 true 가 "자식 클래스까지 이 에디터가 그린다"는 뜻이라, 상속만 해도 그쪽 인스펙터가 잡힌다.
///   그런데 그 에디터는 <b>자기가 아는 항목(패딩·셀 크기·간격·제약 등)만 골라 그리므로</b>,
///   상속하며 추가한 필드는 [SerializeField] 를 붙여도 인스펙터에서 통째로 사라진다.
///   ([Tooltip]·[CenterHeader] 도 필드가 그려지지 않으니 함께 묻힌다)
///   그래서 더 구체적인 이 에디터를 등록해 추가 항목을 직접 덧그린다.
/// </summary>
[CustomEditor(typeof(FlexibleGridLayoutGroup))]
[CanEditMultipleObjects]
public class FlexibleGridLayoutGroupEditor : GridLayoutGroupEditor
{
    // OnEnable에서 채운다. 그 전에 쓰이는 경로가 없어 non-null로 둔다(CS8618 회피).
    private SerializedProperty _cellAspectRatioProperty = null!;

    // 인스펙터가 열릴 때 (Unity 에디터가 호출)
    protected override void OnEnable()
    {
        base.OnEnable();

        _cellAspectRatioProperty = serializedObject.FindProperty("_cellAspectRatio");
    }

    // 인스펙터 본문을 그린다 (Unity 에디터가 호출)
    public override void OnInspectorGUI()
    {
        // 기본 항목보다 먼저 그려야 [CenterHeader] 안내가 컴포넌트 맨 위에 온다.
        // PropertyField 가 필드에 붙은 데코레이터([CenterHeader])까지 함께 그려 준다.
        serializedObject.Update();
        EditorGUILayout.PropertyField(_cellAspectRatioProperty);
        serializedObject.ApplyModifiedProperties();

        base.OnInspectorGUI();
    }
}
