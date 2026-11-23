using UnityEngine;

// ลูกศรบอกทิศของ HintVFX: บั้ง ">" ไหลตามแกน +X ของราก (ParryManager หมุนรากให้ +X = ทิศที่ต้องลากบนจอ)
// ค่าเป็นหน่วยของรากของ VFX_Hint (รากถูกย่อให้เส้นประกายยาวราว vfxHintScreenSize ของจอ สเกลราว 0.5-0.6)
// ประกายของอาร์ตเป็นเส้นตรงไม่มีหัวท้าย บั้งที่ไหลไปทางเดียวจึงบอกว่าต้องลากไปทางไหน
// วาดภาพเองในโค้ด (SpriteRenderer + Sprites/Default เพราะวัสดุ Sprite ของ URP ไม่ผสมโปร่งใสในฉาก 3D นี้) เดินด้วยเวลาจริง เล่นต่อแม้ Hitstop / Slow-mo
public class ParryHintArrows : MonoBehaviour
{
    [Header("บั้งที่ไหลตามทิศ")]
    [Min(1)] public int count = 3;
    [Tooltip("ระยะที่บั้งไหล (หน่วยของรากของ Hint)")]
    public float length = 6f;
    [Tooltip("ขนาดบั้ง (หน่วยของรากของ Hint)")]
    public float size = 2f;
    [Tooltip("วินาทีที่บั้งหนึ่งไหลจากต้นถึงปลาย")]
    public float cycle = 0.55f;
    public Color arrowColor = new Color(1f, 0.85f, 0.45f, 1f);
    public Color glowColor = new Color(1f, 0.45f, 0.1f, 0.55f);

    [Header("หัวลูกศรที่ปลายทาง")]
    public bool showHead = true;
    public float headSize = 2.6f;
    public float headPulse = 0.15f;

    [Header("รางจาง ๆ ตามแนว")]
    public bool showTrack = true;
    public float trackAlpha = 0.25f;
    public float trackThickness = 0.3f;

    [Header("การวาด")]
    public string sortingLayer = "HintParticle";
    public int sortingOrder = 400;

    private SpriteRenderer[] arrows, glows;
    private SpriteRenderer head, headGlow, track;
    private float fade = 0f, fadeTarget = 1f, fadeSpeed = 6f;
    private float startTime;

    private static Sprite chevron, glow, bar;
    private static Material material;

    private void Awake()
    {
        startTime = Time.unscaledTime;
        BuildAssets();
        if (showTrack) track = MakeRenderer("Track", bar, 0);
        arrows = new SpriteRenderer[count];
        glows = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            glows[i] = MakeRenderer("ArrowGlow" + i, glow, 1);
            arrows[i] = MakeRenderer("Arrow" + i, chevron, 2);
        }
        if (showHead)
        {
            headGlow = MakeRenderer("HeadGlow", glow, 3);
            head = MakeRenderer("Head", chevron, 4);
        }
        Update();
    }

    // ค่อย ๆ จางหายแทนหายวับ (ParryManager.ClearHint)
    public void FadeOut(float duration)
    {
        fadeTarget = 0f;
        fadeSpeed = 1f / Mathf.Max(0.01f, duration);
    }

    private SpriteRenderer MakeRenderer(string name, Sprite sprite, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sharedMaterial = material;
        if (SortingLayer.NameToID(sortingLayer) != 0 || sortingLayer == "Default") r.sortingLayerName = sortingLayer;
        r.sortingOrder += sortingOrder + order;
        return r;
    }

    private void Update()
    {
        float now = Time.unscaledTime - startTime;
        fade = Mathf.MoveTowards(fade, fadeTarget, Time.unscaledDeltaTime * (fadeTarget > fade ? 8f : fadeSpeed));
        float half = length * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float phase = Mathf.Repeat(now / Mathf.Max(0.05f, cycle) + i / (float)count, 1f);
            float x = Mathf.Lerp(-half, half, phase);
            // โผล่ที่ต้น สว่างกลางทาง จางที่ปลาย และโตขึ้นเล็กน้อยตามทาง เหมือนพุ่งไปข้างหน้า
            float alpha = Mathf.Sin(phase * Mathf.PI) * fade;
            float scale = size * Mathf.Lerp(0.75f, 1.15f, phase);
            Place(arrows[i], new Vector3(x, 0f, 0f), scale, arrowColor, alpha);
            Place(glows[i], new Vector3(x, 0f, 0.01f), scale * 1.9f, glowColor, alpha);
        }

        if (head != null)
        {
            float beat = 1f + Mathf.Sin(now * 12f) * headPulse;
            Place(head, new Vector3(half + size * 0.35f, 0f, 0f), headSize * beat, arrowColor, fade);
            Place(headGlow, new Vector3(half + size * 0.35f, 0f, 0.01f), headSize * 2f * beat, glowColor, fade);
        }

        if (track != null)
        {
            track.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            track.transform.localScale = new Vector3(length, trackThickness, 1f);
            track.color = new Color(glowColor.r, glowColor.g, glowColor.b, trackAlpha * fade);
        }
    }

    private static void Place(SpriteRenderer r, Vector3 position, float scale, Color color, float alpha)
    {
        if (r == null) return;
        r.transform.localPosition = position;
        r.transform.localScale = Vector3.one * scale;
        color.a *= Mathf.Clamp01(alpha);
        r.color = color;
    }

    // ---------------- ภาพที่วาดในโค้ด ----------------
    private static void BuildAssets()
    {
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) material = new Material(shader) { hideFlags = HideFlags.DontSave };
        }
        if (chevron == null) chevron = MakeSprite(64, (u, v) => Edge(ChevronDistance(u, v), 0.075f, 0.03f), 64f);
        if (glow == null) glow = MakeSprite(64, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            return a * a;
        }, 64f);
        if (bar == null) bar = MakeSprite(64, (u, v) =>
        {
            float across = 1f - Mathf.Abs(v - 0.5f) * 2f;
            float along = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 5f);
            return Mathf.Clamp01(across) * along;
        }, 64f);
    }

    // ระยะถึงเส้น ">" สองเส้น (พิกัด 0..1 ปลายแหลมชี้ +X)
    private static float ChevronDistance(float u, float v)
    {
        Vector2 p = new Vector2(u, v);
        Vector2 tip = new Vector2(0.72f, 0.5f);
        return Mathf.Min(SegmentDistance(p, new Vector2(0.3f, 0.16f), tip), SegmentDistance(p, new Vector2(0.3f, 0.84f), tip));
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    // ขอบนุ่ม (Mathf.SmoothStep เป็น lerp ไม่ใช่ smoothstep จึงเขียนเอง)
    private static float Edge(float distance, float radius, float softness)
    {
        float t = Mathf.Clamp01((radius + softness - distance) / Mathf.Max(0.0001f, softness * 2f));
        return t * t * (3f - 2f * t);
    }

    private static Sprite MakeSprite(int resolution, System.Func<float, float, float> alpha, float pixelsPerUnit)
    {
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha((x + 0.5f) / resolution, (y + 0.5f) / resolution));
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
