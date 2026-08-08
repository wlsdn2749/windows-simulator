---
name: feature-design
description: 기능 설계 규칙. 새 기능/클래스/시스템 구현 요청 시 이 규칙을 따른다.
---

> 최종 업데이트: 2026-07-17

# 기능 설계 가이드

---

## 0. 적용 원칙 (먼저 판단)

> **규모와 상황을 먼저 판단한다.**
> 
> 작은 기능, 일회성 스크립트, 프로토타입에는 아래 원칙을 모두 적용할 필요가 없다.
> 
> 오버엔지니어링이 단순한 코드보다 나쁘다 — 필요할 때만 도입한다.

| 규모 | 가이드 |
|------|--------|
| 소규모 (단일 스크립트, 간단한 기능) | 명명·포맷만 지키고 나머지 생략 가능 |
| 중규모 (여러 클래스 협력, 재사용 예정) | SOLID·OOP 체크, 패턴 적용 검토 |
| 대규모 (시스템 전체, 팀 협업) | 모든 원칙 적극 적용, 테스트 필수 |

---

## 1. 구현 전 체크리스트

```
□ 기존 코드베이스에 재사용 가능한 것이 있는가?
□ 클래스가 하나의 책임만 갖는가? (SRP)
□ 추상화/인터페이스가 필요한가? (OCP, DIP)
□ 적용할 수 있는 디자인 패턴이 있는가?
□ 향후 확장성을 고려했는가? (OCP)
□ 테스트 가능한 구조로 설계했는가? (의존성 주입, 인터페이스 분리)
```

---

## 2. SOLID 원칙 빠른 참조

> 상세 구현 및 예시 코드:
> https://github.com/JeongTaeWoong99/Mini_Project/blob/main/Assets/Project/2_Tutorials/LevelUpYourCode/README.md


| 원칙 | 한 줄 요약 | 위반 신호 |
|------|-----------|----------|
| **SRP** | 클래스는 변경 이유가 하나여야 함 | 클래스가 500줄 이상, 여러 관심사 혼합 |
| **OCP** | 확장엔 열려있고, 수정엔 닫혀있음 | 새 기능마다 기존 if/switch 추가 |
| **LSP** | 자식은 부모를 완전 대체 가능 | override 후 동작이 달라지거나 예외 발생 |
| **ISP** | 불필요한 메서드 구현 강요 금지 | 인터페이스에 빈 메서드 구현 |
| **DIP** | 구체 클래스가 아닌 추상에 의존 | `new ConcreteClass()` 직접 생성이 많음 |

```csharp
// DIP 예시 — 구체 클래스 대신 인터페이스에 의존
// Bad
public class EnemyAI
{
    private PathfinderAStar _pathfinder = new PathfinderAStar(); // 구체 클래스에 의존
}

// Good
public class EnemyAI
{
    private readonly IPathfinder _pathfinder;
    public EnemyAI(IPathfinder pathfinder) { _pathfinder = pathfinder; } // 추상에 의존
}
```

---

## 3. 디자인 패턴 빠른 참조

> 상세 구현 및 예시 코드:
> https://github.com/JeongTaeWoong99/Mini_Project/blob/main/Assets/Project/2_Tutorials/LevelUpYourCode/README.md

### 생성 패턴

| 패턴 | 언제 사용 | 신호 |
|------|----------|------|
| **Factory** | 객체 생성 로직 분리 | `if (type == "A") new A(); else new B();` 반복 |
| **Object Pool** | 잦은 생성/파괴 오브젝트 | 총알, 이펙트, 파티클 |
| **Singleton** | 전역 매니저 — **하드 싱글톤(`X.Inst`) 대신 아래 서비스 로케이터를 쓴다** | GameManager, AudioManager |

### 행동 패턴

| 패턴 | 언제 사용 | 신호 |
|------|----------|------|
| **Command** | Undo/Redo, 입력 처리 분리 | 되돌리기 필요, 입력을 큐에 저장 |
| **State** | 복잡한 상태 분기 | if-else/enum 상태 분기가 길어짐 |
| **Observer** | 이벤트 발행-구독 | 느슨한 결합, Unity Event 대체 |
| **Strategy** | 알고리즘 교체 가능 | 이동 방식, AI 행동, 무기 공격 방식 |
| **Dirty Flag** | 변경 시에만 연산 실행 | UI 업데이트, 경로 재계산 |

### 구조 패턴

| 패턴 | 언제 사용 | 신호 |
|------|----------|------|
| **Flyweight** | 동일 데이터 공유 | 수백 개 적 스탯, 타일맵 데이터 |

### 아키텍처 패턴

| 패턴 | 언제 사용 |
|------|----------|
| **MVP** | UI 로직 분리 (uGUI 기반) |
| **MVVM** | 데이터 바인딩 자동화 (UI Toolkit 기반) |
| **Service Locator** | 전역 매니저 접근 창구 통일 — 하드 싱글톤 대체 (아래 절 참조) |

---

## 3-1. 서비스 로케이터 — 전역 접근의 기본 수단

코드는 `<스크립트 루트>/Common/Service/` 에 있다 (`Services` 정적 클래스 + `MonoService<T>` 베이스).
전역 매니저를 `X.Inst` 로 직접 물지 않고 **`Services.Get<T>()` 한 창구로 통일**한다.

- 등록은 `MonoService<T>` 를 상속하면 `Awake` 에서 자동으로 된다 (`OnDestroy` 에서 자동 해제).
  `X.Inst = this` 보일러플레이트가 사라진다.
- **`Awake` 에서 다른 서비스를 `Get` 하지 않는다.** 모든 `Awake` 등록이 끝난 뒤인 `Start` 부터 쓴다.
  등록 전에 `Get` 하면 예외가 나는데, 이는 초기화 순서 버그를 조용히 넘기지 않고 즉시 드러내려는 의도다.

