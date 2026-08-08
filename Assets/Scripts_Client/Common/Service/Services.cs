using System;
using System.Collections.Generic;

// 역할 인터페이스 ↔ 구현 인스턴스를 등록/조회하는 경량 서비스 로케이터.
// 하드 싱글톤(X.Inst)을 대체해, 호출자가 구체 클래스가 아닌 '역할'에만 의존하게 한다 (DIP).
public static class Services
{
    private static readonly Dictionary<Type, object> _services = new();

    // 구현을 역할 타입 T로 등록한다 (각 MonoService가 Awake에서 자신을 등록)
    public static void Register<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }

    // 등록 해제 — 현재 등록된 인스턴스가 service일 때만 제거한다 (씬 재로드 시 잔존 참조 방지)
    public static void Unregister<T>(T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out object current) && ReferenceEquals(current, service))
        {
            _services.Remove(typeof(T));
        }
    }

    // 역할 T의 구현을 가져온다 (미등록 시 예외 — 초기화 순서 버그를 즉시 드러낸다)
    //
    // 예외 메시지에 T와 흔한 원인을 적어 둔다. 기본 KeyNotFoundException은
    // "주어진 키가 사전에 없습니다"만 말해서, 어떤 서비스가 없는지도 왜 없는지도 알려 주지 않는다.
    public static T Get<T>() where T : class
    {
        if (!_services.TryGetValue(typeof(T), out object service))
        {
            throw new KeyNotFoundException(
                $"{typeof(T).Name}이(가) Services에 등록돼 있지 않다. " +
                $"씬에 해당 오브젝트가 있는지, 그리고 조회를 Awake·OnEnable이 아니라 Start에서 하는지 확인할 것 " +
                $"(등록 순서는 MonoService 주석 참조).");
        }

        return (T)service;
    }
}
