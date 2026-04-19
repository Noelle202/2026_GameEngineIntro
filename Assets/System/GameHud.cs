using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이 중 화면 좌하단에 항상 표시되는 HUD.
///   • 레벨 표시 (Lv.N)
///   • EXP 바 + 수치 (현재EXP / 다음레벨필요EXP)
///   • HP 바 (선택: HealthSystem 연결 시 표시)
///
/// 씬에 이 컴포넌트를 가진 GameObject를 하나 배치하면 됩니다.
/// PlayerController 태그("Player")로 자동 탐색합니다.
/// </summary>
public class GameHUD : MonoBehaviour
{
    // ── 참조 (코드로 자동 생성) ───────────────────────────────────────────────
    private Canvas canvas;

    // 레벨 & EXP
    private Text  levelText;
    private Image expBarFill;
    private Text  expText;

    // HP
    private Image hpBarFill;
    private Text  hpText;

    // 갈무리 진행 안내 (시체 근처에서 F 표시)
    private GameObject carveHintGo;
    private Text       carveHintText;

    // ── 타겟 ─────────────────────────────────────────────────────────────────
    private PlayerController player;
    private HealthSystem      playerHealth;

    // ── 색상 ─────────────────────────────────────────────────────────────────
    private static readonly Color BgDark   = new(0.05f, 0.05f, 0.08f, 0.85f);
    private static readonly Color ExpColor = new(0.30f, 0.80f, 0.30f, 1.00f);
    private static readonly Color HpColor  = new(0.85f, 0.20f, 0.20f, 1.00f);
    private static readonly Color AccentYellow = new(0.95f, 0.80f, 0.20f, 1.00f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        EnsureCanvas();
        BuildHUD();
    }

