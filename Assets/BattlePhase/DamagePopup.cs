using TMPro;
using UnityEngine;
using UnityEngine.UI;

// เลขดาเมจ / ข้อความลอย แบบ UI บนจอ (ชั้น DamagePopup_Layer ของ BattleUI ดู BattleUILayers)
// ยึดจุดในโลก 3D แล้วฉายลงจอทุกเฟรม จึงไม่จมเข้าโมเดล และวาดเหนือหลอดศัตรูแต่ใต้ UI อื่นของการต่อสู้
// ทุกอย่างเดินด้วยเวลาจริง (unscaled) จึงเล่นทะลุ Hitstop / สโลว์โมชันตอนแพร์รี่
// หน้าตาอยู่ใน DamagePopup_Prefab (สร้างด้วย Tools/ASCENDIA/UI/Build Damage Popup Prefab) แต่งต่อได้ แต่เก็บชื่อลูกไว้
public class DamagePopup : MonoBehaviour
{
    public enum Style { Number, Critical, Label }

    [Header("ชิ้นส่วน")]
    public RectTransform content;
    public TextMeshProUGUI text;
    [Tooltip("แสงฟุ้งหลังตัวเลข (DamagePopup_Glow) ย้อมตามสีของเลข")]
    public Image glow;
    [Tooltip("ปื้นพู่กันสีเข้มหลังตัวเลข (TitleBack)")]
    public Image brush;
    [Tooltip("ป้ายข้อความ (Noti_Frame) ใช้กับ Label")]
    public Image plate;
    [Tooltip("เส้นพู่กันสว่างพาดผ่านตอนคริติคอล")]
    public Image streak;
    public CanvasGroup group;

    [Header("ขนาดตัวอักษร (พิกเซลที่ 1080p)")]
    public float numberSize = 58f;
    public float criticalSize = 82f;
    public float labelSize = 34f;

    [Header("เวลา (วินาทีจริง)")]
    public float numberLife = 0.95f;
    public float criticalLife = 1.25f;
    public float labelLife = 1.45f;
    [Tooltip("ช่วงเด้งเข้า")]
    public float popTime = 0.18f;
    [Tooltip("ช่วงจางหายท้ายสุด")]
    public float fadeTime = 0.3f;
    [Tooltip("ช่วงที่ตัวเลขเป็นสีขาวก่อนกลับเป็นสีจริง (แฟลชตอนกระทบ)")]
    public float flashTime = 0.1f;

    [Header("การเคลื่อนที่ (พิกเซลที่ 1080p)")]
    public float rise = 70f;
    public float labelRise = 40f;
    [Tooltip("ลอยเฉียงออกข้างแบบสุ่ม")]
    public float drift = 26f;
    public float popScale = 1.55f;
    public float criticalPopScale = 2.1f;
    public float labelPopScale = 1.25f;
    [Tooltip("สั่นตอนคริติคอลเด้งเข้า")]
    public float criticalShake = 8f;
    [Tooltip("เอียงตอนเกิด (องศา) แล้วค่อยตั้งตรง คริติคอลเอียงมากกว่า")]
    public float numberTilt = 5f;
    public float criticalTilt = 10f;
    [Tooltip("ห่างขอบจออย่างน้อย")]
    public float edgeMargin = 40f;

    [Header("สี")]
    public Color brushColor = new Color(0.03f, 0.03f, 0.07f, 0.7f);
    public Color plateColor = new Color(1f, 1f, 1f, 0.92f);
    [Range(0f, 1f)] public float glowAlpha = 0.4f;
    [Range(0f, 1f)] public float streakAlpha = 0.85f;

    public Vector3 Anchor { get; private set; }
    public float Age { get; private set; }
    public Style CurrentStyle { get; private set; }

    private Vector2 offset;
    private Color color;
    private float life, driftSign, tilt;
    private Canvas canvas;
    private Camera cam;
    private Vector2 brushSize, plateSize, glowSize, streakSize;

