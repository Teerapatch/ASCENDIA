using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// แถบสถานะ (บัฟ/ดีบัฟ) ใช้ทั้งใต้หลอดศัตรู (EnemyHUDOverlay) และเหนือหลอด AP ของผู้เล่น (BattleArtUI)
// ไอคอนอย่างเดียว ให้เข้ากับธีมของเกม: เพชรกรอบสีครีม (ภาพเดียวกับช่องธาตุ SigilSlotsUI) พื้นเข้มเจือสีสถานะ แสงเรืองสีสถานะด้านหลัง
// รูปสัญลักษณ์ของสถานะตรงกลาง + จำนวนเทิร์นที่เหลือเป็นตัวเลขเล็กมุมขวาล่าง ป้ายเด้งเข้าตอนติด กระตุกตอนเทิร์นลด จางออกตอนหมด
// ชี้เมาส์ที่ป้าย = กล่องคำอธิบาย (ชื่อ ผลจริงเป็นตัวเลข เหลือกี่เทิร์น) ไม่ใช้ Raycast เพราะ BattleArtUI ปิด Raycast Target ของ UI ทั้งหมด
// ข้อความเป็นภาษาอังกฤษ เพราะฟอนต์ในฉากต่อสู้ไม่มีอักษรไทย
public class StatusBadgeRow : MonoBehaviour
{
    // ข้อมูลของป้าย 1 ใบ
    public struct Info
    {
        public string key;      // ใช้จับคู่ป้ายเดิม (ชนิดสถานะ)
        public string title;    // ชื่อบนป้าย เช่น EXPOSED
        public string detail;   // คำอธิบายผลในกล่องคำอธิบาย
        public string duration; // เช่น "2 turns left", "Until used"
        public string counter;  // ตัวเลขในวงกลม (จำนวนเทิร์น / จำนวนครั้ง) ว่าง = ไม่มีวงกลม
        public Color color;
        public bool isBuff;
        public StatusGlyph glyph;
    }

    public enum StatusGlyph { Up, Down, DoubleUp, Dash, Crosshair, Crack, Hourglass, Flame, Cross }

    public enum Align { Center, Left }

    [Tooltip("ขนาดป้ายทั้งแถบ")]
    public float badgeScale = 1f;
    [Tooltip("ขนาดเพชรของแต่ละป้าย (พิกเซลอ้างอิง 1920x1080)")]
    public float badgeSize = 30f;
    public float spacing = 2f;
    public static readonly Color Cream = new Color(0.94f, 0.91f, 0.83f, 1f);
    public static readonly Color Ink = new Color(0.06f, 0.05f, 0.09f, 1f);
    public Align align = Align.Center;

    public Func<List<Info>> source;
    public TMP_FontAsset font;
    public float alpha = 1f; // คูณความทึบทั้งแถบ (เช่นหลอดศัตรูจางตอนออกนอกจอ)

    private class Badge
    {
        public Info info;
        public RectTransform root;
        public CanvasGroup group;
        public Image glow, gem, frame, icon, counterBack;
        public TextMeshProUGUI counter;
        public float width, x, appear, pulse;
        public bool leaving;
        public string lastCounter;
    }

    private readonly List<Badge> badges = new List<Badge>();
    private readonly List<Info> scratch = new List<Info>();
    private RectTransform rect;

    public static StatusBadgeRow Create(Transform parent, string name, TMP_FontAsset font, Align align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(align == Align.Left ? 0f : 0.5f, 1f);
        rt.sizeDelta = new Vector2(10f, 40f);
        var row = go.AddComponent<StatusBadgeRow>();
        row.font = font;
        row.align = align;
        return row;
    }

    private void Awake() { rect = (RectTransform)transform; }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        scratch.Clear();
        if (source != null)
        {
            List<Info> now = source();
            if (now != null) scratch.AddRange(now);
        }

