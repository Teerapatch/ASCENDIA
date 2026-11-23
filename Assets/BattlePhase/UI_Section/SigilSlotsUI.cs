using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ช่องธาตุของ Wand (Elemental Sigil) บนแผง Passive: เพชร 5 ช่อง (บน ขวา ล่าง ซ้าย + กลาง) ดัดแปลงจากภาพของอาร์ตที่เป็นเพชร 4 ช่องเรียงกากบาท
// ช่องว่าง = กรอบเพชรจาง ๆ, มีธาตุ = อัญมณีสีของธาตุพร้อมแสงเรือง เติมตามลำดับ บน -> ขวา -> ล่าง -> ซ้าย -> กลาง (ช่องที่ 5 เต็มพอดี)
// BattleArtUI สร้างจาก Prefab (Weapon/Spell/SigilSlots.prefab, ช่อง BattleArtUI.sigilSlotsPrefab) ตรงตำแหน่งตัวเลขของ Passive
// ใช้ลูก Slot1..Slot5 (Frame / Gem / Glow) ที่มีอยู่ใน Prefab แต่งเพิ่มได้ ขาดชิ้นไหนสร้างให้ ภาพว่างใช้ภาพที่วาดในโค้ด
public class SigilSlotsUI : MonoBehaviour
{
    [Header("ภาพ (Assets/BattlePhase/UI_Section/Sigil) ว่าง = วาดในโค้ด")]
    public Sprite frameSprite;
    public Sprite gemSprite;
    public Sprite glowSprite;

    [Header("การจัดวาง")]
    [Tooltip("จัดตำแหน่ง/ขนาดช่องเองทุกเฟรมจาก slotSize / spacing ปิดไว้ถ้าจะจัดเองใน Prefab")]
    public bool autoLayout = true;
    public float slotSize = 46f;
    [Tooltip("ระยะจากกลางถึงช่องรอบนอก (เท่าของ slotSize) 0.7 = มุมเพชรชนกันแบบในภาพ")]
    public float spacing = 0.72f;
    public Color frameColor = new Color(0.78f, 0.82f, 0.95f, 0.9f);
    public Color emptyColor = new Color(0.12f, 0.14f, 0.22f, 0.85f);
    public float popScale = 1.45f;
    public float popTime = 0.25f;

