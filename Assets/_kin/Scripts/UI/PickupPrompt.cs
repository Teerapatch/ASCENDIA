using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ป้ายกดเก็บของตอนปีน (ป้ายปุ่ม E + คำว่า COLLECT / GATHER) แบบ Screen Space
// วาดบน Canvas Overlay ของตัวเอง ไม่ใช่ข้อความ 3D ในฉาก หินจึงบังไม่ได้
// ทุกเฟรมฉายจุดเหนือของชิ้นนั้นจากโลกขึ้นจอ ขนาดบนจอคงที่ อ่านง่ายในกล้อง Ortho ของฉากปีน
// หน้าตามาจาก Resources/PickupPrompt.prefab (Tools/ASCENDIA/UI/Build Pickup Prompt Prefab) แต่งใน Prefab ได้เลย
// ถ้าไม่มี Prefab จะสร้างแบบเรียบ ๆ จากโค้ดด้วย BuildDefault ตัวเดียวกับที่เครื่องมือใช้
public class PickupPrompt : MonoBehaviour
{
    // ใต้ ScreenFader (-1) จอมืดแล้วป้ายมืดตาม และใต้ HUD (ClimbUI) กับการ์ดเลือกเส้นทาง (0)
    public const int SortingOrder = -2;
    public const string ResourcePath = "PickupPrompt";

    [Header("Parts (ว่างไว้ = หาจากชื่อลูก)")]
    public CanvasGroup group;
    public RectTransform content;   // ส่วนที่เด้ง/ย่อขยาย (ชื่อ Content)
    public RectTransform key;       // ป้ายปุ่ม (ชื่อ Key)
    public Graphic keyGlow;         // แสงหลังป้ายปุ่ม (ชื่อ KeyGlow) ย้อมตามสีของชิ้นนั้น
    public TMP_Text keyText;        // ตัวอักษรปุ่ม (ชื่อ KeyText)
    public TMP_Text label;          // คำอธิบาย (ชื่อ Label)

    [Header("Placement")]
    [Tooltip("ยกป้ายขึ้นจากจุดเหนือของชิ้นนั้นอีกเท่านี้ (พิกเซลที่ความละเอียดอ้างอิง 1080p)")]
    public float screenLift = 26f;
    [Tooltip("ไม่ให้ป้ายชิดขอบจอเกินนี้")]
    public float edgeMargin = 70f;

    [Header("Motion (เวลาจริง)")]
    public float fadeInTime = .16f;
    public float fadeOutTime = .12f;
    [Tooltip("ขนาดตอนเริ่มเด้งเข้า")]
    public float popFrom = .55f;
    [Tooltip("ขนาดตอนหายไป")]
    public float hiddenScale = .8f;
    [Tooltip("ความแข็งของสปริงขนาด (สูง = เร็ว)")]
    public float springStiffness = 380f;
    [Tooltip("แรงหน่วงของสปริง (ต่ำ = เด้งเกินมากขึ้น)")]
    public float springDamping = 17f;
    [Tooltip("ตอนโผล่ลอยขึ้นมาจากต่ำกว่านี้ (พิกเซล)")]
    public float riseFrom = -16f;
    [Tooltip("ป้ายปุ่มเต้นเบา ๆ ระหว่างรอกด")]
    public float pulseAmount = .045f;
    public float pulseSpeed = 3.4f;
    public float glowMinAlpha = .45f;
    public float glowMaxAlpha = .95f;
    [Tooltip("ตอนเก็บ: ป้ายขยายแล้วจางหาย")]
    public float collectTime = .3f;
    public float collectScale = 1.3f;

    private static Canvas canvas;
    private static GameObject prefab;

    private RectTransform rect;
    private Transform target;
    private Vector3 localAnchor;
    private Color accent = Color.white;
    private bool visible;
    private float alpha;
    private float scale;
    private float scaleVelocity;
    private float pulseTime;
    private bool collecting;
    private float collectProgress;
    private Vector2 lastPosition;

    public bool IsVisible => visible;

    // ---------------------------------------------------------------- สร้าง
    // worldAnchor = จุดในโลกที่ป้ายจะตั้งอยู่เหนือ (ขอบบนของชิ้นนั้น) ป้ายตามชิ้นนั้นถ้ามันขยับ
    public static PickupPrompt Create(Transform target, Vector3 worldAnchor, string text, Color accent)
    {
        Canvas root = GetCanvas();
        if (prefab == null) prefab = Resources.Load<GameObject>(ResourcePath);

        GameObject go = prefab != null ? Instantiate(prefab, root.transform, false) : BuildDefault(null, null, null, null, null).gameObject;
        if (prefab == null) go.transform.SetParent(root.transform, false);
        go.name = "PickupPrompt_" + target.name;

        PickupPrompt prompt = go.GetComponent<PickupPrompt>();
        if (prompt == null) prompt = go.AddComponent<PickupPrompt>();
        prompt.Bind(target, worldAnchor, text, accent);
        return prompt;
    }

