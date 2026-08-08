using System;
using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작업슬롯 한 칸의 설정 화면. 목록에서 칸을 누르면 목록 대신 이 화면이 열린다.
///
/// <para>
/// ■ 머리 둘은 늘 보이고, 몸통 둘이 갈아 끼워진다<br/>
/// <code>
/// Header Panel     슬롯 번호 + 뒤로가기          ← 항상
/// Industry Panel   산업 5개                      ← 항상 (전부 누를 수 있다)
/// ───────────────────────────────────────────────
/// Character Assign Scroll View Panel   캐릭터를 고른다   ┐ 둘 중
/// Character Setting Panel              배치된 것을 만진다 ┘ 하나만
/// </code>
/// </para>
///
/// <para>
/// ■ 세 단계를 오간다<br/>
/// <code>
/// 슬롯 목록 ─── 빈 칸 ──→ 배치 목록 ── [배치] 성공 ──→ 세팅
///     ▲          찬 칸 ─────────────────────────────→ 세팅
///     │                    배치 목록 ←─ 성공 [해제] ── 세팅
///     └──────────── [뒤로] 또는 <b>요청 실패</b> ───────────┘
/// </code>
/// <b>배치와 해제가 성공하면 슬롯 목록으로 튕기지 않는다.</b> 배치했으면 이어서 세팅할 것이고,
/// 해제했으면 이어서 다른 캐릭터를 고를 것이기 때문이다.
/// </para>
///
/// <para>
/// ■ 응답을 보고 단계를 정한다<br/>
/// 누르자마자 넘어가면 <b>서버가 거절해도 넘어간다</b> — 아직 열리지 않은 슬롯에 배치를 걸면
/// 실제로는 아무 일도 없는데 세팅 화면이 뜬다. 그래서 <c>WorkStationAssignCompleted</c>를 기다렸다가
/// 성공하면 다음 단계로, <b>실패하면 슬롯 목록으로 물러난다.</b>
/// 기다리는 동안에는 배치·해제 버튼을 잠근다 — 다만 <b>뒤로가기는 잠그지 않는다</b>,
/// 응답이 영영 안 와도 나갈 길은 있어야 한다.
/// </para>
///
/// <para>
/// ■ 산업 버튼은 잠그지 않는다<br/>
/// 배치 목록에서는 <b>캐릭터를 걸러 보는 수단</b>이고, 세팅에서는 <b>다른 산업으로 갈아 끼우는 수단</b>이다.
/// 어느 쪽이든 늘 누를 수 있어야 해서 <c>interactable</c>은 건드리지 않고 <b>색으로만</b> 고른 것을 표시한다.
/// </para>
///
/// <para>
/// ■ 화면이 상태를 기억하지 않는다<br/>
/// "지금 배치돼 있는가"는 서버 스냅샷(<c>PlayerDataManager.WorkStationSlots</c>)에서 읽는다.
/// 자체 플래그를 들면 실패 응답이 왔을 때 화면과 서버가 어긋난다.
/// <b>고른 산업만</b>은 아직 서버에 없는 값이라 여기서 들고 있는다.
/// </para>
///
/// <para>
/// ⚠️ <b>줄은 씬에 미리 깔아 둔 것을 재사용한다(풀).</b> 보유 캐릭터 수만큼만 켜고 나머지는 끈다.
/// 캐릭터가 깔아 둔 줄 수보다 많아지면 그때 줄을 프리팹으로 빼면 된다.
/// </para>
///
/// <para>
/// ⚠️ <b>산업 버튼은 아직 캐릭터를 걸러 내지 않는다.</b> 고른 산업이 배치 요청에 실릴 뿐,
/// 그 산업을 못 다루는 캐릭터도 목록에 그대로 뜬다. 세팅에서 산업을 바꿔도 아직 요청이 안 나간다
/// → 일감 "작업슬롯 선택 패널".
/// </para>
/// </summary>
public class WorkStationSelectPanelUI : MonoBehaviour
{
    [CenterHeader("< Header Panel — 항상 보인다 >")]
    [SerializeField, Tooltip("'슬롯 N 설정' — 어느 칸을 눌러 들어왔는지 알리는 유일한 단서다")]
    private TMP_Text titleText = null!;