    // ลำดับการเติม (ตำแหน่งเป็นทิศ x/y ก่อนคูณระยะ)
    private static readonly Vector2[] Layout = { new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0), Vector2.zero };

    private class Slot
    {
        public RectTransform root;
        public Image frame, fill, glow;
        public float popAt = -10f;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private int shownCount = -1;
    private readonly List<ElementType> shown = new List<ElementType>();

    private static Sprite generatedFrame, generatedGem, generatedGlow;

    public static SigilSlotsUI Create(RectTransform parent, Vector2 anchoredPosition)
    {
        var go = new GameObject("SigilSlots", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = Vector2.one * 140f;
        return go.AddComponent<SigilSlotsUI>();
    }

    private void Awake()
    {
        BuildSprites();
        Sprite frameImage = frameSprite != null ? frameSprite : generatedFrame;
        Sprite gemImage = gemSprite != null ? gemSprite : generatedGem;
        Sprite glowImage = glowSprite != null ? glowSprite : generatedGlow;
        for (int i = 0; i < Layout.Length; i++)
        {
            var slot = new Slot();
            Transform existing = transform.Find("Slot" + (i + 1));
            if (existing == null)
            {
                existing = new GameObject("Slot" + (i + 1), typeof(RectTransform)).transform;
                existing.SetParent(transform, false);
                ((RectTransform)existing).anchoredPosition = Layout[i] * slotSize * spacing;
                ((RectTransform)existing).sizeDelta = Vector2.one * slotSize;
            }
            slot.root = (RectTransform)existing;
            slot.glow = FindOrMakeImage("Glow", slot.root, glowImage, 1.9f);
            slot.fill = FindOrMakeImage("Gem", slot.root, gemImage, 0.78f);
            slot.frame = FindOrMakeImage("Frame", slot.root, frameImage, 1f);
            slots.Add(slot);
        }
        Layout1();
        Show(new List<ElementType>(), true);
    }

    private static Image FindOrMakeImage(string name, RectTransform parent, Sprite sprite, float scale)
    {
        Transform found = parent.Find(name);
        Image image = found != null ? found.GetComponent<Image>() : null;
        if (image == null) return MakeImage(name, parent, sprite, scale);
        if (image.sprite == null) image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private void Layout1()
    {
        if (!autoLayout) return;
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].root.anchoredPosition = Layout[i] * slotSize * spacing;
            slots[i].root.sizeDelta = Vector2.one * slotSize;
        }
    }

    private static Image MakeImage(string name, RectTransform parent, Sprite sprite, float scale)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one * scale;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    // ธาตุที่มีอยู่ (เรียงตามที่ได้มา)
    public void Show(List<ElementType> elements, bool instant = false)
    {
        bool changed = elements.Count != shownCount;
        for (int i = 0; !changed && i < elements.Count; i++) changed = elements[i] != shown[i];
        if (!changed) return;

        for (int i = 0; i < slots.Count; i++)
        {
            bool had = i < shownCount;
            bool has = i < elements.Count;
            if (has && (!had || shown[i] != elements[i]) && !instant) slots[i].popAt = Time.unscaledTime;
        }
        shown.Clear();
        shown.AddRange(elements);
        shownCount = elements.Count;
    }

    private void Update()
    {
        Layout1();
        float t = Time.unscaledTime;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            bool has = i < shownCount;
            Color element = has ? ElementTypes.GetColor(shown[i]) : Color.clear;

            float pop = Mathf.Clamp01((t - slot.popAt) / Mathf.Max(0.01f, popTime));
            float popScaleNow = pop < 1f ? Mathf.Lerp(popScale, 1f, 1f - (1f - pop) * (1f - pop)) : 1f;
            slot.root.localScale = Vector3.one * popScaleNow;

            slot.frame.color = has ? Color.Lerp(frameColor, Color.white, 0.4f) : frameColor * new Color(1f, 1f, 1f, 0.55f);
            slot.fill.color = has ? new Color(element.r, element.g, element.b, 1f) : emptyColor;
            // แสงเรืองเต้นเบา ๆ เมื่อมีธาตุ (ทุกช่องเต้นพร้อมกันตอนเต็ม 5)
            float pulse = 0.55f + 0.25f * Mathf.Sin(t * (shownCount >= slots.Count ? 6f : 3f) + i * 0.7f);
            slot.glow.color = has ? new Color(element.r, element.g, element.b, pulse * (pop < 1f ? 1.4f : 1f)) : Color.clear;
        }
    }

    // ---------------- ภาพที่วาดในโค้ด (เพชร = |x|+|y| <= 1)
    private static void BuildSprites()
    {
        if (generatedFrame == null) generatedFrame = ToSprite(GenerateTexture("Frame"));
        if (generatedGem == null) generatedGem = ToSprite(GenerateTexture("Gem"));
        if (generatedGlow == null) generatedGlow = ToSprite(GenerateTexture("Glow"));
    }

    // ภาพของช่อง (Frame / Gem / Glow) ใช้ทั้งตอนเล่นและตอนบันทึกเป็น PNG ใน Editor
    public static Texture2D GenerateTexture(string kind, int size = 128)
    {
        switch (kind)
        {
            case "Frame":
                return MakeTexture(size, d =>
                {
                    // ขอบนอกหนา + ขอบในบาง (กรอบเพชรสองชั้นแบบในภาพ)
                    float outer = Edge(Mathf.Abs(d - 0.93f), 0.07f, 0.02f);
                    float inner = Edge(Mathf.Abs(d - 0.72f), 0.025f, 0.015f) * 0.8f;
                    return Mathf.Max(outer, inner);
                });
            case "Gem":
                return MakeTexture(size, d => Edge(d, 0.86f, 0.03f), true);
            default:
                return MakeTexture(size, d => Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
        }
    }

    private static Sprite ToSprite(Texture2D texture)
    {
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static float Edge(float distance, float radius, float softness)
    {
        float t = Mathf.Clamp01((radius + softness - distance) / Mathf.Max(0.0001f, softness * 2f));
        return t * t * (3f - 2f * t);
    }

    // shade = อัญมณีมีแสงด้านบนซ้ายสว่างกว่า (ไล่ระดับในตัวภาพ สีธาตุคูณทับอีกที)
    private static Texture2D MakeTexture(int size, System.Func<float, float> alphaOfDiamond, bool shade = false)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Abs(u) + Mathf.Abs(v);
                float a = alphaOfDiamond(d);
                float light = shade ? Mathf.Lerp(0.6f, 1.25f, Mathf.Clamp01((v - u) * 0.5f + 0.5f)) * (d < 0.35f ? 1.15f : 1f) : 1f;
                pixels[y * size + x] = new Color(Mathf.Min(1f, light), Mathf.Min(1f, light), Mathf.Min(1f, light), a);
            }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