### 둘 중 무엇으로 등록할까

| 방식 | 선언 | 쓰는 경우 |
|------|------|----------|
| **역할(인터페이스)로 등록** | `class ComputerOpponent : MonoService<IOpponent>, IOpponent` | 구현을 **교체할 가능성이 있을 때**. 호출부는 `Services.Get<IOpponent>()` 만 알아 어느 구현인지 모른다 (DIP) |
| **자기 타입으로 등록** | `class CardManager : MonoService<CardManager>` | 교체 가능성이 없을 때. 호출 포맷을 통일해두는 것이 목적 |

자기 타입으로 등록해도 호출 포맷이 같으므로, 나중에 인터페이스가 필요해지면
**등록부만 바꾸면 된다** — 호출부는 `Get<T>` 의 `T` 만 갈아끼우면 끝이다. 처음부터 모든 매니저에
인터페이스를 뽑는 오버엔지니어링을 피하고, 교체 필요가 실제로 생겼을 때 승격한다.

### 구현 교체는 '등록되는 쪽'을 바꿔서 한다

같은 역할을 구현한 둘을 모두 `MonoService<IOpponent>` 로 두고, **하나만 활성화**하면
그쪽 `Awake` 가 자신을 등록한다. 호출부는 전혀 손대지 않는다.

```csharp
public interface IOpponent { void SetupPlace(); void Play(); }        // AI/Contracts/

public class ComputerOpponent : MonoService<IOpponent>, IOpponent { } // 룰 기반 컴퓨터
public class RemoteOpponent   : MonoService<IOpponent>, IOpponent { } // 원격 사람 (서버 연동 자리)

// 선택자가 둘 중 하나만 SetActive(true) → 그쪽이 IOpponent 로 등록됨
// 호출부는 어느 쪽이 등록됐는지 모른다
Services.Get<IOpponent>().Play();
```

### 역할 이름에 구현 방식을 넣지 않는다

위 역할이 `IOpponentAI` 가 아니라 `IOpponent` 인 데는 이유가 있다. 이 역할은 **컴퓨터도 사람도 수행**한다
(`RemoteOpponent` 는 서버가 붙으면 실제 사람이 두는 자리다). 이름에 `AI` 를 박으면 사람 구현이
들어오는 순간 이름이 거짓이 되고, 호출부는 `Get<IOpponentAI>()` 로 사람을 부르게 된다.

- **역할 이름** = 무엇을 하는가 (`IOpponent`, `IDamageable`) — 구현 수단은 빼고 계약만 말한다.
- **구현 이름** = 어떻게 하는가 (`ComputerOpponent`, `RemoteOpponent`) — 정체를 드러낸다.
- 같은 역할의 구현끼리는 **접미사를 맞춘다** (`~Opponent`). 앞부분이 구현 간 차이를 설명한다.

### 계약은 그것을 정의한 기능 폴더에 둔다

`Services` 는 범용 메커니즘이라 `Common/Service/` 지만, 계약은 도메인이므로 `Common/` 에 두지 않는다.
**그 계약을 정의한 기능 폴더 안 `Contracts/`** 에 두어 계약과 구현이 함께 움직이게 한다.

```
Gameplay/AI/
├── Contracts/
│   └── IOpponent.cs        계약
├── ComputerOpponent.cs     구현
├── RemoteOpponent.cs       구현
└── OpponentSelector.cs     교체 담당
```

- 인터페이스가 하나뿐이어도 `Contracts/` 를 만든다 — 계약이 항상 같은 자리에 있어야 찾기 쉽다.
- **계약 전용 최상위 폴더(`Core/Contracts/` 같은)는 두지 않는다.** 기능이 늘수록 구현과 멀어져
  어느 구현이 이 계약을 지키는지 안 보이는 창고가 된다.

**"누가 정의했나"는 호출하는 쪽(소비자)이 기준이다** — 인터페이스는 부르는 쪽이 "나는 이런 게 필요하다"고
선언하는 것이지, 구현하는 쪽이 제공하는 게 아니다 (DIP). 여러 기능이 쓴다고 중앙으로 올리지 않는다.

```
Combat/
├── Contracts/
│   └── IDamageable.cs      '때릴 수 있는 것' 을 필요로 하는 건 Combat → Combat 이 정의
└── CombatSystem.cs         Services.Get / 직접 호출로 IDamageable 을 부른다

Gameplay/
├── Entity.cs               : IDamageable   구현만 할 뿐, 계약의 주인이 아니다
└── Card.cs                 : IDamageable
```

Gameplay·UI 가 나중에 `IDamageable` 을 같이 쓰게 돼도 파일은 그대로 `Combat/Contracts/` 에 있는다.
**사용처 수가 늘었다고 계약을 옮기지 않는다** — 옮기는 기준을 '몇 개가 쓰나'로 두면 사용처가 바뀔 때마다
파일이 이사를 다닌다. 소유자는 변하지 않으므로 자리도 변하지 않는다.

---

## 4. OOP 설계 체크

```csharp
// 캡슐화 — public 필드 최소화, [SerializeField] 활용
[SerializeField] private float _maxHealth; // Good
public float maxHealth;                    // Bad (외부에서 직접 수정 가능)

// 다형성 — Interface 활용
public interface IDamageable { void TakeDamage(float amount); }
public interface IInteractable { void Interact(); }

// 상속 vs 컴포지션 — 깊은 상속보다 컴포넌트 분리 우선
// Bad: Enemy → FlyingEnemy → FlyingBossEnemy (3단계 상속)
// Good: Enemy + FlyComponent + BossComponent (컴포지션)
```