    [SerializeField, Tooltip("어느 단계에 있든 슬롯 목록으로 나간다. OnClick은 코드가 연결한다")]
    private Button backButton = null!;

    [CenterHeader("< Industry Panel — 항상 보인다 >")]
    [SerializeField, Tooltip("산업 버튼 5개. 인스펙터에 넣은 순서가 곧 산업 순서다(농사·낚시·채굴·벌목·사냥)")]
    private Button[] industryButtons = new Button[0];

    [SerializeField, Tooltip("고른 산업 버튼의 바탕색")]
    private Color selectedIndustryColor = Color.white;

    [SerializeField, Tooltip("고르지 않은 산업 버튼의 바탕색. 잠그는 게 아니라 흐리게만 한다")]
    private Color unselectedIndustryColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [CenterHeader("< 2단계 — 캐릭터를 고른다 >")]
    [SerializeField, Tooltip("Character Assign Scroll View Panel 오브젝트")]
    private GameObject assignPanel = null!;

    [SerializeField, Tooltip("캐릭터 줄들이 들어 있는 부모 — Viewport > Content")]
    private Transform rowParent = null!;

    [CenterHeader("< 3단계 — 배치된 것을 만진다 >")]
    [SerializeField, Tooltip("Character Setting Panel 오브젝트")]
    private GameObject settingPanel = null!;

    [SerializeField, Tooltip("해제 버튼. 배치 버튼과 역할을 나눈다 — 여긴 해제만 한다")]
    private Button unassignButton = null!;

    /// <summary>이 화면을 닫아 달라 (<see cref="WorkStationCanvasUI"/>가 구독).</summary>
    public event Action? Closed;

    /// <summary>보낸 요청의 종류. 응답에는 배치였는지 해제였는지가 안 실려 와서 보낸 쪽이 기억한다.</summary>
    private enum PendingRequest
    {
        None,
        Assign,
        Unassign,
    }

    // 버튼 순서와 1:1로 대응하는 산업 목록. enum 값을 직접 인덱스로 쓰면
    // None·Misc·Special이 끼어 있어 어긋나므로 별도 목록으로 들고 있는다.
    private readonly List<ItemType> _industries = new List<ItemType>();

    // 씬에 깔아 둔 줄들. 보유 캐릭터 수만큼만 켠다.
    private readonly List<CharacterStateRowView> _rows = new List<CharacterStateRowView>();

    // 지금 다루는 슬롯 번호. Open이 정한다 — 아직 안 열렸으면 -1.
    private int _slotIndex = -1;

    // 지금 고른 산업. 서버가 모르는 값이라 화면이 들고 있는다.
    private int _selectedIndustry;

    // 응답을 기다리는 중인 요청. 없으면 None.
    private PendingRequest _pending = PendingRequest.None;

    /// <summary>응답을 기다리는 중인가. 그동안 배치·해제 버튼을 잠근다.</summary>
    private bool IsWaiting => _pending != PendingRequest.None;

    private PlayerDataManager _data    = null!;
    private NetworkManager    _network = null!;
    private bool              _isSubscribed;
    private bool              _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(titleText,      nameof(titleText));
        this.RequireRef(backButton,     nameof(backButton));
        this.RequireRef(assignPanel,    nameof(assignPanel));
        this.RequireRef(rowParent,      nameof(rowParent));
        this.RequireRef(settingPanel,   nameof(settingPanel));
        this.RequireRef(unassignButton, nameof(unassignButton));

        _data    = Services.Get<PlayerDataManager>();
        _network = NetworkManager.Instance;

        Subscribe();

        BuildIndustryList();
        BindIndustryButtons();
        CollectRows();

        backButton.onClick.AddListener(() => Closed?.Invoke());
        unassignButton.onClick.AddListener(OnUnassignButtonClicked);

        OpenStageForSlot();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 슬롯 상태가 바뀌었으면 라벨이 낡은 채로 남는다.
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