    private static Canvas GetCanvas()
    {
        if (canvas != null) return canvas; // ฉากถูกโหลดใหม่ = Canvas เดิมถูกลบไปแล้ว สร้างใหม่ในฉากปัจจุบัน
        var go = new GameObject("PickupPromptCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        return canvas; // ไม่มี GraphicRaycaster: ป้ายไม่รับคลิก
    }

    private void Bind(Transform followTarget, Vector3 worldAnchor, string text, Color accentColor)
    {
        rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        target = followTarget;
        localAnchor = target.InverseTransformPoint(worldAnchor);
        accent = accentColor;
        FindParts();
        if (label != null && !string.IsNullOrEmpty(text)) label.text = text;
        if (keyGlow != null)
        {
            Color c = accent;
            c.a = keyGlow.color.a;
            keyGlow.color = c;
        }
        alpha = 0f;
        scale = popFrom;
        Apply(0f);
        if (content != null) content.gameObject.SetActive(false);
    }

    private void FindParts()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        if (content == null) content = Find("Content") as RectTransform;
        if (content == null) content = rect;
        if (key == null) key = Find("Key") as RectTransform;
        if (keyGlow == null) keyGlow = Find("KeyGlow")?.GetComponent<Graphic>();
        if (keyText == null) keyText = Find("KeyText")?.GetComponent<TMP_Text>();
        if (label == null) label = Find("Label")?.GetComponent<TMP_Text>();
    }

