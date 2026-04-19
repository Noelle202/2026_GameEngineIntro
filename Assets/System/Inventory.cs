using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 인벤토리 데이터 레이어.
/// MonoBehaviour 싱글톤으로 씬에 배치하거나 PlayerController와 같은 오브젝트에 추가.
///
/// 기능:
///   • 아이템 추가 / 제거
///   • 제작 레시피 검증 및 실행 (재료 3개 → 결과물 1개)
///   • OnInventoryChanged 이벤트로 UI에 변경 알림
/// </summary>
public class Inventory : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────────────────────
    public static Inventory Instance { get; private set; }

    // ── 설정 ──────────────────────────────────────────────────────────────────
    [Header("제작 레시피")]
    [Tooltip("아이템 4(제작 결과물) ScriptableObject — craftingIngredients에 아이템 1·2·3 지정")]
    [SerializeField] private ItemData craftedItemRecipe;   // 아이템 4

    // ── 데이터 ────────────────────────────────────────────────────────────────
    // Key: ItemData, Value: 보유 수량
    private Dictionary<ItemData, int> items = new();

    // ── 이벤트 ───────────────────────────────────────────────────────────────
    /// <summary>아이템이 추가/제거될 때마다 발생 → InventoryUI가 구독해 새로 그림</summary>
    public event Action OnInventoryChanged;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────────────
    /// <summary>아이템을 amount 개 추가한다.</summary>
    public void AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return;

        if (items.ContainsKey(item)) items[item] += amount;
        else                         items[item]  = amount;

        OnInventoryChanged?.Invoke();
    }

    /// <summary>아이템을 amount 개 제거한다. 수량이 부족하면 false 반환.</summary>
    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (item == null || !items.ContainsKey(item)) return false;
        if (items[item] < amount) return false;

        items[item] -= amount;
        if (items[item] <= 0) items.Remove(item);

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>특정 아이템의 보유 수량을 반환한다. 없으면 0.</summary>
    public int GetCount(ItemData item) =>
        item != null && items.TryGetValue(item, out int cnt) ? cnt : 0;

    /// <summary>현재 인벤토리의 전체 목록을 반환한다 (복사본).</summary>
    public Dictionary<ItemData, int> GetAllItems() => new(items);

    // ── 제작 ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// 제작창에 올린 재료 3개가 레시피와 일치하면 제작을 수행한다.
    /// 성공 시 재료를 각 1개 소모하고 결과물을 1개 추가, true 반환.
    /// </summary>
    public bool TryCraft(ItemData[] ingredients)
    {
        if (craftedItemRecipe == null) return false;
        if (ingredients == null || ingredients.Length != 3) return false;

        ItemData[] recipe = craftedItemRecipe.craftingIngredients;
        if (recipe == null || recipe.Length != 3) return false;

        // 재료 배열이 레시피와 순서 무관하게 동일한지 확인
        List<ItemData> recipeList = new(recipe);
        List<ItemData> inputList  = new(ingredients);

        // null 슬롯 허용 안 함
        foreach (var ing in inputList)
            if (ing == null) return false;

        // 순서 무관 매칭
        foreach (var ing in inputList)
        {
            if (!recipeList.Remove(ing)) return false;   // 레시피에 없는 재료
        }
        if (recipeList.Count != 0) return false;

        // 인벤토리에 재료가 충분한지 확인
        foreach (var ing in inputList)
            if (GetCount(ing) < 1) return false;

        // 소모 & 지급
        foreach (var ing in inputList)
            RemoveItem(ing);

        AddItem(craftedItemRecipe);
        return true;
    }

    /// <summary>Inspector 설정 없이 코드에서 직접 레시피 아이템을 설정할 때 사용.</summary>
    public void SetCraftedItemRecipe(ItemData recipe) => craftedItemRecipe = recipe;
    public ItemData CraftedItemRecipe => craftedItemRecipe;
}