    private void Start()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go)
        {
            player       = go.GetComponent<PlayerController>();
            playerHealth = go.GetComponent<HealthSystem>();
        }
    }

    private void Update()
    {
        UpdateLevelEXP();
        UpdateHP();
        UpdateCarveHint();
    }

    // ── Canvas 보장 ───────────────────────────────────────────────────────────
    private void EnsureCanvas()
    {
        canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("UICanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
        }
    }

    // ── HUD 빌드 ──────────────────────────────────────────────────────────────
    private void BuildHUD()
    {
        // ── 좌하단 패널 ───────────────────────────────────────────────────────
        var panel = MakePanel("HUDPanel",
            new Vector2(0f,   0f),
            new Vector2(0.28f, 0.18f),
            BgDark, canvas.transform);

        // 레벨 텍스트
        levelText = MakeText(panel.transform, "LevelText", "Lv. 1",
            15, TextAnchor.UpperLeft, AccentYellow,
            new Vector2(0.03f,0.70f), new Vector2(0.60f,0.98f));
        levelText.fontStyle = FontStyle.Bold;

        // EXP 라벨
        MakeText(panel.transform, "EXPLabel", "EXP",
            9, TextAnchor.UpperLeft, new Color(0.7f,0.9f,0.7f),
            new Vector2(0.03f,0.50f), new Vector2(0.18f,0.68f));

        // EXP 바 배경
        var expBg = MakePanel("EXPBarBg",
            new Vector2(0.18f,0.52f), new Vector2(0.97f,0.66f),
            new Color(0.15f,0.15f,0.15f,1f), panel.transform);

        // EXP 바 채우기
        var expFillGo = MakePanel("EXPBarFill",
            Vector2.zero, new Vector2(0f,1f),    // 너비는 Update에서 조정
            ExpColor, expBg.transform);
        expBarFill = expFillGo.GetComponent<Image>();
        expBarFill.rectTransform.anchorMax = new Vector2(0f, 1f);

        // EXP 수치 텍스트
        expText = MakeText(panel.transform, "EXPText", "0 / 100",
            8, TextAnchor.UpperRight, new Color(0.8f,1f,0.8f),
            new Vector2(0.18f,0.36f), new Vector2(0.97f,0.52f));

        // ── HP 바 ─────────────────────────────────────────────────────────────
        MakeText(panel.transform, "HPLabel", "HP",
            9, TextAnchor.UpperLeft, new Color(1f,0.6f,0.6f),
            new Vector2(0.03f,0.16f), new Vector2(0.18f,0.34f));

        var hpBg = MakePanel("HPBarBg",
            new Vector2(0.18f,0.18f), new Vector2(0.97f,0.32f),
            new Color(0.15f,0.15f,0.15f,1f), panel.transform);

        var hpFillGo = MakePanel("HPBarFill",
            Vector2.zero, new Vector2(1f,1f),
            HpColor, hpBg.transform);
        hpBarFill = hpFillGo.GetComponent<Image>();

        hpText = MakeText(panel.transform, "HPText", "100 / 100",
            8, TextAnchor.UpperRight, new Color(1f,0.8f,0.8f),
            new Vector2(0.18f,0.04f), new Vector2(0.97f,0.18f));

        // ── 갈무리 힌트 (화면 중앙 하단) ────────────────────────────────────
        carveHintGo = MakePanel("CarveHint",
            new Vector2(0.3f,0.06f), new Vector2(0.7f,0.12f),
            new Color(0f,0f,0f,0.75f), canvas.transform);

        carveHintText = MakeText(carveHintGo.transform, "CarveHintText",
            "[F] 갈무리 중...",
            11, TextAnchor.MiddleCenter, AccentYellow,
            Vector2.zero, Vector2.one);

        carveHintGo.SetActive(false);
    }

    // ── Update: 레벨/EXP ─────────────────────────────────────────────────────
    private void UpdateLevelEXP()
    {
        if (player == null) return;

        levelText.text = $"Lv. {player.Level}";

        float cur  = player.CurrentEXP;
        float need = player.ExpToNextLevel;
        float ratio = (need > 0f && need < float.MaxValue) ? Mathf.Clamp01(cur / need) : 1f;

        expBarFill.rectTransform.anchorMax = new Vector2(ratio, 1f);

        string needStr = need >= float.MaxValue ? "MAX" : need.ToString("F0");
        expText.text = $"{cur:F0} / {needStr}";
    }

    // ── Update: HP ───────────────────────────────────────────────────────────
    private void UpdateHP()
    {
        if (playerHealth == null) { hpBarFill.rectTransform.anchorMax = new Vector2(1f,1f); return; }

        float ratio = playerHealth.MaxHP > 0f
            ? Mathf.Clamp01(playerHealth.CurrentHP / playerHealth.MaxHP)
            : 0f;

        hpBarFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        hpText.text = $"{playerHealth.CurrentHP:F0} / {playerHealth.MaxHP:F0}";
    }

    // ── Update: 갈무리 힌트 ──────────────────────────────────────────────────
    /// <summary>
    /// 씬에 죽은 적이 있고 플레이어가 carveRange 안에 있으면 [F] 힌트를 표시.
    /// </summary>
    private void UpdateCarveHint()
    {
        if (player == null) { carveHintGo.SetActive(false); return; }

        // EnemyController 중 isDead인 것을 찾는 건 무겁기 때문에
        // 간단히 F 키를 누르고 있을 때만 진행 표시
        bool show  = Input.GetKey(KeyCode.F);
        carveHintGo.SetActive(show);
    }

    // ── UI 헬퍼 ──────────────────────────────────────────────────────────────
    private GameObject MakePanel(string name, Vector2 ancMin, Vector2 ancMax,
        Color color, Transform parent)
    {
        var go  = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        var rt  = img.rectTransform;
        rt.anchorMin = ancMin;
        rt.anchorMax = ancMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    private Text MakeText(Transform parent, string name, string content,
        int size, TextAnchor anchor, Color color,
        Vector2 ancMin, Vector2 ancMax)
    {
        var go  = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<Text>();
        txt.text      = content;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = size;
        txt.color     = color;
        txt.alignment = anchor;
        txt.raycastTarget = false;
        var rt = txt.rectTransform;
        rt.anchorMin = ancMin;
        rt.anchorMax = ancMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return txt;
    }
}