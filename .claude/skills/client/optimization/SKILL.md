---
name: optimization
description: 성능 최적화 판단 규칙. 최적화/성능 개선 요청 시 이 규칙을 따른다.
---

> 최종 업데이트: 2026-07-17

# 최적화 가이드

---

## 0. 적용 원칙 (먼저 판단)

> **최적화는 측정 후 필요한 곳에만 한다.**
> 
> 체감 성능 향상이 없거나, 코드 복잡도가 더 올라간다면 최적화하지 않는다.
> 
> 가독성 있는 느린 코드가 이해 불가능한 빠른 코드보다 낫다.

---

## 1. 최적화 전 필수 점검

```
□ Unity Profiler로 실제 병목을 확인했는가?
□ 이 최적화로 얼마나 향상되는가? (측정 가능?)
□ 코드 가독성이 심각하게 저하되지 않는가?
□ 유지보수 비용이 성능 이익보다 크지 않은가?
```

---

## 2. 최적화하지 말아야 할 경우

| 상황 | 판단 |
|------|------|
| 성능 향상이 체감 불가 (< 1ms) 한데 코드가 복잡해짐 | 하지 않음 |
| 프로파일러 없이 추측으로 병목 판단 | 먼저 측정 |
| 가독성을 크게 희생하면서 얻는 이익이 적음 | 하지 않음 |
| 초기화 시 1회만 호출되는 코드 | 가독성 우선 |
| 수십 개 이하 오브젝트에 적용 | 가독성 우선 |

---

## 3. Unity 실무 최적화 패턴

### Object Pool — `Instantiate/Destroy` 대신

```csharp
// Bad — 매 프레임 생성/파괴
void Shoot()
{
    Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
}

// Good — Pool에서 꺼내고 반납
void Shoot()
{
    Bullet bullet = _bulletPool.Get();
    bullet.transform.SetPositionAndRotation(firePoint.position, Quaternion.identity);
    bullet.Launch();
}
```

적용 대상: 총알, 이펙트, 파티클, 적 스폰

### Dirty Flag — 변경 시에만 연산 실행

```csharp
// 값이 바뀔 때만 재계산
private bool _statsDirty = true;

public void OnStatChanged() { _statsDirty = true; }

private void Update()
{
    if (!_statsDirty) { return; }
    RecalculateStats();
    _statsDirty = false;
}
```

적용 대상: UI 업데이트, 경로 재계산, 스탯 합산

### 캐싱 — `GetComponent`는 Awake에서 한 번만

```csharp
// Bad — 매 프레임 호출
private void Update()
{
    GetComponent<Rigidbody2D>().AddForce(Vector2.up);
}

// Good — Awake에서 캐싱
private Rigidbody2D _rigidbody;
private void Awake() { _rigidbody = GetComponent<Rigidbody2D>(); }
private void Update() { _rigidbody.AddForce(Vector2.up); }
```

### Update 최소화 — 이벤트 기반으로 전환 검토

```csharp
// Bad — 매 프레임 조건 체크
private void Update()
{
    if (_currentHealth <= 0f) { Die(); }
}

// Good — 값이 변경될 때 이벤트로 처리
public void TakeDamage(float damage)
{
    _currentHealth -= damage;
    if (_currentHealth <= 0f) { Die(); }
}
```

### StringBuilder — 문자열 반복 결합 시

```csharp
// Bad — 매 프레임 문자열 결합 → GC 발생
string hud = "HP: " + _health + " / " + _maxHealth;

// Good — StringBuilder 재사용
_stringBuilder.Clear();
_stringBuilder.Append("HP: ").Append(_health).Append(" / ").Append(_maxHealth);
_hudText.text = _stringBuilder.ToString();
```

---

## 4. 가독성 vs 성능 트레이드오프 판단

| 상황 | 판단 |
|------|------|
| 매 프레임 호출 (Update, FixedUpdate) | 최적화 적극 검토 |
| 초기화 시 1회 호출 | 가독성 우선 |
| 100개 이상 오브젝트에 동시 적용 | 최적화 검토 |
| 수십 개 이하 | 가독성 우선 |
| 모바일 타겟 | 더 엄격하게 적용 |
| PC 타겟, 여유 프레임 버짓 | 가독성 우선 |
