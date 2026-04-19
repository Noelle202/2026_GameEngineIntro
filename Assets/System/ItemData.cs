using UnityEngine;

/// <summary>
/// 아이템 하나를 정의하는 ScriptableObject.
/// 생성: Assets 우클릭 → Create → MechanicApocalypse → ItemData
/// </summary>
[CreateAssetMenu(menuName = "MechanicApocalypse/ItemData", fileName = "NewItem")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string itemName    = "새 아이템";
    [TextArea] public string description = "";
    public Sprite icon;                       // 인벤토리 슬롯에 표시할 아이콘

    [Header("분류")]
    public ItemType itemType  = ItemType.Material;

    [Header("제작")]
    [Tooltip("이 아이템이 제작 결과물일 때 필요한 재료 (최대 3개)")]
    public ItemData[] craftingIngredients = new ItemData[0];  // 결과물 측에서 정의
}

public enum ItemType
{
    Material,   // 소재 (아이템 1·2·3)
    Crafted,    // 제작 결과물 (아이템 4)
    Key,        // 열쇠
    CardKey,    // 카드키
}
