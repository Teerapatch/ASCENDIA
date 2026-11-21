using UnityEngine;

// ศัตรูติด Stagger (Posture แตก): ป้าย BREAK! + ดาวหมุนรอบหัว จนกว่าจะหาย (ข้ามเทิร์นแล้วฟื้น) หรือตาย
// ไม่แตะท่า/Animator ของศัตรู (ท่ามึนแบบ Procedural ถูกเอาออกแล้ว) EnemyController เพิ่มให้ทุกตัวเอง
[DisallowMultipleComponent]
public class StaggerFX : MonoBehaviour
{
    [Header("ดาวรอบหัว")]
    public int starCount = 5;
    public Color starColor = new Color(1f, 0.86f, 0.35f, 1f);
    [Tooltip("ขนาดดาวเทียบกับความสูงตัว")]
    public float starSizeRatio = 0.16f;
    [Tooltip("รัศมีวงดาวเทียบกับความสูงตัว")]
    public float orbitRatio = 0.3f;
    [Tooltip("รอบต่อวินาที")]
    public float orbitSpeed = 0.55f;
    [Tooltip("ดาวลอยเหนือหัวกี่ส่วนของความสูงตัว")]
    public float headOffsetRatio = 0.1f;

    private EnemyController enemy;
    private EnemyAnimationController anim;
    private Transform starRoot;
    private Transform[] stars;
    private SpriteRenderer[] starRenderers;
    private float strength; // 0..1 ค่อย ๆ เข้า/ออก
    private bool wasStaggered;
    private float phase;

    private static Sprite starSprite;
    private static Material starMaterial;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        anim = GetComponent<EnemyAnimationController>();
    }

    private float Height => anim != null && anim.Height > 0.01f ? anim.Height : 3f;
    private float Ground => anim != null ? anim.GroundBelowSpawn : 0f;

    private void LateUpdate()
    {
        if (enemy == null) return;
        bool staggered = enemy.isStaggered && !enemy.IsDead;

        if (staggered && !wasStaggered) Enter();
        wasStaggered = staggered;

        strength = Mathf.MoveTowards(strength, staggered ? 1f : 0f, Time.deltaTime * (staggered ? 3f : 2.5f));
        if (strength <= 0f)
        {
            if (starRoot != null && starRoot.gameObject.activeSelf) starRoot.gameObject.SetActive(false);
            return;
        }

        // เฟรมที่ติด Stagger มักมี Hitstop (deltaTime = 0) ดาวจึงอาจถูกปิดไปก่อน เปิดคืนทุกครั้งที่ยังแสดงอยู่
        if (starRoot != null && !starRoot.gameObject.activeSelf) starRoot.gameObject.SetActive(true);
        phase += Time.deltaTime;
        UpdateStars();
    }

    private void Enter()
    {
        BuildStars();
        starRoot.gameObject.SetActive(true);
        phase = 0f;
        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.CreateTextPopup(transform.position + Vector3.up * (Height - Ground), "BREAK!", starColor);
    }

    private void OnDisable()
    {
        wasStaggered = false;
    }

    private void BuildStars()
    {
        if (starRoot != null) return;
        if (starSprite == null) starSprite = MakeStarSprite();
        // วัสดุ Sprite เริ่มต้นของ URP ในฉาก 3D ไม่ผสมความโปร่งใส (ดาวกลายเป็นสี่เหลี่ยม) ใช้ Sprites/Default แบบเดียวกับ SoftGlow
        if (starMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) starMaterial = new Material(shader) { hideFlags = HideFlags.DontUnloadUnusedAsset };
        }
        starRoot = new GameObject("StaggerStars").transform;
        starRoot.SetParent(transform, false);
        stars = new Transform[starCount];
        starRenderers = new SpriteRenderer[starCount];
        for (int i = 0; i < starCount; i++)
        {
            GameObject star = new GameObject("Star" + i, typeof(SpriteRenderer));
            star.transform.SetParent(starRoot, false);
            SpriteRenderer sr = star.GetComponent<SpriteRenderer>();
            sr.sprite = starSprite;
            if (starMaterial != null) sr.sharedMaterial = starMaterial;
            sr.color = starColor;
            sr.sortingOrder = 50;
            stars[i] = star.transform;
            starRenderers[i] = sr;
        }
    }

    private void UpdateStars()
    {
        if (starRoot == null) return;
        Camera cam = Camera.main;
        float height = Height;
        Vector3 head = transform.position + Vector3.up * (height - Ground + height * headOffsetRatio);
        float radius = height * orbitRatio;
        float size = height * starSizeRatio * Mathf.Lerp(0.2f, 1f, strength);
        for (int i = 0; i < stars.Length; i++)
        {
            float a = (phase * orbitSpeed + (float)i / stars.Length) * Mathf.PI * 2f;
            // วงรีแนวนอนรอบหัว ดาวที่อยู่ด้านหลังเล็กลงและจางลงเล็กน้อย
            Vector3 offset = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 2f) * radius * 0.12f, Mathf.Sin(a) * radius * 0.55f);
            stars[i].position = head + offset;
            float depth = cam != null ? Vector3.Dot(offset, cam.transform.forward) / Mathf.Max(0.01f, radius) : 0f; // -1 หน้า .. 1 หลัง
            float scale = size * Mathf.Lerp(1.15f, 0.75f, (depth + 1f) * 0.5f) * (0.9f + 0.1f * Mathf.Sin(phase * 9f + i));
            stars[i].localScale = Vector3.one * scale / Mathf.Max(0.0001f, transform.lossyScale.x);
            if (cam != null) stars[i].rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up) * Quaternion.Euler(0f, 0f, phase * 120f + i * 40f);
            Color c = starColor;
            c.a = strength * Mathf.Lerp(1f, 0.55f, (depth + 1f) * 0.5f);
            starRenderers[i].color = c;
            starRenderers[i].sortingOrder = depth < 0f ? 60 : 40;
        }
    }

    // smoothstep แบบ GLSL (Mathf.SmoothStep ของ Unity คือการ Lerp ระหว่างสองค่า ไม่ใช่ขอบนุ่ม)
    private static float Edge(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // ดาว 5 แฉกขอบนุ่ม + แสงเรือง (วาดเอง ไม่ต้องมีไฟล์ภาพ) 1 หน่วยโลกต่อ Sprite
    private static Sprite MakeStarSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) + Mathf.PI / 2f;
                // รัศมีขอบดาว: สลับแฉกนอก/ใน 5 แฉก
                float k = Mathf.Abs(Mathf.Repeat(angle / (2f * Mathf.PI) * 5f, 1f) - 0.5f) * 2f; // 1 ที่ปลายแฉก, 0 ที่ร่อง
                float edge = Mathf.Lerp(0.36f, 0.78f, k * k);
                float star = 1f - Edge(edge - 0.06f, edge + 0.02f, r);
                float glow = Mathf.Pow(Mathf.Clamp01(1f - r), 3f) * 0.5f;
                float a = Mathf.Clamp01(star + glow);
                // กลางดาวขาวกว่า (สีจริงคูณจาก SpriteRenderer.color)
                byte w = (byte)(Mathf.Lerp(200f, 255f, 1f - r) );
                pixels[y * size + x] = new Color32(255, 255, w, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }
}