    /// <summary>
    /// 이 슬롯을 다루도록 열린다 (<see cref="WorkStationCanvasUI"/>가 호출).
    ///
    /// <para>
    /// ※ 켜기 전에 번호부터 넣는다. 꺼져 있던 화면은 <see cref="Start"/>가 아직 안 돌았을 수 있는데,
    /// 그때는 Start가 이어서 단계를 정한다. 이미 돌았으면 여기서 바로 정한다.
    /// </para>
    /// </summary>
    public void Open(int slotIndex)
    {
        _slotIndex = slotIndex;

        // 닫히는 동안 구독이 끊겨 지난 응답을 놓쳤을 수 있다. 잠금을 들고 들어가지 않는다.
        _pending = PendingRequest.None;

        gameObject.SetActive(true);

        if (_isReady)
            OpenStageForSlot();
    }

    #region 단계 전환

    /// <summary>
    /// 슬롯 상태가 첫 단계를 정한다 — <b>빈 칸이면 캐릭터를 고르러, 찬 칸이면 세팅으로.</b>
    /// (<see cref="Start"/> · <see cref="Open"/>에서 호출)
    /// </summary>
    private void OpenStageForSlot()
    {
        if (IsAssigned(FindSlot()))
            ShowSetting();
        else
            ShowAssignList();
    }

    /// <summary>2단계 — 캐릭터 목록을 보여 준다.</summary>
    private void ShowAssignList()
    {
        assignPanel.SetActive(true);
        settingPanel.SetActive(false);
        Refresh();
    }

    /// <summary>3단계 — 배치된 캐릭터의 세팅을 보여 준다.</summary>
    private void ShowSetting()
    {
        assignPanel.SetActive(false);
        settingPanel.SetActive(true);
        Refresh();
    }

    #endregion

    #region 구독