    private Transform Find(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == childName) return child;
        return null;
    }

    // ---------------------------------------------------------------- ควบคุม
    public void Show(bool show)
    {
        if (collecting || visible == show) return;
        visible = show;
        if (show)
        {
            if (alpha <= .01f) { scale = popFrom; scaleVelocity = 0f; }
            if (content != null) content.gameObject.SetActive(true);
        }
    }

    // ตอนเก็บสำเร็จ: ชิ้นนั้นกำลังจะถูกลบ ป้ายจึงแยกตัวออกมาเล่นท่าเก็บแล้วลบตัวเอง
    public void Collect()
    {
        if (collecting) return;
        collecting = true;
        visible = false;
        collectProgress = 0f;
        if (content != null) content.gameObject.SetActive(true);
    }

    // ชิ้นนั้นหายไปโดยไม่ได้เก็บ (เปลี่ยนห้อง ฯลฯ)
    public void Dismiss()
    {
        if (!collecting && this != null) Destroy(gameObject);
    }

    // ---------------------------------------------------------------- ทุกเฟรม
    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);

        if (collecting)
        {
            collectProgress += dt / Mathf.Max(.01f, collectTime);
            float k = Mathf.Clamp01(collectProgress);
            float ease = 1f - (1f - k) * (1f - k);
            group.alpha = alpha * (1f - k);
            content.localScale = Vector3.one * Mathf.Lerp(Mathf.Max(scale, 1f), collectScale, ease);
            content.anchoredPosition = new Vector2(0f, 18f * ease);
            if (keyText != null) keyText.color = Color.Lerp(keyText.color, Color.white, k);
            if (keyGlow != null)
            {
                Color c = Color.Lerp(accent, Color.white, .5f);
                c.a = (1f - k) * glowMaxAlpha;
                keyGlow.color = c;
            }
            rect.anchoredPosition = lastPosition;
            if (k >= 1f) Destroy(gameObject);
            return;
        }

        if (target == null) { Destroy(gameObject); return; }

        bool show = visible && target.gameObject.activeInHierarchy && !PlayerStateManager.InputLocked;
        if (!show && alpha <= 0f)
        {
            if (content != null && content.gameObject.activeSelf) content.gameObject.SetActive(false);
            return;
        }

        if (!Place()) show = false;

        alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, dt / Mathf.Max(.01f, show ? fadeInTime : fadeOutTime));
        // สปริงทำให้เด้งเกินนิด ๆ ตอนโผล่ และไม่กระตุกถ้าสลับโผล่/หายกลางทาง
        float goal = show ? 1f : hiddenScale;
        scaleVelocity += (springStiffness * (goal - scale) - springDamping * scaleVelocity) * dt;
        scale += scaleVelocity * dt;
        Apply(dt);
    }

    private bool Place()
    {
        Camera cam = Camera.main;
        if (cam == null || canvas == null) return false;
        Vector3 screen = cam.WorldToScreenPoint(target.TransformPoint(localAnchor));
        if (screen.z < 0f) return false;

        RectTransform canvasRect = (RectTransform)canvas.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local)) return false;
        local.y += screenLift;
        Rect area = canvasRect.rect;
        Vector2 half = rect.rect.size * .5f;
        local.x = Mathf.Clamp(local.x, area.xMin + edgeMargin + half.x, area.xMax - edgeMargin - half.x);
        local.y = Mathf.Clamp(local.y, area.yMin + edgeMargin, area.yMax - edgeMargin - rect.rect.height);
        rect.anchoredPosition = lastPosition = local;
        return true;
    }

    private void Apply(float dt)
    {
        if (group != null) group.alpha = alpha;
        if (content == null) return;
        content.localScale = Vector3.one * Mathf.Max(0f, scale);
        float rise = 1f - alpha;
        content.anchoredPosition = new Vector2(0f, riseFrom * rise * rise);

        pulseTime += dt;
        float wave = .5f + .5f * Mathf.Sin(pulseTime * pulseSpeed);
        if (key != null) key.localScale = Vector3.one * (1f + pulseAmount * wave);
        if (keyGlow != null)
        {
            Color c = keyGlow.color;
            c.a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, wave);
            keyGlow.color = c;
            keyGlow.rectTransform.localScale = Vector3.one * (1f + pulseAmount * 2f * wave);
        }
    }

    // ---------------------------------------------------------------- หน้าตาเริ่มต้น
    // ใช้ทั้งตอนสร้าง Prefab (Tools/ASCENDIA/UI/Build Pickup Prompt Prefab ส่งภาพของอาร์ตมา) และตอนไม่มี Prefab (ส่ง null)
    // keyBack = ป้ายปุ่มสีเข้ม, glow = แสงกลมนุ่มหลังป้ายปุ่ม (ย้อมสีตามชิ้นนั้น), plate = แผ่นพู่กันหลังคำอธิบาย
    public static PickupPrompt BuildDefault(Sprite keyBack, Sprite glow, Sprite plate, TMP_FontAsset font, TMP_FontAsset keyFont)
    {
        var root = new GameObject("PickupPrompt", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.pivot = new Vector2(.5f, 0f); // ฐานป้ายอยู่ที่จุดเหนือของชิ้นนั้น
        rt.sizeDelta = new Vector2(300f, 84f);

        RectTransform content = Part("Content", rt, new Vector2(0f, 0f), rt.sizeDelta);
        content.pivot = new Vector2(.5f, 0f);
        content.anchorMin = content.anchorMax = new Vector2(.5f, 0f);
        content.anchoredPosition = Vector2.zero;

        // แผ่นคำอธิบาย: ภาพ Noti_Frame มีขอบโปร่งใสมาก (แถบจริงกว้าง ~76% สูง ~30% ของภาพ) จึงขยายภาพให้แถบได้ขนาดที่ต้องการ
        Vector2 plateSize = new Vector2(214f, 50f);
        Vector2 platePos = new Vector2(34f, 42f);
        RectTransform plateRect = Part("Plate", content, platePos, plate != null ? new Vector2(plateSize.x / .76f, plateSize.y / .3f) : plateSize);
        Image plateImage = plateRect.gameObject.AddComponent<Image>();
        plateImage.sprite = plate;
        plateImage.color = plate != null ? new Color(1f, 1f, 1f, .92f) : new Color(.03f, .04f, .08f, .8f);
        plateImage.raycastTarget = false;

        TextMeshProUGUI text = Text("Label", content, "COLLECT", font, 24f, new Color(.92f, .94f, 1f), platePos + new Vector2(8f, 2f), new Vector2(180f, 44f));
        text.characterSpacing = 8f;

        // ป้ายปุ่ม E รูปข้าวหลามตัดแบบช่องอาวุธในแคมป์ วางทับปลายซ้ายของแผ่น
        Vector2 keyPos = new Vector2(-92f, 42f);
        RectTransform glowRect = Part("KeyGlow", content, keyPos, new Vector2(150f, 150f));
        Image glowImage = glowRect.gameObject.AddComponent<Image>();
        glowImage.sprite = glow;
        glowImage.color = new Color(1f, 1f, 1f, .8f);
        glowImage.raycastTarget = false;
        if (glow == null) { glowRect.sizeDelta = new Vector2(84f, 84f); glowRect.localEulerAngles = new Vector3(0f, 0f, 45f); }

        RectTransform keyRect = Part("Key", content, keyPos, new Vector2(104f, 104f));
        Image keyImage = keyRect.gameObject.AddComponent<Image>();
        keyImage.sprite = keyBack;
        keyImage.color = keyBack != null ? Color.white : new Color(.04f, .05f, .1f, .95f);
        keyImage.raycastTarget = false;
        if (keyBack == null) { keyRect.sizeDelta = new Vector2(60f, 60f); keyRect.localEulerAngles = new Vector3(0f, 0f, 45f); }

        TextMeshProUGUI letter = Text("KeyText", keyRect, "E", keyFont != null ? keyFont : font, 40f, Color.white, Vector2.zero, new Vector2(80f, 80f));
        if (keyBack == null) letter.rectTransform.localEulerAngles = new Vector3(0f, 0f, -45f);

        PickupPrompt prompt = root.AddComponent<PickupPrompt>();
        prompt.group = root.GetComponent<CanvasGroup>();
        prompt.group.interactable = false;
        prompt.group.blocksRaycasts = false;
        prompt.content = content;
        prompt.key = keyRect;
        prompt.keyGlow = glowImage;
        prompt.keyText = letter;
        prompt.label = text;
        return prompt;
    }

    private static RectTransform Part(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f);
        rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, TMP_FontAsset font, float size, Color color, Vector2 pos, Vector2 box)
    {
        RectTransform rt = Part(name, parent, pos, box);
        if (parent is RectTransform pr && pr.name == "Key") rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = value;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }
}
