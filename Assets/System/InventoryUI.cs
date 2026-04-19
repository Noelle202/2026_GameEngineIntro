using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Tab 키로 열고 닫는 인벤토리 + 제작창 UI.
/// uGUI를 코드로 직접 생성하므로 별도 프리팹 없이 동작합니다.
///
/// 씬에 Canvas(Screen Space - Overlay)가 없으면 자동 생성.
/// Inventory 싱글톤에 의존합니다.
///
/// 레이아웃:
///   ┌──────────────────────────────────────┐
///   │  [인벤토리]          [제작창]        │
///   │  ┌──────────┐        ┌──────────┐   │
///   │  │ 슬롯 목록 │        │ [결과물] │   │
///   │  │ (스크롤)  │        │  슬롯×3  │   │
///   │  └──────────┘        │ [제작하기]│   │
///   │                      └──────────┘   │
///   └──────────────────────────────────────┘
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // ── 참조 (자동 생성) ─────────────────────────────────────────────────────
    private Canvas      canvas;
    private GameObject  rootPanel;

    // 인벤토리 슬롯 컨테이너
    private Transform   inventoryGrid;
    private readonly List<InventorySlotUI> invSlots = new();

    // 제작 슬롯 (3개)
    private CraftSlotUI[]  craftSlots = new CraftSlotUI[3];
    private Image          craftResultIcon;
    private Text           craftResultName;
    private Button         craftButton;

    // 드래그 중인 아이템 표시용 고스트 이미지
    private Image dragGhost;
    private ItemData draggingItem;
    private int      draggingFromSlot = -1;   // -1 = 인벤토리 슬롯 인덱스 기반 아님

    private bool isOpen = false;

    // ── 색상·크기 상수 ────────────────────────────────────────────────────────
    private static readonly Color PanelBg   = new(0.10f, 0.10f, 0.15f, 0.95f);
    private static readonly Color SlotBg    = new(0.20f, 0.20f, 0.25f, 1.00f);
    private static readonly Color SlotHover = new(0.30f, 0.30f, 0.40f, 1.00f);
    private static readonly Color AccentCol = new(0.90f, 0.70f, 0.20f, 1.00f);
    private static readonly Color TextWhite = Color.white;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        EnsureCanvas();
        BuildUI();
        rootPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged += RefreshInventory;
    }

    private void OnDisable()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= RefreshInventory;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
            ToggleUI();

        // 드래그 고스트를 마우스를 따라 이동
        if (draggingItem != null && dragGhost != null)
            dragGhost.rectTransform.position = Input.mousePosition;
    }

    // ── Toggle ────────────────────────────────────────────────────────────────
    private void ToggleUI()
    {
        isOpen = !isOpen;
        rootPanel.SetActive(isOpen);
        if (isOpen) RefreshInventory();
        // 인벤토리 열릴 때 게임 일시정지 여부는 프로젝트 정책에 맞게 추가
    }

    // ── Canvas 보장 ───────────────────────────────────────────────────────────
    private void EnsureCanvas()
    {
        canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("UICanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }
    }

    // ── UI 빌드 ───────────────────────────────────────────────────────────────
    private void BuildUI()
    {
        // 루트 반투명 배경
        rootPanel = CreatePanel(canvas.transform, "InventoryRoot",
            new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.90f), PanelBg);

        // 제목
        var title = CreateText(rootPanel.transform, "Title", "인벤토리 / 제작",
            14, TextAnchor.UpperCenter, AccentCol);
        SetAnchors(title, new Vector2(0f,0.93f), new Vector2(1f,1f));

        // ── 왼쪽: 인벤토리 ──────────────────────────────────────────────────
        var invPanel = CreatePanel(rootPanel.transform, "InvPanel",
            new Vector2(0.02f,0.04f), new Vector2(0.52f,0.90f),
            new Color(0.05f,0.05f,0.10f,0.8f));

        var invLabel = CreateText(invPanel.transform, "InvLabel", "▣ 인벤토리",
            11, TextAnchor.UpperLeft, AccentCol);
        SetAnchors(invLabel, new Vector2(0.02f,0.92f), new Vector2(0.98f,1f));

        // 스크롤뷰
        var scroll = CreateScrollView(invPanel.transform,
            new Vector2(0.01f,0.02f), new Vector2(0.99f,0.91f));
        inventoryGrid = scroll.transform.Find("Viewport/Content");

        // ── 오른쪽: 제작창 ───────────────────────────────────────────────────
        var craftPanel = CreatePanel(rootPanel.transform, "CraftPanel",
            new Vector2(0.54f,0.04f), new Vector2(0.98f,0.90f),
            new Color(0.05f,0.05f,0.10f,0.8f));

        var craftLabel = CreateText(craftPanel.transform, "CraftLabel", "⚙ 제작창",
            11, TextAnchor.UpperLeft, AccentCol);
        SetAnchors(craftLabel, new Vector2(0.04f,0.92f), new Vector2(0.96f,1f));

        // 결과물 슬롯 (상단)
        BuildCraftResult(craftPanel.transform);

        // 재료 슬롯 3개 (중단)
        BuildCraftIngredientSlots(craftPanel.transform);

        // 제작 버튼 (하단)
        craftButton = CreateButton(craftPanel.transform, "CraftBtn", "제 작",
            new Vector2(0.15f,0.04f), new Vector2(0.85f,0.18f), AccentCol, Color.black);
        craftButton.onClick.AddListener(OnCraftClicked);

        // 드래그 고스트 (항상 최상위)
        var ghostGo = new GameObject("DragGhost", typeof(Image));
        ghostGo.transform.SetParent(canvas.transform, false);
        dragGhost = ghostGo.GetComponent<Image>();
        dragGhost.raycastTarget = false;
        dragGhost.color = new Color(1,1,1,0.7f);
        dragGhost.rectTransform.sizeDelta = new Vector2(48, 48);
        ghostGo.SetActive(false);
    }

    // ── 제작 결과물 슬롯 ─────────────────────────────────────────────────────
    private void BuildCraftResult(Transform parent)
    {
        var resultArea = CreatePanel(parent, "ResultArea",
            new Vector2(0.1f,0.68f), new Vector2(0.9f,0.90f),
            new Color(0.12f,0.12f,0.18f,1f));

        var lbl = CreateText(resultArea.transform, "ResultLbl", "제작 결과물",
            9, TextAnchor.UpperCenter, new Color(0.7f,0.7f,0.7f));
        SetAnchors(lbl, new Vector2(0f,0.72f), new Vector2(1f,1f));

        // 아이콘
        var iconGo = new GameObject("ResultIcon", typeof(Image));
        iconGo.transform.SetParent(resultArea.transform, false);
        craftResultIcon = iconGo.GetComponent<Image>();
        craftResultIcon.color = new Color(1,1,1,0.3f);
        var rt = craftResultIcon.rectTransform;
        rt.anchorMin = new Vector2(0.3f, 0.1f);
        rt.anchorMax = new Vector2(0.7f, 0.7f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // 이름
        craftResultName = CreateText(resultArea.transform, "ResultName", "???",
            9, TextAnchor.LowerCenter, TextWhite);
        SetAnchors(craftResultName, new Vector2(0f,0f), new Vector2(1f,0.25f));

        RefreshCraftResult();
    }

    // ── 재료 슬롯 3개 ────────────────────────────────────────────────────────
    private void BuildCraftIngredientSlots(Transform parent)
    {
        float slotW = 0.28f;
        float[] xs  = { 0.04f, 0.36f, 0.68f };

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var slotGo = CreatePanel(parent, $"CraftSlot{i}",
                new Vector2(xs[i], 0.35f), new Vector2(xs[i]+slotW, 0.65f),
                SlotBg);

            var img = new GameObject("Icon", typeof(Image));
            img.transform.SetParent(slotGo.transform, false);
            var icon = img.GetComponent<Image>();
            icon.color = Color.clear;
            var irt = icon.rectTransform;
            irt.anchorMin = new Vector2(0.1f,0.1f);
            irt.anchorMax = new Vector2(0.9f,0.9f);
            irt.offsetMin = irt.offsetMax = Vector2.zero;

            var txt = CreateText(slotGo.transform, "SlotTxt", $"슬롯 {i+1}",
                8, TextAnchor.LowerCenter, new Color(0.6f,0.6f,0.6f));
            SetAnchors(txt, Vector2.zero, new Vector2(1f, 0.3f));

            craftSlots[i] = new CraftSlotUI(slotGo, icon, txt);

            // 드랍 수신
            AddDropHandler(slotGo, (data) => OnDropToCraftSlot(idx, data));
        }
    }

    // ── 인벤토리 새로 그리기 ─────────────────────────────────────────────────
    public void RefreshInventory()
    {
        // 기존 슬롯 전부 제거
        foreach (Transform child in inventoryGrid)
            Destroy(child.gameObject);
        invSlots.Clear();

        if (Inventory.Instance == null) return;

        var allItems = Inventory.Instance.GetAllItems();
        int idx = 0;

        foreach (var kv in allItems)
        {
            ItemData item  = kv.Key;
            int      count = kv.Value;
            int      slotIdx = idx;

            // 슬롯 배경
            var slotGo = new GameObject($"InvSlot_{idx}", typeof(Image), typeof(Button));
            slotGo.transform.SetParent(inventoryGrid, false);
            var slotImg = slotGo.GetComponent<Image>();
            slotImg.color = SlotBg;
            var slotRt = slotImg.rectTransform;
            slotRt.sizeDelta = new Vector2(70, 70);

            // 아이콘
            var iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = item.icon;
            iconImg.color  = item.icon ? Color.white : new Color(0.5f,0.5f,0.5f);
            iconImg.raycastTarget = false;
            var irt = iconImg.rectTransform;
            irt.anchorMin = new Vector2(0.1f,0.2f);
            irt.anchorMax = new Vector2(0.9f,0.9f);
            irt.offsetMin = irt.offsetMax = Vector2.zero;

            // 수량 텍스트
            var cntGo = new GameObject("Count", typeof(Text));
            cntGo.transform.SetParent(slotGo.transform, false);
            var cntTxt = cntGo.GetComponent<Text>();
            cntTxt.text      = count > 1 ? count.ToString() : "";
            cntTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cntTxt.fontSize  = 11;
            cntTxt.fontStyle = FontStyle.Bold;
            cntTxt.color     = Color.yellow;
            cntTxt.alignment = TextAnchor.LowerRight;
            cntTxt.raycastTarget = false;
            var crt = cntTxt.rectTransform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = crt.offsetMax = Vector2.zero;

            // 아이템명 텍스트
            var nameGo = new GameObject("Name", typeof(Text));
            nameGo.transform.SetParent(slotGo.transform, false);
            var nameTxt = nameGo.GetComponent<Text>();
            nameTxt.text      = item.itemName;
            nameTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize  = 7;
            nameTxt.color     = TextWhite;
            nameTxt.alignment = TextAnchor.LowerCenter;
            nameTxt.raycastTarget = false;
            var nrt = nameTxt.rectTransform;
            nrt.anchorMin = new Vector2(0f,0f);
            nrt.anchorMax = new Vector2(1f,0.25f);
            nrt.offsetMin = nrt.offsetMax = Vector2.zero;

            // 드래그 시작
            AddDragHandler(slotGo, item, iconImg);

            invSlots.Add(new InventorySlotUI(slotGo, item, count));
            idx++;
        }

        RefreshCraftResult();
    }

    // ── 제작 결과물 미리보기 갱신 ─────────────────────────────────────────────
    private void RefreshCraftResult()
    {
        if (Inventory.Instance == null || Inventory.Instance.CraftedItemRecipe == null)
        {
            craftResultIcon.sprite = null;
            craftResultIcon.color  = new Color(1,1,1,0.2f);
            craftResultName.text   = "???";
            return;
        }

        // 슬롯 3개가 모두 채워졌고 레시피와 일치하면 결과물 표시
        bool allFilled = true;
        ItemData[] ingredients = new ItemData[3];
        for (int i = 0; i < 3; i++)
        {
            ingredients[i] = craftSlots[i].Item;
            if (ingredients[i] == null) { allFilled = false; }
        }

        ItemData recipe = Inventory.Instance.CraftedItemRecipe;
        if (allFilled && MatchesRecipe(ingredients, recipe))
        {
            craftResultIcon.sprite = recipe.icon;
            craftResultIcon.color  = recipe.icon ? Color.white : new Color(0.8f,0.5f,0.2f);
            craftResultName.text   = recipe.itemName;
        }
        else
        {
            craftResultIcon.sprite = recipe.icon;
            craftResultIcon.color  = new Color(1,1,1,0.25f);
            craftResultName.text   = "???";
        }
    }

    private bool MatchesRecipe(ItemData[] ingredients, ItemData recipe)
    {
        if (recipe.craftingIngredients == null || recipe.craftingIngredients.Length != 3) return false;
        var needed = new List<ItemData>(recipe.craftingIngredients);
        foreach (var ing in ingredients)
        {
            if (ing == null) return false;
            if (!needed.Remove(ing)) return false;
        }
        return true;
    }

    // ── 제작 버튼 ─────────────────────────────────────────────────────────────
    private void OnCraftClicked()
    {
        if (Inventory.Instance == null) return;
        ItemData[] ingredients = new ItemData[3];
        for (int i = 0; i < 3; i++) ingredients[i] = craftSlots[i].Item;

        bool success = Inventory.Instance.TryCraft(ingredients);
        if (success)
        {
            for (int i = 0; i < 3; i++) craftSlots[i].Clear();
            RefreshInventory();
            Debug.Log("[제작] 성공!");
        }
        else
        {
            Debug.Log("[제작] 재료 부족 또는 레시피 불일치");
        }
    }

    // ── 드래그 처리 ───────────────────────────────────────────────────────────
    private void AddDragHandler(GameObject slotGo, ItemData item, Image iconImg)
    {
        var trigger = slotGo.AddComponent<EventTrigger>();

        // BeginDrag
        var beginEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginEntry.callback.AddListener((_) =>
        {
            draggingItem = item;
            dragGhost.sprite = item.icon;
            dragGhost.color  = item.icon ? new Color(1,1,1,0.7f) : new Color(0.6f,0.6f,0.6f,0.7f);
            dragGhost.gameObject.SetActive(true);
        });
        trigger.triggers.Add(beginEntry);

        // EndDrag
        var endEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endEntry.callback.AddListener((_) =>
        {
            draggingItem = null;
            dragGhost.gameObject.SetActive(false);
        });
        trigger.triggers.Add(endEntry);
    }

    private void AddDropHandler(GameObject target, System.Action<ItemData> onDrop)
    {
        var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.Drop };
        entry.callback.AddListener((_) =>
        {
            if (draggingItem != null) onDrop(draggingItem);
        });
        trigger.triggers.Add(entry);

        // 드랍 대상은 raycast를 받아야 함
        var img = target.GetComponent<Image>();
        if (img) img.raycastTarget = true;
    }

    private void OnDropToCraftSlot(int slotIdx, ItemData item)
    {
        craftSlots[slotIdx].SetItem(item);
        RefreshCraftResult();
    }

    // ── UI 헬퍼 ──────────────────────────────────────────────────────────────
    private GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go  = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        SetAnchors(img.rectTransform, anchorMin, anchorMax);
        return go;
    }

    private Text CreateText(Transform parent, string name, string content,
        int fontSize, TextAnchor anchor, Color color)
    {
        var go  = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<Text>();
        txt.text      = content;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = fontSize;
        txt.color     = color;
        txt.alignment = anchor;
        txt.raycastTarget = false;
        return txt;
    }

    private Button CreateButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor)
    {
        var go  = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = bgColor;
        SetAnchors(img.rectTransform, anchorMin, anchorMax);

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(bgColor.r+0.1f, bgColor.g+0.1f, bgColor.b, 1f);
        btn.colors = colors;

        var txtGo = new GameObject("Label", typeof(Text));
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.GetComponent<Text>();
        txt.text      = label;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 12;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = textColor;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        var trt = txt.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        return btn;
    }

    private GameObject CreateScrollView(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        // Viewport
        var viewGo = new GameObject("ScrollView", typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewGo.transform.SetParent(parent, false);
        var viewImg = viewGo.GetComponent<Image>();
        viewImg.color = Color.clear;
        var mask = viewGo.GetComponent<Mask>();
        mask.showMaskGraphic = false;
        SetAnchors(viewImg.rectTransform, anchorMin, anchorMax);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(viewGo.transform, false);
        var vrt = viewportGo.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;

        // Content — GridLayoutGroup
        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var crt = contentGo.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.pivot     = new Vector2(0, 1);
        crt.offsetMin = crt.offsetMax = Vector2.zero;

        var grid = contentGo.GetComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(70, 70);
        grid.spacing         = new Vector2(6, 6);
        grid.padding         = new RectOffset(8, 8, 8, 8);
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewGo.GetComponent<ScrollRect>();
        scroll.content    = crt;
        scroll.viewport   = vrt;
        scroll.horizontal = false;
        scroll.vertical   = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        return viewGo;
    }

    private void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private void SetAnchors(Text txt, Vector2 min, Vector2 max)
        => SetAnchors(txt.rectTransform, min, max);
}

// ── 내부 데이터 클래스 ────────────────────────────────────────────────────────
internal class InventorySlotUI
{
    public GameObject Go;
    public ItemData   Item;
    public int        Count;
    public InventorySlotUI(GameObject go, ItemData item, int count)
    { Go = go; Item = item; Count = count; }
}

internal class CraftSlotUI
{
    public GameObject  Go;
    public Image       Icon;
    public Text        Label;
    public ItemData    Item { get; private set; }

    public CraftSlotUI(GameObject go, Image icon, Text label)
    { Go = go; Icon = icon; Label = label; }

    public void SetItem(ItemData item)
    {
        Item        = item;
        Icon.sprite = item?.icon;
        Icon.color  = item != null ? (item.icon ? Color.white : new Color(0.7f,0.5f,0.3f)) : Color.clear;
        Label.text  = item?.itemName ?? $"슬롯 {Go.name.Replace("CraftSlot","")+1}";
    }

    public void Clear()
    {
        Item        = null;
        Icon.sprite = null;
        Icon.color  = Color.clear;
        Label.text  = "";
    }
}
