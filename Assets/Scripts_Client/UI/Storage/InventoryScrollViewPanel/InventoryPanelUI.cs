using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리 목록 화면. <see cref="PlayerDataManager.InventoryChanged"/>를 구독해 갱신한다.
/// 창고 열의 <b>자원 탭</b>에 해당한다 — 캐릭터·장비·특성 탭이 붙으면 그 옆에 나란히 선다.
///
/// ■ 칸 프레임은 씬에 미리 깔려 있다
///   <c>Content</c> 아래의 <c>Slot (N)</c> 들이 프레임이고, 아이템 프리팹은 <b>그 프레임의 자식</b>으로
///   들어간다. Content 직속으로 만들면 프레임을 벗어나 레이아웃이 무너진다.
///   프레임은 코드가 만들지도 지우지도 않는다.
///
/// ■ 로그인 전에는 아무것도 만들지 않는다
///   아이템이 실제로 들어왔을 때만 프리팹을 생성한다. 미리 채워 두면 빈 칸에 빈 프리팹이
///   쌓여 하이어라키가 지저분해지고, 화면에도 빈 텍스트가 200개 뜬다.
///
/// ■ 키는 이름이 아니라 ItemId 다
///   이름은 표시용이고 중복될 수 있다. 이름으로 칸을 찾으면 같은 이름의 다른 아이템이
///   생기는 순간 조용히 엉뚱한 칸을 갱신한다.
/// </summary>
public class InventoryPanelUI : MonoBehaviour
{
    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("아이템 한 칸 프리팹 (InventorySlotView 포함). 빈 프레임 안에 생성된다")]
    private InventorySlotView slotPrefab = null!;

    [SerializeField, Tooltip("칸 프레임(Slot)들이 들어 있는 부모 — Inventory Scroll View Panel > Viewport > Content")]
    private Transform slotParent = null!;

    // ItemId → 그 아이템을 그리고 있는 뷰. 매 갱신마다 전체를 훑지 않으려고 둔다.
    private readonly Dictionary<int, InventorySlotView> _viewByItemId = new Dictionary<int, InventorySlotView>();

    // 뷰가 들어가 있는 프레임. 아이템이 사라질 때 프레임을 비우려고 짝을 기억한다.
    private readonly Dictionary<int, Transform> _frameByItemId = new Dictionary<int, Transform>();

    private PlayerDataManager _data = null!;
    private bool              _isSubscribed;
    private bool              _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        // 필수 참조 검증 — 미연결이면 여기서 멈춘다. 안 그러면 아이템이 처음 들어오는 순간
        // Instantiate에서 NRE가 나는데, 그때는 원인이 인스펙터라는 게 드러나지 않는다.
        this.RequireRef(slotPrefab, nameof(slotPrefab));
        this.RequireRef(slotParent, nameof(slotParent));

        _data = Services.Get<PlayerDataManager>();
        Subscribe();
        Refresh(); // 이미 스냅샷을 받은 뒤에 켜졌을 수 있다

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 창고를 닫아 둔 사이 채취·가챠로 수량이 바뀌었을 수 있다.
    //   캐시는 계속 살아 있으므로 다시 그리기만 하면 즉시 맞는다.
    private void OnEnable()
    {
        if (!_isReady)
            return;

        Subscribe();
        Refresh();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 인벤토리 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed          = true;
        _data.InventoryChanged += Refresh;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed          = false;
        _data.InventoryChanged -= Refresh;
    }

    #endregion

    #region 목록 구성

    // 인벤토리 캐시를 화면에 반영한다 (InventoryChanged 구독)
    private void Refresh()
    {
        foreach (var item in _data.Inventory)
        {
            if (item.Count <= 0)
            {
                RemoveItem(item.ItemId);
                continue;
            }

            if (!_viewByItemId.TryGetValue(item.ItemId, out var view))
            {
                view = CreateInEmptyFrame(item.ItemId);
                if (view == null)
                    continue; // 빈 프레임이 없다 — 경고는 CreateInEmptyFrame이 남긴다
            }

            view.Bind(item.ItemId, item.Count);
        }
    }

    /// <summary>비어 있는 프레임을 찾아 그 안에 아이템 프리팹을 만든다. 프레임이 없으면 null.</summary>
    private InventorySlotView? CreateInEmptyFrame(int itemId)
    {
        Transform? frame = FindEmptyFrame();
        if (frame == null)
        {
            ClientLogger.Warn(ClientLogger.UI, $"빈 칸이 없어 아이템 {itemId}를 표시하지 못했다. 프레임을 늘려야 한다.", this);
            return null;
        }

        var view = Instantiate(slotPrefab, frame);
        SnapToFrame(view.transform as RectTransform);

        _viewByItemId.Add(itemId, view);
        _frameByItemId.Add(itemId, frame);

        return view;
    }

    // 자식이 없는 프레임을 앞에서부터 찾는다 (CreateInEmptyFrame에서 호출)
    private Transform? FindEmptyFrame()
    {
        foreach (Transform frame in slotParent)
        {
            if (frame.childCount == 0)
                return frame;
        }

        return null;
    }

    // 아이템이 없어졌으면 뷰를 지우고 프레임을 비운다 (Refresh에서 호출)
    private void RemoveItem(int itemId)
    {
        if (!_viewByItemId.TryGetValue(itemId, out var view))
            return;

        Destroy(view.gameObject);
        _viewByItemId.Remove(itemId);
        _frameByItemId.Remove(itemId);
    }

    /// <summary>
    /// 프리팹을 프레임 안에 안착시킨다 — 위치를 0으로 맞춰 프레임 정중앙에 놓는다.
    /// Instantiate 직후의 RectTransform은 프리팹에 저장된 좌표를 그대로 들고 오므로,
    /// 이걸 하지 않으면 프레임 밖으로 삐져나간다.
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
}