        // จับคู่ป้ายเดิมตาม key, ป้ายใหม่เด้งเข้า, ป้ายที่หายไปจางออก
        foreach (Badge badge in badges) badge.leaving = true;
        for (int i = 0; i < scratch.Count; i++)
        {
            Info info = scratch[i];
            Badge badge = badges.Find(b => b.info.key == info.key && b.root != null);
            if (badge == null)
            {
                badge = Build(info);
                badges.Add(badge);
                SoundManager.Play(info.isBuff ? Sfx.StatusBuff : Sfx.StatusDebuff);
            }
            badge.leaving = false;
            if (badge.lastCounter != info.counter) badge.pulse = 1f;
            Apply(badge, info);
        }

        // จัดเรียงตามลำดับใน source (ป้ายที่กำลังออกคงตำแหน่งเดิม)
        float total = 0f;
        int shown = 0;
        foreach (Info info in scratch)
        {
            Badge badge = badges.Find(b => b.info.key == info.key && !b.leaving);
            if (badge == null) continue;
            total += badge.width * badgeScale + (shown > 0 ? spacing * badgeScale : 0f);
            shown++;
        }
        float cursor = align == Align.Center ? -total * 0.5f : 0f;
        foreach (Info info in scratch)
        {
            Badge badge = badges.Find(b => b.info.key == info.key && !b.leaving);
            if (badge == null) continue;
            float target = cursor + badge.width * badgeScale * 0.5f;
            badge.x = badge.appear <= 0.01f ? target : Mathf.Lerp(badge.x, target, 1f - Mathf.Exp(-14f * dt));
            cursor += badge.width * badgeScale + spacing * badgeScale;
        }

