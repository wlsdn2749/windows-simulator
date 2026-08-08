using System;
using System.Collections.Generic;
using MikaProtocol;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작업슬롯 목록 패널. 서버 스냅샷만큼 <see cref="WorkStationSlotView"/>를 만들고,
/// 카운트다운을 <b>여기 한 곳에서</b> 계산해 각 뷰에 넘긴다.
///
/// <para>
/// ■ 두 축을 섞지 않는다<br/>
/// <b>데이터 갱신</b>은 이벤트(옵저버) — <c>PlayerDataManager.WorkStationSlotsChanged</c>.
/// <b>시간 진행</b>은 이 클래스의 <see cref="Update"/> 하나.
/// 슬롯마다 Update를 두면 상시 실행 앱에서 비용이 슬롯 수만큼 곱해진다.
/// </para>
///
/// <para>
/// ■ 서버는 주기(초)를 보내지 않는다<br/>
/// 슬롯마다 속도가 다르고 버프로 바뀌기 때문이다. 대신 진행도·속도·1회 비용을 주므로
/// 클라가 직접 계산한다. 나중에 주기 규칙이 바뀌어도 이 계산은 그대로다.
/// </para>
///
/// <para>
/// ■ 칸을 누르면 알리기만 한다<br/>
/// 어느 패널로 갈지는 <see cref="WorkStationCanvasUI"/>가 정한다. 이 패널은
/// <see cref="SlotClicked"/>만 쏜다 — 그래야 목록이 자기를 담은 캔버스를 몰라도 된다.
/// </para>
/// </summary>
public class WorkStationScrollViewPanelUI : MonoBehaviour
{
    // 작업량 단위는 "밀리초 × 천분율 속도"다. 1초 × 1.0배 = 1000ms × 1000 = 1,000,000 단위.
    // 남은 시간을 초로 되돌릴 때 이 값으로 나눈다.
    private const float UnitsPerSecondAtBaseSpeed = 1000f;

    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("슬롯 한 칸 프리팹 (WorkStationSlotView 포함). 빈 프레임 안에 생성된다")]
    private WorkStationSlotView slotPrefab = null!;

    [SerializeField, Tooltip("칸 프레임(Slot)들이 들어 있는 부모 — Viewport > Content")]
    private Transform slotParent = null!;

    /// <summary>칸을 눌렀다. 인자는 슬롯 번호 (<see cref="WorkStationCanvasUI"/>가 구독).</summary>
    public event Action<int>? SlotClicked;

    // 슬롯 번호 → 뷰. 스냅샷이 다시 와도 같은 칸을 재사용해 깜빡임을 막는다.
    private readonly Dictionary<int, WorkStationSlotView> _views = new Dictionary<int, WorkStationSlotView>();

    private PlayerDataManager _data = null!;
    private bool              _isSubscribed;
    private bool              _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(slotPrefab, nameof(slotPrefab));
        this.RequireRef(slotParent, nameof(slotParent));

        _data = Services.Get<PlayerDataManager>();

        Subscribe();
        BindFrameButtons();
        Rebuild(); // 이미 스냅샷을 받은 뒤에 켜졌을 수 있다

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 도착한 스냅샷을 놓쳤기 때문이다.
    //   캐시(PlayerDataManager)는 계속 살아 있으므로 다시 그리기만 하면 즉시 맞는다.
    private void OnEnable()
    {
        if (!_isReady)
            return;

        Subscribe();
        Rebuild();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 카운트다운 진행 — 슬롯 전체를 여기서 한 번에 계산한다 (Unity 메시지)
    private void Update()
    {
        if (!_isReady)
            return;

        foreach (var slot in _data.WorkStationSlots)
        {
            if (!_views.TryGetValue(slot.SlotIndex, out var view) || !view.IsRunning)
                continue;

            float remainSeconds = CalculateRemainSeconds(slot);

            WatchCycleWrap(slot.SlotIndex, remainSeconds); // 진단 (임시)

            view.Tick(CalculateProgress(slot), remainSeconds);
        }
    }

    #region 구독

    // 슬롯 스냅샷 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed                 = true;
        _data.WorkStationSlotsChanged += Rebuild;
        _data.GatherResultReceived    += OnGatherResultReceived; // 진단 (임시)
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed                 = false;
        _data.WorkStationSlotsChanged -= Rebuild;
        _data.GatherResultReceived    -= OnGatherResultReceived; // 진단 (임시)
    }

    #endregion

    #region 칸 클릭

    /// <summary>
    /// 칸 프레임의 버튼을 슬롯 번호와 묶는다 (Start에서 한 번).
    ///
    /// <para>
    /// <b>버튼은 프레임에 붙어 있어야 한다 — 안에 생기는 뷰가 아니라.</b>
    /// 비어 있는 슬롯에는 뷰가 만들어지지 않는데, 빈 칸이야말로 눌러서 배치할 대상이다.
    /// </para>
    /// </summary>
    private void BindFrameButtons()
    {
        for (int i = 0; i < slotParent.childCount; i++)
        {
            var button = slotParent.GetChild(i).GetComponent<Button>();
            if (button == null)
            {
                ClientLogger.Warn(ClientLogger.UI,
                    $"칸 프레임 {slotParent.GetChild(i).name}에 Button이 없어 클릭을 받을 수 없다.", this);
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            int slotIndex = i;
            button.onClick.AddListener(() => SlotClicked?.Invoke(slotIndex));
        }
    }

    #endregion

    #region 목록 구성

    /// <summary>
    /// 스냅샷대로 슬롯 뷰를 만들고 갱신한다 (WorkStationSlotsChanged 구독).
    /// 슬롯 번호가 곧 프레임 순서다 — 슬롯 0은 <c>Content</c>의 첫 자식 프레임 안에 들어간다.
    /// 인벤토리와 달리 번호가 고정이라 "빈 프레임 찾기"가 아니라 자리를 직접 고른다.
    ///
    /// <para>
    /// ■ 배치된 칸에만 뷰를 둔다<br/>
    /// 배치가 풀리면 뷰를 <b>지운다.</b> 남겨 두고 "대기"라고 적으면 빈 칸과 구분이 안 되고,
    /// 무엇보다 뷰가 프레임 위를 덮어 <b>칸을 눌러 배치 화면으로 들어가는 길을 막는다.</b>
    /// </para>
    /// </summary>
    private void Rebuild()
    {
        foreach (var slot in _data.WorkStationSlots)
        {
            // 비어 있는 칸은 프레임만 남긴다 — 그래야 눌러서 배치할 수 있다
            if (!IsAssigned(slot))
            {
                RemoveView(slot.SlotIndex);
                continue;
            }

            if (!_views.TryGetValue(slot.SlotIndex, out var view))
            {
                if (slot.SlotIndex < 0 || slot.SlotIndex >= slotParent.childCount)
                {
                    ClientLogger.Warn(ClientLogger.UI, $"슬롯 {slot.SlotIndex}에 해당하는 칸 프레임이 없다. 프레임을 늘려야 한다.", this);
                    continue;
                }

                Transform frame = slotParent.GetChild(slot.SlotIndex);

                view      = Instantiate(slotPrefab, frame);
                view.name = $"WorkStationSlot {slot.SlotIndex}";
                SnapToFrame(view.transform as RectTransform);

                _views.Add(slot.SlotIndex, view);

                LogSnapshot(slot); // 진단 (임시) — 뷰를 처음 만들 때 한 번
            }

            view.Bind(slot, _data.GetCharacterName(slot.CharacterId));
        }
    }

    /// <summary>배치가 풀린 칸의 뷰를 지운다 (<see cref="Rebuild"/>에서 호출).</summary>
    private void RemoveView(int slotIndex)
    {
        if (!_views.TryGetValue(slotIndex, out var view))
            return;

        _views.Remove(slotIndex); // Update가 죽은 뷰를 만지지 않도록 먼저 뺀다

        if (view != null)
            Destroy(view.gameObject);
    }

    /// <summary>
    /// 칸이 배치 상태인가 — 산업과 캐릭터가 둘 다 차 있어야 배치다.
    /// <c>IsRunning</c>과 다르다. 그쪽은 속도까지 봐서 "카운트다운을 돌릴 수 있는가"를 뜻한다.
    /// </summary>
    private static bool IsAssigned(WorkStationSlotInfo slot)
        => slot.Industry != 0 && slot.CharacterId != 0;

    /// <summary>
    /// 프리팹을 프레임 안에 안착시킨다 — 위치를 0으로 맞춰 프레임 정중앙에 놓는다.
    /// Instantiate 직후의 RectTransform은 프리팹에 저장된 좌표를 그대로 들고 온다.
    /// </summary>
    private static void SnapToFrame(RectTransform? rect)
    {
        if (rect == null)
            return;

        rect.anchoredPosition3D = Vector3.zero;
        rect.localScale         = Vector3.one;
        rect.localRotation      = Quaternion.identity;
    }

    #endregion

    #region A-2 진단 (임시 — 원인이 확정되면 이 구역을 통째로 지운다)

    // 슬롯별 직전 프레임의 남은 시간. 되감기는 순간을 잡으려고 들고 있는다.
    private readonly Dictionary<int, float> _lastRemainSeconds = new Dictionary<int, float>();

    // 슬라이더가 한 바퀴를 돈 시각(Time.realtimeSinceStartupAsDouble). 결과가 오면 지운다.
    private readonly Dictionary<int, double> _cycleEndedAt = new Dictionary<int, double>();

    /// <summary>
    /// 카운트다운이 <b>되감긴 순간</b>을 잡는다 (<see cref="Update"/>에서 호출).
    ///
    /// <para>
    /// 남은 시간은 정확히 0을 찍지 않는다 — <c>% JudgeCostUnits</c>가 0에 닿는 즉시
    /// 한 주기를 통째로 되돌려 놓기 때문이다. 그래서 "0 이하"가 아니라
    /// <b>값이 갑자기 커진 것</b>으로 한 바퀴를 판정한다.
    /// </para>
    /// </summary>
    private void WatchCycleWrap(int slotIndex, float remainSeconds)
    {
        if (_lastRemainSeconds.TryGetValue(slotIndex, out float previous)
            && remainSeconds > previous + 1f)
        {
            _cycleEndedAt[slotIndex] = Time.realtimeSinceStartupAsDouble;
            ClientLogger.Info(ClientLogger.Data, $"[A-2] 슬롯 {slotIndex} — 카운트다운이 0을 지나 되감겼다");
        }

        _lastRemainSeconds[slotIndex] = remainSeconds;
    }

    /// <summary>
    /// 채취 결과가 도착한 시각을 되감긴 시각과 견준다 (GatherResultReceived 구독).
    /// <b>이 차이가 A-2의 정체다.</b>
    /// </summary>
    private void OnGatherResultReceived(S_GatherResultResponse res)
    {
        if (_cycleEndedAt.TryGetValue(res.SlotIndex, out double endedAt))
        {
            double lateSeconds = Time.realtimeSinceStartupAsDouble - endedAt;
            _cycleEndedAt.Remove(res.SlotIndex);

            ClientLogger.Info(ClientLogger.Data,
                $"[A-2] 슬롯 {res.SlotIndex} — 되감긴 뒤 {lateSeconds:0.00}초 만에 결과 도착 (판정 {res.JudgeCount}회). " +
                $"양수면 서버가 느리다");
            return;
        }

        // 아직 한 바퀴를 안 돌았는데 결과가 왔다 = 서버가 클라보다 앞선다
        float remain = _lastRemainSeconds.TryGetValue(res.SlotIndex, out float r) ? r : -1f;
        ClientLogger.Info(ClientLogger.Data,
            $"[A-2] 슬롯 {res.SlotIndex} — 카운트다운이 아직 {remain:0.00}초 남았는데 결과가 왔다 (판정 {res.JudgeCount}회). " +
            $"서버가 그만큼 앞선다");
    }

    /// <summary>슬롯 스냅샷의 원본 값을 한 번 찍는다 — 단위가 계약과 맞는지 눈으로 보려고 (뷰를 만들 때 호출).</summary>
    private void LogSnapshot(WorkStationSlotInfo slot)
    {
        double nowUnix   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        double elapsedSec = nowUnix - slot.LastTickAtUnix;
        double cycleSec   = (double)slot.JudgeCostUnits / slot.CurrentWorkSpeed / UnitsPerSecondAtBaseSpeed;

        ClientLogger.Info(ClientLogger.Data,
            $"[A-2] 슬롯 {slot.SlotIndex} 스냅샷 — LastTickAtUnix={slot.LastTickAtUnix} (지금보다 {elapsedSec:0.00}초 전), " +
            $"ProgressUnits={slot.ProgressUnits}, speed={slot.CurrentWorkSpeed}, JudgeCost={slot.JudgeCostUnits}, " +
            $"한 주기={cycleSec:0.00}초, 지금 남은={CalculateRemainSeconds(slot):0.00}초");
    }

    #endregion

    #region 카운트다운 계산 (서버 식 그대로)

    /// <summary>
    /// 마지막 정산 이후 쌓인 작업량 중 <b>이번 판정에 해당하는 몫</b>을 구한다.
    /// 판정 1회 비용으로 나눈 나머지라, 여러 판정이 밀려 있어도 현재 사이클만 남는다.
    /// </summary>
    private static long GetPendingUnits(WorkStationSlotInfo slot)
    {
        double elapsedMs   = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - slot.LastTickAtUnix * 1000L);
        double accumulated = slot.ProgressUnits + elapsedMs * slot.CurrentWorkSpeed;

        return (long)(accumulated % slot.JudgeCostUnits);
    }

    // 판정 진행도 0~1 (Update에서 호출)
    private static float CalculateProgress(WorkStationSlotInfo slot)
    {
        return Mathf.Clamp01((float)GetPendingUnits(slot) / slot.JudgeCostUnits);
    }

    // 다음 수확까지 남은 초 (Update에서 호출)
    private static float CalculateRemainSeconds(WorkStationSlotInfo slot)
    {
        long remainUnits = slot.JudgeCostUnits - GetPendingUnits(slot);

        // IsRunning이 CurrentWorkSpeed > 0을 보장하지만, 계산식만 떼어 봐도 안전하도록 가드를 남긴다.
        if (slot.CurrentWorkSpeed <= 0)
            return 0f;

        return remainUnits / (float)slot.CurrentWorkSpeed / UnitsPerSecondAtBaseSpeed;
    }

    #endregion
}