    // screenOffset เป็นพิกเซลที่ 1080p (กันเลขซ้อนกันเวลาโดนหลายฮิต)
    public void Play(Vector3 worldAnchor, Vector2 screenOffset, string value, Style style, Color tint, Canvas rootCanvas)
    {
        gameObject.SetActive(true); // แม่แบบปิดไว้ ต้องเปิดก่อนวัดขนาดตัวอักษร
        Anchor = worldAnchor;
        offset = screenOffset;
        CurrentStyle = style;
        color = tint;
        canvas = rootCanvas;
        cam = Camera.main;
        Age = 0f;
        driftSign = Random.value < 0.5f ? -1f : 1f;

        bool label = style == Style.Label;
        bool critical = style == Style.Critical;
        life = label ? labelLife : critical ? criticalLife : numberLife;
        tilt = label ? 0f : (critical ? criticalTilt : numberTilt) * -driftSign * Random.Range(0.6f, 1f);

        if (text != null)
        {
            text.text = value;
            text.fontSize = label ? labelSize : critical ? criticalSize : numberSize;
            text.fontStyle = label || critical ? FontStyles.Italic : FontStyles.Normal;
            text.characterSpacing = label ? 6f : critical ? 2f : 0f;
            text.color = Color.white;
        }

        // ขนาดพื้นตามความกว้างตัวอักษรจริง
        Vector2 textSize = text != null ? text.GetPreferredValues(value) : new Vector2(120f, 60f);
        float size = text != null ? text.fontSize : 60f;
        // ภาพ TitleBack: ปื้นพู่กันจริงสูงราว 17% และกว้างราว 88% ของภาพ
        brushSize = new Vector2((textSize.x + size * 1.3f) / 0.88f, size * 0.85f / 0.17f);
        // ภาพ Noti_Frame มีขอบโปร่งใสเยอะ: แถบจริงกว้างราว 75% และสูงราว 30% ของภาพ
        plateSize = new Vector2((textSize.x + size * 2.2f) / 0.75f, size * 1.7f / 0.3f);
        glowSize = Vector2.one * Mathf.Max(textSize.x * 1.3f, size * (critical ? 3.6f : 2.6f));
        streakSize = new Vector2(textSize.x + size * 3.2f, size * 0.6f);

        SetActive(brush, !label);
        SetActive(plate, label);
        SetActive(streak, critical);
        SetActive(glow, true);
        if (brush != null) brush.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-4f, 4f));
        if (streak != null) streak.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -driftSign * Random.Range(10f, 18f));

        Tick(0f);
    }

    // ภาพที่ไม่มี sprite (หน้าตาพื้นฐานจากโค้ด) ปิดไว้เสมอ ไม่งั้นจะเป็นสี่เหลี่ยมขาว
    private static void SetActive(Image image, bool on)
    {
        if (image == null) return;
        on &= image.sprite != null;
        if (image.gameObject.activeSelf != on) image.gameObject.SetActive(on);
    }

    private void LateUpdate()
    {
        Tick(Time.unscaledDeltaTime);
    }

    private void Tick(float dt)
    {
        Age += dt;
        if (Age >= life)
        {
            Destroy(gameObject);
            return;
        }

        bool label = CurrentStyle == Style.Label;
        bool critical = CurrentStyle == Style.Critical;
        float t = Age / life;
        float fade = Mathf.Clamp01((life - Age) / Mathf.Max(0.01f, fadeTime)); // 1 -> 0 ช่วงท้าย
        float fadeK = 1f - fade;

        // เด้งเข้า: เล็ก -> ใหญ่เกิน -> ลงมาที่ 1 แล้วหดเล็กน้อยตอนจาง
        float peak = label ? labelPopScale : critical ? criticalPopScale : popScale;
        float scale = Pop(Age / Mathf.Max(0.01f, popTime), peak) * Mathf.Lerp(1f, 0.8f, fadeK);
        float settle = EaseOutCubic(Mathf.Clamp01(Age / (popTime * 2.5f)));
        if (content != null)
        {
            content.localScale = Vector3.one * scale;
            content.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(tilt, tilt * 0.2f, settle));
        }

        // ตำแหน่งบนจอ: ลอยขึ้นแบบผ่อนช้า เฉียงออกข้างนิดหน่อย ตอนจางลอยต่ออีกเล็กน้อย
        float lift = (label ? labelRise : rise) * EaseOutCubic(t) + fadeK * 18f;
        Vector2 move = new Vector2(label ? 0f : drift * driftSign * EaseOutCubic(t), lift);
        if (critical && Age < popTime * 1.5f)
            move += Random.insideUnitCircle * criticalShake * (1f - Age / (popTime * 1.5f));

        bool visible = Place(offset + move);
        if (group != null) group.alpha = visible ? fade : 0f;

        // ตัวเลขขาวแวบตอนกระทบแล้วเป็นสีจริง (ป้ายข้อความใช้สีจริงตลอด)
        if (text != null)
            text.color = label ? color : Color.Lerp(Color.white, color, Mathf.Clamp01(Age / Mathf.Max(0.01f, flashTime)));

        float wipe = EaseOutCubic(Mathf.Clamp01(Age / (popTime * 0.8f)));
        if (brush != null && brush.gameObject.activeSelf)
        {
            brush.rectTransform.sizeDelta = brushSize;
            brush.fillAmount = wipe;
            brush.color = brushColor;
        }
        if (plate != null && plate.gameObject.activeSelf)
        {
            plate.rectTransform.sizeDelta = plateSize;
            plate.fillAmount = wipe;
            plate.color = plateColor;
        }
        if (streak != null && streak.gameObject.activeSelf)
        {
            streak.rectTransform.sizeDelta = new Vector2(streakSize.x, streakSize.y * Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(Age / 0.5f)));
            streak.fillAmount = EaseOutCubic(Mathf.Clamp01(Age / (popTime * 0.6f)));
            Color c = Color.Lerp(color, Color.white, 0.6f);
            c.a = streakAlpha * (1f - Mathf.Clamp01(Age / 0.5f));
            streak.color = c;
        }
        if (glow != null && glow.gameObject.activeSelf)
        {
            // แฟลชแรงตอนเกิดแล้วเหลือแสงอ่อน ๆ
            float flash = 1f + 1.6f * Mathf.Exp(-Age * 10f);
            Color g = color;
            g.a = Mathf.Min(0.8f, glowAlpha * flash * (critical ? 1.3f : label ? 0.7f : 1f));
            glow.color = g;
            glow.rectTransform.sizeDelta = glowSize * Mathf.Lerp(0.7f, 1.1f, EaseOutCubic(Mathf.Clamp01(Age / 0.3f)));
        }
    }

    // ฉายจุดยึดลงจอแล้วบวกระยะ (พิกเซลที่ 1080p) คืน false ถ้าจุดอยู่หลังกล้อง
    private bool Place(Vector2 pixelOffset)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;
        Vector3 screen = cam.WorldToScreenPoint(Anchor);
        if (screen.z <= 0f) return false;

        float factor = canvas != null ? canvas.scaleFactor : 1f;
        float margin = edgeMargin * factor;
        float x = Mathf.Clamp(screen.x + pixelOffset.x * factor, margin, Screen.width - margin);
        float y = Mathf.Clamp(screen.y + pixelOffset.y * factor, margin, Screen.height - margin);
        transform.position = new Vector3(x, y, 0f);
        return true;
    }

    // 0..1 -> สเกล: จาก 0.3 พุ่งไป peak ในช่วงแรก แล้วค่อยลงมา 1
    private static float Pop(float x, float peak)
    {
        if (x >= 1f) return 1f;
        const float split = 0.35f;
        if (x < split) return Mathf.LerpUnclamped(0.3f, peak, EaseOutCubic(x / split));
        return Mathf.LerpUnclamped(peak, 1f, EaseOutCubic((x - split) / (1f - split)));
    }

    private static float EaseOutCubic(float x)
    {
        x = 1f - Mathf.Clamp01(x);
        return 1f - x * x * x;
    }

    // ---------------------------------------------------------------- สร้างหน้าตา
    // ใช้ทั้งตัวสร้าง Prefab ใน Editor (AscendiaBuildMenusUI) และตอนรันถ้า Prefab ยังเป็นแบบเก่า
    // root = GameObject ที่จะกลายเป็นป็อปอัป (ลูกเดิมควรลบออกก่อน) sprite ที่เป็น null จะถูกปิดไว้
    public static DamagePopup Build(GameObject root, Sprite glowSprite, Sprite brushSprite, Sprite plateSprite, TMP_FontAsset font, Material textMaterial)
    {
        RectTransform rt = root.GetComponent<RectTransform>();
        if (rt == null) rt = root.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(300f, 120f);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null) group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        DamagePopup popup = root.GetComponent<DamagePopup>();
        if (popup == null) popup = root.AddComponent<DamagePopup>();
        popup.group = group;

        popup.content = Child("Content", rt, new Vector2(300f, 120f));
        popup.glow = FillImage("Glow", popup.content, glowSprite, new Vector2(240f, 240f), false);
        popup.streak = FillImage("Streak", popup.content, brushSprite, new Vector2(420f, 30f), true);
        popup.brush = FillImage("Brush", popup.content, brushSprite, new Vector2(260f, 80f), true);
        popup.plate = FillImage("Plate", popup.content, plateSprite, new Vector2(600f, 300f), true);

        RectTransform textRect = Child("Text", popup.content, new Vector2(800f, 200f));
        TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        if (textMaterial != null) text.fontSharedMaterial = textMaterial;
        text.text = "999";
        text.fontSize = 58f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        popup.text = text;
        return popup;
    }

    private static RectTransform Child(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        return rt;
    }

    private static Image FillImage(string name, Transform parent, Sprite sprite, Vector2 size, bool filled)
    {
        RectTransform rt = Child(name, parent, size);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        if (filled && sprite != null)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
        if (sprite == null) rt.gameObject.SetActive(false);
        return image;
    }

    // วัสดุตัวอักษร: ขอบเข้ม + เงาตกกระทบ ให้อ่านออกบนฉากสว่าง/มืดและบนหลอดเลือด
    public static Material CreateTextMaterial(TMP_FontAsset font)
    {
        if (font == null || font.material == null) return null;
        var mat = new Material(font.material) { name = font.name + " DamagePopup" };
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.03f, 0.07f, 1f));
        mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.65f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.55f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.65f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
        return mat;
    }
}