    // 슬롯·캐릭터 캐시 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed                    = true;
        _data.WorkStationSlotsChanged   += Refresh;
        _data.CharactersChanged         += Refresh; // 보유 캐릭터가 늘면 줄도 늘어야 한다
        _data.WorkStationAssignCompleted += OnAssignCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed                    = false;
        _data.WorkStationSlotsChanged   -= Refresh;
        _data.CharactersChanged         -= Refresh;
        _data.WorkStationAssignCompleted -= OnAssignCompleted;
    }

    #endregion

    #region 산업 선택

    // 채취 가능한 1차 산업만 목록에 담는다 (Start에서 호출)
    private void BuildIndustryList()
    {
        _industries.Clear();

        // None·Misc·Special·Max는 배치 대상이 아니다. 채취하는 5종만 남긴다.
        foreach (ItemType industry in Enum.GetValues(typeof(ItemType)))
        {
            if (industry >= ItemType.Farming && industry <= ItemType.Hunting)
                _industries.Add(industry);
        }
    }

    /// <summary>
    /// 산업 버튼을 목록 순서와 묶는다 (Start에서 호출).
    /// <b>버튼 개수와 산업 개수가 다르면 조용히 어긋난다</b> — 그래서 여기서 먼저 알린다.
    /// </summary>
    private void BindIndustryButtons()
    {
        if (industryButtons.Length != _industries.Count)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"산업 버튼이 {industryButtons.Length}개인데 채취 산업은 {_industries.Count}종이다. " +
                $"인스펙터의 버튼 목록을 산업 순서(농사·낚시·채굴·벌목·사냥)대로 채울 것.", this);
        }

        for (int i = 0; i < industryButtons.Length; i++)
        {
            if (industryButtons[i] == null)
                continue;

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            int index = i;
            industryButtons[i].onClick.AddListener(() => SelectIndustry(index));
        }
    }

    // 산업을 고른다 (산업 버튼 OnClick에 코드로 연결)
    private void SelectIndustry(int index)
    {
        _selectedIndustry = index;
        RefreshIndustryButtons();
    }

    /// <summary>
    /// 고른 산업만 밝게 칠한다 (표시 갱신 때 호출).
    ///
    /// <para>
    /// <b>잠그지 않고 색만 바꾼다</b> — 배치 목록에서는 걸러 보는 수단, 세팅에서는 갈아 끼우는 수단이라
    /// 어느 단계에서도 눌릴 수 있어야 한다. <c>Selectable</c>은 실행 중 <c>colors.normalColor</c>로
    /// 바탕을 덮어쓰므로 <c>Image.color</c>가 아니라 이쪽을 바꾼다.
    /// </para>
    /// </summary>
    private void RefreshIndustryButtons()
    {
        for (int i = 0; i < industryButtons.Length; i++)
        {
            var button = industryButtons[i];
            if (button == null)
                continue;

            button.interactable = true;

            var colors = button.colors;
            colors.normalColor = i == _selectedIndustry ? selectedIndustryColor : unselectedIndustryColor;
            button.colors      = colors;
        }
    }

    #endregion

    #region 캐릭터 줄

    // 씬에 깔아 둔 줄을 모아 클릭을 받는다 (Start에서 한 번)
    private void CollectRows()
    {
        _rows.Clear();
        rowParent.GetComponentsInChildren(true, _rows);

        foreach (var row in _rows)
            row.AssignClicked += OnRowAssignClicked;

        if (_rows.Count == 0)
            ClientLogger.Warn(ClientLogger.UI, "캐릭터 줄이 하나도 없다 — Content 아래에 CharacterStateRowView를 둘 것.", this);
    }

    /// <summary>
    /// 보유 캐릭터 수만큼 줄을 켜고 나머지는 끈다 (<see cref="Refresh"/>에서 호출).
    /// 이 목록은 <b>빈 슬롯일 때만 보이므로</b> 모든 줄이 배치 가능이다 — 해제는 세팅 쪽 일이다.
    /// </summary>
    private void RefreshRows()
    {
        var characters = _data.Characters;

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];

            if (i >= characters.Count)
            {
                row.gameObject.SetActive(false);
                continue;
            }

            long characterId = characters[i].CharacterId;

            row.gameObject.SetActive(true);
            row.Bind(characterId, _data.GetCharacterName(characterId));
            row.SetAssignable(!IsWaiting);
        }

        if (characters.Count > _rows.Count)
        {
            ClientLogger.Warn(ClientLogger.UI,
                $"보유 캐릭터가 {characters.Count}인데 깔아 둔 줄은 {_rows.Count}개뿐이다. " +
                $"줄을 늘리거나 프리팹으로 빼서 만들 것.", this);
        }
    }

    #endregion

    #region 표시

    // 제목·산업 버튼·캐릭터 줄을 지금 상태로 맞춘다 (단계 전환 · 데이터 변경)
    //
    // ※ 여기서 단계를 바꾸지 않는다. 어느 단계에 있을지는 사용자의 조작이 정하고,
    //   이 메서드는 그 단계의 내용만 채운다.
    private void Refresh()
    {
        titleText.text = $"슬롯 {_slotIndex} 설정";

        RefreshIndustryButtons();

        if (assignPanel.activeSelf)
            RefreshRows();

        ApplyWaitingLock();
    }

    // 담당 슬롯의 현재 상태를 찾는다. 서버가 주지 않은 번호면 null (단계 판정·클릭 처리에서 호출)
    private WorkStationSlotInfo? FindSlot()
    {
        foreach (var slot in _data.WorkStationSlots)
        {
            if (slot.SlotIndex == _slotIndex)
                return slot;
        }

        return null; // 눌러 보면 실패 응답이 온다
    }

    // 슬롯이 배치 상태인가 — 산업과 캐릭터가 둘 다 차 있어야 배치다 (단계 판정·클릭 처리에서 호출)
    private static bool IsAssigned(WorkStationSlotInfo? slot)
        => slot != null && slot.Industry != 0 && slot.CharacterId != 0;

    #endregion

    #region 송신

    // 어느 줄의 배치를 눌렀다 (CharacterStateRowView.AssignClicked 구독)
    private void OnRowAssignClicked(CharacterStateRowView row)
    {
        if (!CanSend())
            return;

        if (_selectedIndustry < 0 || _selectedIndustry >= _industries.Count)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"고른 산업({_selectedIndustry})이 목록 범위(0~{_industries.Count - 1})를 벗어났다.", this);
            return;
        }

        // 서버는 캐릭터 종류(TID)가 아니라 개체 번호를 받는다. 누른 줄이 그 번호를 들고 있다.
        if (row.CharacterId == 0)
        {
            ClientLogger.Error(ClientLogger.UI, "누른 줄에 캐릭터가 묶여 있지 않다 — Bind를 거치지 않았다.", this);
            return;
        }

        ItemType industry = _industries[_selectedIndustry];
        Send((byte)industry, row.CharacterId);
        ClientLogger.Info(ClientLogger.Send, $"작업슬롯 배치 요청 — 슬롯={_slotIndex}, 산업={industry}, 캐릭터개체={row.CharacterId}");

        BeginWaiting(PendingRequest.Assign); // 넘어갈지 물러날지는 응답이 정한다
    }

    // 해제를 눌렀다 (unassignButton OnClick에 코드로 연결)
    private void OnUnassignButtonClicked()
    {
        if (!CanSend())
            return;

        Send(0, 0); // 산업·캐릭터 0 = 해제
        ClientLogger.Info(ClientLogger.Send, $"작업슬롯 해제 요청 — 슬롯={_slotIndex}");

        BeginWaiting(PendingRequest.Unassign);
    }

    // 응답이 올 때까지 배치·해제 버튼을 잠근다 (요청을 보낸 뒤 호출)
    private void BeginWaiting(PendingRequest request)
    {
        _pending = request;
        ApplyWaitingLock();
    }

    /// <summary>
    /// 응답이 왔다 — 성공이면 다음 단계로, <b>실패면 슬롯 목록으로 물러난다</b>
    /// (PlayerDataManager.WorkStationAssignCompleted 구독).
    ///
    /// <para>
    /// 실패는 대개 <b>아직 열리지 않은 슬롯</b>이다. 그 칸에서는 배치도 해제도 할 수 없으니
    /// 화면에 남겨 둘 이유가 없다. 사유(<c>EResultCode</c>)는 아직 이벤트에 안 실려서
    /// 사용자에게 못 보여 준다 → 일감 "실패 알림 토스트".
    /// </para>
    /// </summary>
    private void OnAssignCompleted(bool success)
    {
        var requested = _pending;

        _pending = PendingRequest.None;
        ApplyWaitingLock();

        if (requested == PendingRequest.None)
            return; // 이 화면이 보낸 요청이 아니다

        if (!success)
        {
            ClientLogger.Warn(ClientLogger.UI,
                $"슬롯 {_slotIndex} 변경이 거절돼 슬롯 목록으로 돌아간다 (열리지 않은 슬롯일 수 있다).", this);
            Closed?.Invoke();
            return;
        }

        if (requested == PendingRequest.Assign)
            ShowSetting();
        else
            ShowAssignList();
    }

    // 기다리는 동안 배치·해제만 잠근다. 뒤로가기는 잠그지 않는다 — 나갈 길은 늘 열려 있어야 한다
    private void ApplyWaitingLock()
    {
        unassignButton.interactable = !IsWaiting;

        foreach (var row in _rows)
            row.SetAssignable(!IsWaiting);
    }

    // 보낼 수 있는 상태인가 (배치·해제 클릭에서 호출)
    private bool CanSend()
    {
        // 버튼을 잠가 두지만 잠금이 늦게 반영되는 경로가 있을 수 있어 여기서 한 번 더 막는다.
        // 같은 슬롯에 두 번 보내면 응답도 두 번 와서 단계가 엉뚱하게 튄다.
        if (IsWaiting)
            return false;

        if (_slotIndex < 0)
        {
            ClientLogger.Error(ClientLogger.UI, "다룰 슬롯이 정해지지 않았다 — Open()을 거치지 않고 열렸다.", this);
            return false;
        }

        // 로그인 전에 보내면 서버가 User를 못 찾아 조용히 버린다 — 클라 입장에선 응답도 오류도
        // 없어서 "눌렀는데 아무 일도 안 일어난다"로만 보인다. 보내기 전에 여기서 끊고 이유를 남긴다.
        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "작업슬롯 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");
            return false;
        }

        return true;
    }

    // 담당 슬롯의 배치 요청을 보낸다. 산업·캐릭터를 0으로 주면 해제다 (클릭 처리에서 호출)
    private void Send(byte industry, long characterId)
    {
        _network.Send(new C_WorkStationAssignRequest
        {
            SlotIndex   = _slotIndex,
            Industry    = industry,
            CharacterId = characterId
        });
    }

    #endregion
}