        for (int i = badges.Count - 1; i >= 0; i--)
        {
            Badge badge = badges[i];
            if (badge.root == null) { badges.RemoveAt(i); continue; }
            badge.appear = Mathf.MoveTowards(badge.appear, badge.leaving ? 0f : 1f, dt * (badge.leaving ? 4f : 5f));
            badge.pulse = Mathf.MoveTowards(badge.pulse, 0f, dt * 3f);
            if (badge.leaving && badge.appear <= 0f)
            {
                Destroy(badge.root.gameObject);
                badges.RemoveAt(i);
                continue;
            }

            float t = badge.appear;
            // เด้งเข้า: เล็ก -> ใหญ่เกิน -> พอดี
            float pop = badge.leaving ? Mathf.Lerp(0.6f, 1f, t) : 1f + Mathf.Sin(t * Mathf.PI) * 0.25f * (1f - t * 0.4f);
            float punch = 1f + badge.pulse * badge.pulse * 0.2f;
            badge.root.localScale = Vector3.one * badgeScale * Mathf.Max(0.01f, (badge.leaving ? pop : Mathf.Lerp(0.3f, 1f, t) * pop) * punch);
            badge.root.anchoredPosition = new Vector2(badge.x, -badgeSize * badgeScale * 0.5f + (1f - t) * 6f);
            badge.group.alpha = t * alpha;

            // กรอบกระพริบสว่างตอนติดใหม่ / เทิร์นลด แสงเรืองเต้นช้า ๆ
            badge.frame.color = Color.Lerp(Cream, Color.white, badge.pulse);
            Color c = badge.info.color;
            float breathe = 0.32f + 0.1f * Mathf.Sin(Time.unscaledTime * 2.4f + badge.x * 0.05f);
            badge.glow.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(breathe + badge.pulse * 0.6f));
        }

        UpdateTooltip();
    }

    private void Apply(Badge badge, Info info)
    {
        badge.info = info;
        badge.lastCounter = info.counter;
        badge.gem.color = Color.Lerp(Ink, info.color, 0.32f);
        badge.icon.sprite = Glyph(info.glyph);
        badge.icon.color = Color.Lerp(info.color, Color.white, 0.62f);
        bool hasCounter = !string.IsNullOrEmpty(info.counter);
        badge.counterBack.gameObject.SetActive(hasCounter);
        badge.counter.text = info.counter ?? "";
        badge.counter.color = info.isBuff ? Cream : new Color(1f, 0.72f, 0.7f, 1f);
        badge.width = badgeSize;
    }

    private Badge Build(Info info)
    {
        var badge = new Badge();
        var go = new GameObject("Status_" + info.key, typeof(RectTransform));
        go.layer = gameObject.layer;
        badge.root = (RectTransform)go.transform;
        badge.root.SetParent(transform, false);
        badge.root.anchorMin = badge.root.anchorMax = new Vector2(align == Align.Left ? 0f : 0.5f, 1f);
        badge.root.sizeDelta = Vector2.one * badgeSize;
        badge.group = go.AddComponent<CanvasGroup>();
        badge.group.alpha = 0f;
        badge.group.blocksRaycasts = false;
        badge.group.interactable = false;

        float s = badgeSize;
        badge.glow = MakeImage("Glow", badge.root, DiamondGlow, info.color);
        Place(badge.glow.rectTransform, Vector2.zero, s * 1.9f);
        badge.gem = MakeImage("Gem", badge.root, DiamondGem, Ink);
        Place(badge.gem.rectTransform, Vector2.zero, s * 0.86f);
        badge.frame = MakeImage("Frame", badge.root, DiamondFrame, Cream);
        Place(badge.frame.rectTransform, Vector2.zero, s);
        badge.icon = MakeImage("Glyph", badge.root, Glyph(info.glyph), Color.white);
        Place(badge.icon.rectTransform, Vector2.zero, s * 0.44f);

        // จำนวนเทิร์น: เพชรเล็กสีเข้มมุมขวาล่าง
        badge.counterBack = MakeImage("Turns", badge.root, DiamondGem, new Color(Ink.r, Ink.g, Ink.b, 0.95f));
        Place(badge.counterBack.rectTransform, new Vector2(s * 0.36f, -s * 0.3f), s * 0.52f);
        Image counterFrame = MakeImage("Frame", badge.counterBack.rectTransform, DiamondFrame, new Color(Cream.r, Cream.g, Cream.b, 0.8f));
        Stretch(counterFrame.rectTransform, -0.08f);
        badge.counter = MakeText("Count", badge.counterBack.rectTransform, s * 0.34f, FontStyles.Bold);
        Stretch(badge.counter.rectTransform);
        badge.appear = 0f;
        return badge;
    }

    private static void Place(RectTransform rt, Vector2 position, float size)
    {
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(size, size);
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.one * inset;
        rt.anchorMax = Vector2.one * (1f - inset);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private Image MakeImage(string name, Transform parent, Sprite sprite, Color color, bool sliced = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sliced) { image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 2f; }
        return image;
    }

    private TextMeshProUGUI MakeText(string name, Transform parent, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.characterSpacing = 2f;
        return text;
    }

    // ---------------------------------------------------------------- กล่องคำอธิบาย (ชี้เมาส์)
    private static RectTransform tooltip;
    private static TextMeshProUGUI tooltipText;
    private static CanvasGroup tooltipGroup;
    private static Image tooltipOutline;
    private static StatusBadgeRow tooltipOwner;
    private static int tooltipFrame = -1;
    private static StatusTooltipView tooltipView; // Resources/StatusTooltip.prefab (แบบพู่กัน) ไม่มี = กล่องที่สร้างในโค้ด
    private static string tooltipShown;
    public const string TooltipResource = "StatusTooltip";

    private void UpdateTooltip()
    {
        Badge hovered = null;
        Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1000f, -1000f);
        if (alpha > 0.5f)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            foreach (Badge badge in badges)
            {
                if (badge.leaving || badge.appear < 0.9f) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(badge.root, mouse, cam)) { hovered = badge; break; }
            }
        }

        // แถบหลายแถบใช้กล่องเดียวกัน: แถบที่มีป้ายใต้เมาส์เป็นเจ้าของในเฟรมนั้น
        if (hovered == null)
        {
            if (tooltipOwner == this && tooltipFrame != Time.frameCount) tooltipOwner = null;
            if (tooltipOwner == null && tooltipGroup != null && tooltipFrame != Time.frameCount)
                tooltipGroup.alpha = Mathf.MoveTowards(tooltipGroup.alpha, 0f, Time.unscaledDeltaTime * 8f);
            return;
        }

        EnsureTooltip();
        if (tooltip == null) return;
        tooltipOwner = this;
        tooltipFrame = Time.frameCount;
        Info info = hovered.info;
        string shown = info.key + "|" + info.counter + "|" + info.detail;
        float gap = 10f, margin = 8f;
        if (tooltipView != null)
        {
            // ภาพพู่กันล้นกรอบออกไป เว้นระยะเพิ่ม
            gap = 30f;
            margin = 40f;
            if (tooltipShown != shown) tooltipView.Show(info);
        }
        else
        {
            string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(info.color, Color.white, 0.35f));
            string kind = info.isBuff ? "<color=#F2CD66>BUFF</color>" : "<color=#E8606A>DEBUFF</color>";
            tooltipText.text = $"<size=120%><b><color=#{hex}>{info.title}</color></b></size>  <size=80%>{kind}</size>\n{info.detail}\n<size=85%><color=#B8B4C8>{info.duration}</color></size>";
            tooltipOutline.color = new Color(Cream.r, Cream.g, Cream.b, 0.75f);
            Vector2 textSize = tooltipText.GetPreferredValues(tooltipText.text, 360f, 0f);
            textSize.x = Mathf.Min(360f, textSize.x);
            tooltip.sizeDelta = textSize + new Vector2(28f, 20f);
        }
        tooltipShown = shown;
        tooltipGroup.alpha = Mathf.MoveTowards(tooltipGroup.alpha, 1f, Time.unscaledDeltaTime * 10f);

        // วางเหนือป้าย ไม่ให้ล้นจอ (ใต้ป้ายถ้าข้างบนไม่พอ)
        Vector2 size = tooltip.rect.size;
        Canvas root = tooltip.GetComponentInParent<Canvas>().rootCanvas;
        float scale = root.scaleFactor;
        Vector3[] corners = new Vector3[4];
        hovered.root.GetWorldCorners(corners);
        Vector2 pos = new Vector2((corners[0].x + corners[2].x) * 0.5f, corners[1].y + gap * scale);
        float halfW = size.x * 0.5f * scale;
        pos.x = Mathf.Clamp(pos.x, halfW + margin * scale, Screen.width - halfW - margin * scale);
        float height = size.y * scale;
        if (pos.y + height > Screen.height - margin * scale) pos.y = corners[0].y - gap * scale - height;
        tooltip.position = new Vector3(pos.x, pos.y, 0f);
    }

    private void EnsureTooltip()
    {
        if (tooltip != null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Canvas root = canvas.rootCanvas;
        RectTransform layer = root.renderMode == RenderMode.ScreenSpaceOverlay ? BattleUILayers.Get(root, "StatusTooltip_Layer", 5) : (RectTransform)root.transform;

        // แบบพู่กันจาก Prefab (แต่งได้ที่ Assets/Resources/StatusTooltip.prefab)
        GameObject prefab = Resources.Load<GameObject>(TooltipResource);
        if (prefab != null && prefab.GetComponent<StatusTooltipView>() != null)
        {
            GameObject made = Instantiate(prefab, layer, false);
            made.name = "StatusTooltip";
            tooltip = (RectTransform)made.transform;
            tooltip.anchorMin = tooltip.anchorMax = Vector2.zero;
            tooltipView = made.GetComponent<StatusTooltipView>();
            tooltipGroup = made.GetComponent<CanvasGroup>();
            if (tooltipGroup == null) tooltipGroup = made.AddComponent<CanvasGroup>();
            tooltipGroup.alpha = 0f;
            tooltipGroup.blocksRaycasts = false;
            foreach (Graphic graphic in made.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            tooltipShown = null;
            return;
        }

        tooltipView = null;
        var go = new GameObject("StatusTooltip", typeof(RectTransform));
        go.layer = layer.gameObject.layer;
        tooltip = (RectTransform)go.transform;
        tooltip.SetParent(layer, false);
        tooltip.anchorMin = tooltip.anchorMax = Vector2.zero;
        tooltip.pivot = new Vector2(0.5f, 0f);
        tooltipGroup = go.AddComponent<CanvasGroup>();
        tooltipGroup.alpha = 0f;
        tooltipGroup.blocksRaycasts = false;
        Image plate = MakeImage("Plate", tooltip, PlateSprite, new Color(Ink.r, Ink.g, Ink.b, 0.94f), true);
        Stretch(plate.rectTransform);
        tooltipOutline = MakeImage("Outline", tooltip, OutlineSprite, Color.white, true);
        Stretch(tooltipOutline.rectTransform);
        tooltipText = MakeText("Text", tooltip, 18f, FontStyles.Normal);
        tooltipText.alignment = TextAlignmentOptions.TopLeft;
        tooltipText.textWrappingMode = TextWrappingModes.Normal;
        tooltipText.characterSpacing = 0f;
        tooltipText.lineSpacing = 6f;
        tooltipText.color = Cream;
        RectTransform textRect = tooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 10f);
        textRect.offsetMax = new Vector2(-14f, -10f);
    }

    // ---------------------------------------------------------------- คำอธิบายของสถานะ
    public static Info Describe(CombatStatus status)
    {
        var info = new Info { key = status.type.ToString() };
        float v = status.value;
        int turns = status.turnsLeft;
        info.counter = turns > 0 ? turns.ToString() : "";
        info.duration = turns > 0 ? (turns == 1 ? "1 turn left" : $"{turns} turns left") : "Until removed";
        switch (status.type)
        {
            case CombatStatusType.Evasion:
                info.title = "EVASION"; info.isBuff = true; info.glyph = StatusGlyph.Dash;
                info.color = new Color(0.45f, 0.85f, 1f);
                int charges = Mathf.Max(1, Mathf.RoundToInt(v));
                info.detail = charges > 1 ? $"Auto-parries the next {charges} enemy hits." : "Auto-parries the next enemy hit.";
                info.counter = charges > 1 ? "x" + charges : info.counter;
                if (turns < 0) info.duration = "Until used";
                break;
            case CombatStatusType.Berserk:
                info.title = "BERSERK"; info.isBuff = true; info.glyph = StatusGlyph.DoubleUp;
                info.color = new Color(1f, 0.42f, 0.28f);
                info.detail = $"Deals +{v * 100f:0}% damage.";
                break;
            case CombatStatusType.AttackUp:
                info.title = "ATK UP"; info.isBuff = true; info.glyph = StatusGlyph.Up;
                info.color = new Color(1f, 0.78f, 0.3f);
                info.detail = $"Deals +{v * 100f:0}% damage.";
                break;
            case CombatStatusType.ExposeWeakness:
                info.title = "EXPOSED"; info.isBuff = false; info.glyph = StatusGlyph.Crosshair;
                info.color = new Color(0.78f, 0.45f, 1f);
                info.detail = $"Takes +{v * 100f:0}% damage from Gun and Long Sword.";
                break;
            case CombatStatusType.Shatter:
                info.title = "SHATTER"; info.isBuff = false; info.glyph = StatusGlyph.Crack;
                info.color = new Color(0.55f, 0.82f, 1f);
                info.detail = $"Takes +{v * 100f:0}% posture damage.";
                if (turns < 0) info.duration = "Until Staggered";
                break;
            case CombatStatusType.Slow:
                info.title = "SLOW"; info.isBuff = false; info.glyph = StatusGlyph.Hourglass;
                info.color = new Color(0.4f, 0.65f, 1f);
                info.detail = $"Speed -{v * 100f:0}%: takes turns less often.";
                break;
            case CombatStatusType.Burn:
                info.title = "BURN"; info.isBuff = false; info.glyph = StatusGlyph.Flame;
                info.color = new Color(1f, 0.5f, 0.18f);
                info.detail = $"Loses {v:0} HP at the start of each of its turns.";
                break;
            default:
                info.title = status.type.ToString().ToUpperInvariant(); info.glyph = StatusGlyph.Down;
                info.color = Color.gray; info.detail = "";
                break;
        }
        return info;
    }

    public static void Describe(CombatStatusSet set, List<Info> into)
    {
        if (set == null) return;
        foreach (CombatStatus status in set.All) into.Add(Describe(status));
    }

    // ---------------------------------------------------------------- ภาพที่วาดในโค้ด
    private static Sprite plateSprite, outlineSprite, circleSprite;
    private static readonly Dictionary<StatusGlyph, Sprite> glyphs = new Dictionary<StatusGlyph, Sprite>();

    private static Sprite diamondFrame, diamondGem, diamondGlow;
    private static Sprite DiamondFrame => diamondFrame != null ? diamondFrame : diamondFrame = ToSprite(SigilSlotsUI.GenerateTexture("Frame"));
    private static Sprite DiamondGem => diamondGem != null ? diamondGem : diamondGem = ToSprite(SigilSlotsUI.GenerateTexture("Gem"));
    private static Sprite DiamondGlow => diamondGlow != null ? diamondGlow : diamondGlow = ToSprite(SigilSlotsUI.GenerateTexture("Glow"));

    private static Sprite ToSprite(Texture2D texture)
    {
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static Sprite PlateSprite => plateSprite != null ? plateSprite : plateSprite = RoundRect(false);
    private static Sprite OutlineSprite => outlineSprite != null ? outlineSprite : outlineSprite = RoundRect(true);
    private static Sprite CircleSprite => circleSprite != null ? circleSprite : circleSprite = MakeSprite(64, (x, y) =>
    {
        float d = new Vector2(x, y).magnitude;
        return Edge(1f - d, 0.04f);
    });

    // สี่เหลี่ยมมุมมน 64px มุมรัศมี 20 (ตัดแบบ 9 ส่วน) outline = เส้นขอบหนา 3px
    private static Sprite RoundRect(bool outline)
    {
        const int size = 64;
        const float radius = 20f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float qx = Mathf.Max(Mathf.Abs(px - size * 0.5f) - (size * 0.5f - radius), 0f);
                float qy = Mathf.Max(Mathf.Abs(py - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dist = Mathf.Sqrt(qx * qx + qy * qy) - radius; // < 0 ข้างใน
                float a = Mathf.Clamp01(0.5f - dist);
                if (outline) a *= Mathf.Clamp01(dist + 3.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        texture.SetPixels(pixels);
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(22, 22, 22, 22));
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static Sprite Glyph(StatusGlyph glyph)
    {
        if (glyphs.TryGetValue(glyph, out Sprite cached) && cached != null) return cached;
        Func<float, float, float> shape;
        switch (glyph)
        {
            case StatusGlyph.Up: shape = (x, y) => Chevron(x, y + 0.1f, true); break;
            case StatusGlyph.Down: shape = (x, y) => Chevron(x, y - 0.1f, false); break;
            case StatusGlyph.DoubleUp: shape = (x, y) => Mathf.Max(Chevron(x, y + 0.38f, true), Chevron(x, y - 0.18f, true)); break;
            case StatusGlyph.Dash:
                // ลูกศรพุ่งไปทางขวา + เส้นความเร็ว
                shape = (x, y) => Mathf.Max(Chevron(-y, x - 0.25f, true),
                    Mathf.Max(Segment(x, y, -0.85f, 0.32f, -0.15f, 0.32f, 0.09f), Segment(x, y, -0.85f, -0.32f, -0.15f, -0.32f, 0.09f)));
                break;
            case StatusGlyph.Crosshair:
                shape = (x, y) =>
                {
                    float d = new Vector2(x, y).magnitude;
                    float ring = Edge(0.11f - Mathf.Abs(d - 0.55f), 0.04f);
                    float dot = Edge(0.16f - d, 0.04f);
                    float ticks = Mathf.Max(Mathf.Max(Segment(x, y, 0f, 0.7f, 0f, 0.95f, 0.09f), Segment(x, y, 0f, -0.7f, 0f, -0.95f, 0.09f)),
                                            Mathf.Max(Segment(x, y, 0.7f, 0f, 0.95f, 0f, 0.09f), Segment(x, y, -0.7f, 0f, -0.95f, 0f, 0.09f)));
                    return Mathf.Max(ring, Mathf.Max(dot, ticks));
                };
                break;
            case StatusGlyph.Crack:
                // เพชรที่มีรอยแตกพาดกลาง
                shape = (x, y) =>
                {
                    float diamond = Edge(0.8f - (Mathf.Abs(x) + Mathf.Abs(y)), 0.05f);
                    float crack = Mathf.Max(Segment(x, y, -0.15f, 0.85f, 0.12f, 0.15f, 0.08f),
                                  Mathf.Max(Segment(x, y, 0.12f, 0.15f, -0.1f, -0.2f, 0.08f), Segment(x, y, -0.1f, -0.2f, 0.1f, -0.85f, 0.08f)));
                    return Mathf.Clamp01(diamond - crack * 1.2f);
                };
                break;
            case StatusGlyph.Hourglass:
                shape = (x, y) =>
                {
                    float body = Mathf.Abs(y) < 0.7f && Mathf.Abs(x) < Mathf.Abs(y) * 0.85f + 0.07f ? 1f : 0f;
                    float caps = Mathf.Max(Segment(x, y, -0.6f, 0.72f, 0.6f, 0.72f, 0.09f), Segment(x, y, -0.6f, -0.72f, 0.6f, -0.72f, 0.09f));
                    return Mathf.Max(body, caps);
                };
                break;
            case StatusGlyph.Flame:
                // หยดเปลวไฟ: วงกลมด้านล่าง + ปลายแหลมด้านบน
                shape = (x, y) =>
                {
                    float r = 0.42f;
                    float circle = Edge(r - new Vector2(x, y + 0.3f).magnitude, 0.04f);
                    float top = y > -0.3f && y < 0.85f ? Edge((1f - (y + 0.3f) / 1.15f) * r - Mathf.Abs(x + Mathf.Sin(y * 2.2f) * 0.08f), 0.04f) : 0f;
                    return Mathf.Max(circle, top);
                };
                break;
            default: // Cross
                shape = (x, y) => Mathf.Max(Segment(x, y, -0.55f, -0.55f, 0.55f, 0.55f, 0.13f), Segment(x, y, -0.55f, 0.55f, 0.55f, -0.55f, 0.13f));
                break;
        }
        Sprite sprite = MakeSprite(64, shape);
        glyphs[glyph] = sprite;
        return sprite;
    }

    // บั้ง ^ (up) หรือ v ศูนย์กลางที่จุดกำเนิด
    private static float Chevron(float x, float y, bool up)
    {
        float s = up ? 1f : -1f;
        return Mathf.Max(Segment(x, y * s, -0.55f, -0.25f, 0f, 0.3f, 0.12f), Segment(x, y * s, 0.55f, -0.25f, 0f, 0.3f, 0.12f));
    }

    private static float Segment(float x, float y, float ax, float ay, float bx, float by, float width)
    {
        Vector2 p = new Vector2(x, y), a = new Vector2(ax, ay), b = new Vector2(bx, by);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
        float d = (p - (a + ab * t)).magnitude;
        return Edge(width - d, 0.035f);
    }

    // ขอบนุ่ม: v > 0 = ข้างใน
    private static float Edge(float v, float soft) => Mathf.Clamp01(v / soft + 0.5f);

    private static Sprite MakeSprite(int size, Func<float, float, float> alpha)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v)));
            }
        texture.SetPixels(pixels);
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
