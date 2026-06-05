using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD 프리팹 런타임 빌더
/// Canvas 하위에 모든 HUD 요소를 코드로 생성
/// 실제 Unity 프로젝트에서는 Prefab으로 대체 권장
/// </summary>
public class HUDBuilder : MonoBehaviour
{
    [Header("폰트/스프라이트 (에디터에서 할당)")]
    [SerializeField] private TMP_FontAsset  gameFont;
    [SerializeField] private Sprite         statBarBg;
    [SerializeField] private Sprite         statBarFill;
    [SerializeField] private Sprite         hotbarSlotSprite;
    [SerializeField] private Sprite         hotbarSelectedSprite;

    // ─── 생성된 컴포넌트 참조 ────────────────────────────────
    [HideInInspector] public Slider   healthSlider;
    [HideInInspector] public Slider   hungerSlider;
    [HideInInspector] public Slider   thirstSlider;
    [HideInInspector] public Slider   staminaSlider;
    [HideInInspector] public TextMeshProUGUI timeText;
    [HideInInspector] public TextMeshProUGUI dayText;
    [HideInInspector] public HotbarSlotUI[]  hotbarSlotUIs;

    private Canvas _canvas;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        BuildHUD();
    }

    // ─── HUD 전체 구성 ───────────────────────────────────────
    private void BuildHUD()
    {
        // ── 좌하단 스탯 바 ──
        BuildStatBars();

        // ── 우상단 시간/날짜 ──
        BuildTimeDisplay();

        // ── 하단 중앙 핫바 ──
        BuildHotbar();

        // ── 우상단 미니맵 자리 (카메라 RenderTexture로 채움) ──
        BuildMiniMapFrame();

        // ── 상호작용 프롬프트 (화면 중앙 하단) ──
        BuildInteractPrompt();

        // ── 알림 텍스트 (화면 상단 중앙) ──
        BuildNotificationArea();
    }

    // ─── 스탯 바 (좌하단) ───────────────────────────────────
    private void BuildStatBars()
    {
        var panel = CreatePanel("StatBars",
            anchor: new Vector2(0, 0),
            pivot:  new Vector2(0, 0),
            pos:    new Vector2(20, 20),
            size:   new Vector2(220, 120));

        // HP (빨강)
        healthSlider  = CreateStatBar(panel, "HP",      0,  new Color(0.85f, 0.2f, 0.2f));
        // 배고픔 (주황)
        hungerSlider  = CreateStatBar(panel, "배고픔", 30,  new Color(0.9f,  0.6f, 0.1f));
        // 갈증 (파랑)
        thirstSlider  = CreateStatBar(panel, "갈증",   60,  new Color(0.2f,  0.6f, 0.9f));
        // 스태미나 (노랑)
        staminaSlider = CreateStatBar(panel, "스태미나",90, new Color(0.95f, 0.9f, 0.2f));
    }

    private Slider CreateStatBar(GameObject parent, string label, float yOffset, Color fillColor)
    {
        // 라벨
        var labelObj = CreateText(parent, label, new Vector2(0, -yOffset), new Vector2(60, 22), 11);
        labelObj.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

        // 배경
        var barBg = CreateImage(parent, $"{label}_Bg",
            new Vector2(65, -yOffset - 1), new Vector2(150, 14),
            new Color(0.1f, 0.1f, 0.1f, 0.8f));

        // 슬라이더
        var sliderGo = new GameObject($"{label}_Slider", typeof(Slider));
        sliderGo.transform.SetParent(parent.transform, false);
        var sliderRT = sliderGo.GetComponent<RectTransform>();
        sliderRT.anchorMin = sliderRT.anchorMax = new Vector2(0, 1);
        sliderRT.pivot     = new Vector2(0, 1);
        sliderRT.anchoredPosition = new Vector2(65, -yOffset);
        sliderRT.sizeDelta        = new Vector2(150, 14);

        var slider = sliderGo.GetComponent<Slider>();
        slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
        slider.direction = Slider.Direction.LeftToRight;

        // 필 영역
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        var faRT = fillArea.GetComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.offsetMin = Vector2.zero; faRT.offsetMax = Vector2.zero;

        var fill = CreateImage(fillArea, "Fill",
            Vector2.zero, Vector2.zero, fillColor);
        fill.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        fill.GetComponent<RectTransform>().anchorMax = Vector2.one;
        fill.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        fill.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        slider.fillRect = fill.GetComponent<RectTransform>();
        return slider;
    }

    // ─── 시간/날짜 표시 (우상단) ────────────────────────────
    private void BuildTimeDisplay()
    {
        var panel = CreatePanel("TimeDisplay",
            anchor: new Vector2(1, 1),
            pivot:  new Vector2(1, 1),
            pos:    new Vector2(-130, -10),
            size:   new Vector2(120, 50));

        SetPanelStyle(panel, new Color(0, 0, 0, 0.5f));

        var timeGo = CreateText(panel, "00:00", new Vector2(0, -8),  new Vector2(110, 28), 20);
        var dayGo  = CreateText(panel, "Day 1", new Vector2(0, -32), new Vector2(110, 18), 12);

        timeText = timeGo.GetComponent<TextMeshProUGUI>();
        dayText  = dayGo .GetComponent<TextMeshProUGUI>();

        timeText.alignment = TextAlignmentOptions.Center;
        dayText .alignment = TextAlignmentOptions.Center;
        dayText .color     = new Color(0.7f, 0.7f, 0.7f);
    }

    // ─── 핫바 (하단 중앙) ───────────────────────────────────
    private void BuildHotbar()
    {
        const int SLOTS = 8;
        const float SLOT_SIZE = 52f;
        const float SPACING   = 4f;
        float totalWidth = SLOTS * SLOT_SIZE + (SLOTS - 1) * SPACING;

        var panel = CreatePanel("Hotbar",
            anchor: new Vector2(0.5f, 0),
            pivot:  new Vector2(0.5f, 0),
            pos:    new Vector2(0, 12),
            size:   new Vector2(totalWidth + 16, SLOT_SIZE + 16));

        SetPanelStyle(panel, new Color(0.05f, 0.05f, 0.05f, 0.75f));

        hotbarSlotUIs = new HotbarSlotUI[SLOTS];

        for (int i = 0; i < SLOTS; i++)
        {
            float x = -totalWidth * 0.5f + i * (SLOT_SIZE + SPACING) + SLOT_SIZE * 0.5f;

            var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform));
            slotGo.transform.SetParent(panel.transform, false);
            var rt = slotGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta        = new Vector2(SLOT_SIZE, SLOT_SIZE);

            // 슬롯 배경
            var bg = slotGo.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            // 아이템 아이콘
            var iconGo = CreateImage(slotGo, "Icon", Vector2.zero,
                new Vector2(SLOT_SIZE - 8, SLOT_SIZE - 8), Color.white);
            iconGo.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            iconGo.GetComponent<RectTransform>().anchorMax = Vector2.one;
            iconGo.GetComponent<RectTransform>().offsetMin = new Vector2(4, 4);
            iconGo.GetComponent<RectTransform>().offsetMax = new Vector2(-4, -4);
            iconGo.GetComponent<Image>().enabled = false;

            // 수량 텍스트
            var countGo = CreateText(slotGo, "", new Vector2(2, -2),
                new Vector2(SLOT_SIZE - 4, 16), 10);
            countGo.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            countGo.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
            var countTMP = countGo.GetComponent<TextMeshProUGUI>();
            countTMP.alignment = TextAlignmentOptions.BottomRight;
            countTMP.enabled   = false;

            // 키 힌트 (1~8)
            var keyHintGo = CreateText(slotGo, (i + 1).ToString(),
                new Vector2(0, 0), new Vector2(SLOT_SIZE, 14), 9);
            keyHintGo.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
            keyHintGo.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            keyHintGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -2);
            var keyTMP = keyHintGo.GetComponent<TextMeshProUGUI>();
            keyTMP.alignment = TextAlignmentOptions.TopLeft;
            keyTMP.color     = new Color(0.5f, 0.5f, 0.5f);
            keyTMP.fontSize  = 9;

            var slotUI = slotGo.AddComponent<HotbarSlotUI>();
            // HotbarSlotUI 내부에서 icon/count 필드를 찾아 연결 (리플렉션 대신 직접 할당)
            typeof(HotbarSlotUI)
                .GetField("iconImage",  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(slotUI, iconGo.GetComponent<Image>());
            typeof(HotbarSlotUI)
                .GetField("countText",  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(slotUI, countTMP);

            hotbarSlotUIs[i] = slotUI;
        }
    }

    // ─── 미니맵 프레임 (우상단) ─────────────────────────────
    private void BuildMiniMapFrame()
    {
        var frame = CreatePanel("MiniMapFrame",
            anchor: new Vector2(1, 1),
            pivot:  new Vector2(1, 1),
            pos:    new Vector2(-10, -70),
            size:   new Vector2(160, 160));

        // 원형 마스크용 Image
        var mask = frame.AddComponent<Mask>();
        var maskImg = frame.AddComponent<Image>();
        maskImg.color = Color.white;

        // RawImage — 미니맵 카메라 RenderTexture 연결 자리
        var rawGo = new GameObject("MiniMapRaw", typeof(RawImage));
        rawGo.transform.SetParent(frame.transform, false);
        var rawRT = rawGo.GetComponent<RectTransform>();
        rawRT.anchorMin = Vector2.zero;
        rawRT.anchorMax = Vector2.one;
        rawRT.offsetMin = rawRT.offsetMax = Vector2.zero;

        // 테두리 (위험도 색상)
        var border = CreateImage(frame, "Border", Vector2.zero, new Vector2(164, 164),
            new Color(0.2f, 0.9f, 0.2f));
        border.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        border.GetComponent<RectTransform>().anchorMax = Vector2.one;
        border.GetComponent<RectTransform>().offsetMin = new Vector2(-2, -2);
        border.GetComponent<RectTransform>().offsetMax = new Vector2( 2,  2);
        border.GetComponent<Image>().raycastTarget = false;

        // 구역명 텍스트
        CreateText(frame, "섬 (안전지대)", new Vector2(0, -168), new Vector2(160, 18), 10);
    }

    // ─── 상호작용 프롬프트 (화면 중앙 하단) ────────────────
    private void BuildInteractPrompt()
    {
        var panel = CreatePanel("InteractPrompt",
            anchor: new Vector2(0.5f, 0.5f),
            pivot:  new Vector2(0.5f, 0),
            pos:    new Vector2(0, -100),
            size:   new Vector2(280, 36));

        SetPanelStyle(panel, new Color(0, 0, 0, 0.65f));

        var txt = CreateText(panel, "[F] 상호작용", Vector2.zero, new Vector2(260, 36), 13);
        txt.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        panel.SetActive(false);  // 기본 숨김
    }

    // ─── 알림 영역 (상단 중앙) ──────────────────────────────
    private void BuildNotificationArea()
    {
        var notifGo = CreateText(gameObject, "", new Vector2(0, -80), new Vector2(400, 40), 14);
        notifGo.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1);
        notifGo.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
        notifGo.GetComponent<RectTransform>().pivot     = new Vector2(0.5f, 1);
        var notifTMP = notifGo.GetComponent<TextMeshProUGUI>();
        notifTMP.alignment = TextAlignmentOptions.Center;
        notifTMP.color     = new Color(1f, 0.9f, 0.3f);
        notifGo.SetActive(false);
    }

    // ─── 공통 UI 빌더 유틸 ──────────────────────────────────

    private GameObject CreatePanel(string name, Vector2 anchor, Vector2 pivot,
        Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot     = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return go;
    }

    private void SetPanelStyle(GameObject panel, Color bgColor)
    {
        var img = panel.AddComponent<Image>();
        img.color = bgColor;
    }

    private GameObject CreateImage(GameObject parent, string name,
        Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot     = new Vector2(0.5f, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        go.GetComponent<Image>().color = color;
        return go;
    }

    private GameObject CreateText(GameObject parent, string content,
        Vector2 pos, Vector2 size, float fontSize)
    {
        var go = new GameObject("Text_" + content.Replace(" ", "_"),
            typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text     = content;
        tmp.fontSize = fontSize;
        tmp.color    = Color.white;
        if (gameFont != null) tmp.font = gameFont;
        return go;
    }
}
