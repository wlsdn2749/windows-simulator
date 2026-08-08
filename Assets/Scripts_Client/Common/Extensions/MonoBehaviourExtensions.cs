using UnityEngine;

/// <summary>MonoBehaviour 공용 확장 메서드 모음.</summary>
public static class MonoBehaviourExtensions
{
    /// <summary>
    /// 인스펙터 "필수" 참조를 검증한다(fail-fast). 미연결(null)이면 어떤 컴포넌트의 어떤 필드인지
    /// 명시해 MissingReferenceException 을 던진다 — 조용히 넘어가지 않아 원인 파악이 쉽다.
    /// </summary>
    /// <param name="owner">검증을 요청한 컴포넌트(this). 예외 메시지에 타입명으로 들어간다.</param>
    /// <param name="reference">
    /// 검증할 직렬화 참조. 파라미터 타입을 제네릭 T 가 아니라 UnityEngine.Object 로 둔 이유:
    /// 제네릭 T 의 == null 은 참조 동등성이라 Unity 의 "가짜 null"(미연결·파괴된 오브젝트)을 놓친다.
    /// Object 로 받으면 UnityEngine.Object 의 == 오버로드가 걸려 가짜 null 까지 정확히 잡힌다.
    /// </param>
    /// <param name="fieldName">참조 필드명. 보통 nameof(...) 로 넘긴다.</param>
    public static void RequireRef(this MonoBehaviour owner, Object? reference, string fieldName)
    {
        if (reference == null)
        {
            throw new MissingReferenceException
            (
                $"[{owner.GetType().Name}] '{fieldName}' 참조가 인스펙터에 연결되지 않았습니다. 연결 후 다시 실행하세요."
            );
        }
    }
}
