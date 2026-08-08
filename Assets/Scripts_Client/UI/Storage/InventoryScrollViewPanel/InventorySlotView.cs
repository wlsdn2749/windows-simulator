using TMPro;
using UnityEngine;

/// <summary>
/// 인벤토리 한 칸의 표시. 프리팹에 붙는다.
/// 비어 있는 칸은 파괴하지 않고 <see cref="Clear"/>로 비워 두었다가 재사용한다
/// — 채취가 도는 동안 생성·파괴가 반복되면 GC 부담이 쌓인다.
/// </summary>
public class InventorySlotView : MonoBehaviour
{
    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("아이템 이름 (ItemTable에서 조회)")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("보유 수량")]
    private TMP_Text countText = null!;

    /// <summary>이 칸이 그리고 있는 아이템. 비어 있으면 0.</summary>
    public int ItemId { get; private set; }

    /// <summary>아이템이 없는 빈 칸인가.</summary>
    public bool IsEmpty => ItemId == 0;

    /// <summary>아이템을 표시한다 (InventoryPanelUI가 호출).</summary>
    public void Bind(int itemId, int count)
    {
        ItemId         = itemId;
        nameText.text  = GameDataLoader.GetItemName(itemId);
        countText.text = count.ToString();
    }

    /// <summary>칸을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.</summary>
    public void Clear()
    {
        ItemId         = 0;
        nameText.text  = "";
        countText.text = "";
    }
}
