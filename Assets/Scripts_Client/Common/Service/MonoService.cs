using UnityEngine;

// 자기 자신을 역할 타입 T로 Services에 자동 등록/해제하는 MonoBehaviour 베이스.
// 매니저/시스템/컨트롤러의 'X.Inst + Awake{Inst=this}' 보일러플레이트를 통일한다.
//
// ⚠️ 조회는 반드시 Start에서 한다 — Awake·OnEnable 둘 다 안 된다.
//    Unity는 씬을 열 때 오브젝트마다 Awake → OnEnable 을 이어서 부른다. 모든 Awake가 먼저
//    끝나는 것이 아니므로, OnEnable 시점엔 다른 서비스가 아직 등록 전일 수 있다.
//    "모든 Awake가 끝났음"이 보장되는 첫 시점은 Start다.
//    어기면 Services.Get이 KeyNotFoundException을 던지고, 캐시 필드가 null로 남아
//    한참 뒤 사용 지점에서 NullReferenceException으로 다시 터진다.
//
//    구독을 OnEnable/OnDisable에 두고 싶다면 조회만 Start로 분리한다:
//      Start    → Get + 최초 구독
//      OnEnable → 캐시가 있을 때만 재구독 (껐다 켜는 경로)
//      OnDisable→ 구독 해제
//    (Start와 OnEnable이 모두 구독을 시도하므로 중복 구독 플래그로 막는다)
//
// ─────────── T에 무엇을 넣는가 ───────────
// T는 "이 객체를 무엇으로 찾을 것인가"를 정하는 키다. Services가 typeof(T)를 키로 쓰기 때문에,
// 여기 적은 타입으로만 꺼낼 수 있다. 두 가지 쓰임을 모두 허용한다.
//
//   1) 자기 자신으로 등록 — 교체할 일 없는 매니저. 싱글톤 대체 용도.
//        class XxxManager : MonoService<XxxManager>
//        → Services.Get<XxxManager>()
//
//   2) 역할 인터페이스로 등록 — 구현을 갈아끼울 수 있는 AI·시스템.
//        class Person : MonoService<IWalk>     // Person이 IWalk를 구현
//        → Services.Get<IWalk>()
//
// 2번이 이 클래스를 단순 싱글톤과 구분 짓는 지점이다. Person을 Robot으로 교체할 때:
//   - 인터페이스로 등록했으면 → 씬에서 오브젝트만 바꾸면 되고, Get<IWalk>() 호출부는 그대로다.
//   - 자기 자신으로 등록했으면 → Get<Person>()을 Get<Robot>()으로, 호출부를 전부 고쳐야 한다.
// 등록 키가 typeof(T) 하나뿐이라, MonoService<Person>으로 등록하면 Person이 IWalk를
// 구현했더라도 Get<IWalk>()는 키가 없어 실패한다. 둘은 같은 게 아니다.
//
// ─────────── 왜 제네릭 제약이 아니라 런타임 검사인가 ───────────
// 원래 문제: this as T 는 캐스팅 실패 시 null을 반환하는데, 그 null이 그대로 등록됐다.
// 잘못된 T를 막는 방법이 두 가지였다.
//
//   A안(채택) 런타임에 this is T 로 검사하고, 아니면 등록을 포기한다.
//   B안        where T : MonoService<T> (CRTP)로 컴파일 타임에 막는다.
//
// B안이 더 일찍 잡아 주지만, T가 항상 자기 자신이어야 하므로 위 2번이 원천 봉쇄된다.
// 교체 가능성을 위해 만든 클래스에서 그걸 버릴 수는 없어서 A안을 택했다.
// 대신 검사를 Awake로 앞당겨, 잘못 쓰면 그 자리에서 드러나게 한다.
// (검사가 없으면 null이 등록되고, 한참 뒤 Get<T>()를 쓰는 쪽에서 NRE가 터진다 —
//  원인 지점과 증상 지점이 멀어져 추적이 어려워진다.)
public abstract class MonoService<T> : MonoBehaviour where T : class
{
    protected virtual void Awake()
    {
        // T로 캐스팅되지 않으면(= T를 구현하지 않은 타입에 붙였으면) 등록하지 않는다.
        // is 패턴은 성공 시 non-null을 보장하므로 Register의 null 경고도 함께 사라진다.
        if (this is not T service)
        {
            Debug.LogError($"[MonoService] {GetType().Name}은(는) {typeof(T).Name}이(가) 아니라서 등록할 수 없다. " +
                           $"MonoService<{typeof(T).Name}>을 상속하려면 자기 자신이거나 그 타입을 구현해야 한다.", this);
            enabled = false;
            return;
        }

        Services.Register(service);
    }

    protected virtual void OnDestroy()
    {
        // 등록에 실패했으면 해제할 것도 없다.
        if (this is T service)
            Services.Unregister(service);
    }
}
