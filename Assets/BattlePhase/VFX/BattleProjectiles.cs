using System;
using UnityEngine;
using UnityEngine.Rendering;

// กระสุนของท่ายิงของผู้เล่น (BattleClip.projectile) สร้างจากโค้ดทั้งหมด ไม่ต้องมี Prefab
//  - Bullet (ปืน): ไฟปากกระบอก + ควัน, กระสุนเรืองแสงพร้อมเส้นแสง (Tracer) วิ่งเร็ว, ประกายกระทบที่เป้า
//  - EnergyBlast (ไม้เท้า): แสงร่ายที่ปลายไม้เท้า, ลูกพลังงาน (แกน + รัศมีเต้น + หางแสง + ประกายวน) วิ่งส่ายเล็กน้อย,
//    ระเบิดพลังงาน (แสงวาบ + วงแหวนขยาย + ประกายกระจาย) ที่เป้า
// สร้างตัวเองในฉากต่อสู้ครั้งแรกที่ใช้ (วางคอมโพเนนต์นี้ในฉากเพื่อปรับค่า) เอฟเฟกต์ใช้เวลาจริง จึงเล่นต่อแม้ Hitstop
public class BattleProjectiles : MonoBehaviour
{
    private static BattleProjectiles instance;
    public static BattleProjectiles Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<BattleProjectiles>();
            if (instance == null) instance = new GameObject("BattleProjectiles").AddComponent<BattleProjectiles>();
            return instance;
        }
    }

    [Header("ปืน (Bullet)")]
    public float bulletSpeed = 70f;
    public Color bulletColor = new Color(1f, 0.86f, 0.5f, 1f);
    public float bulletSize = 1.5f;
    public float tracerWidth = 0.5f;
    public float tracerTime = 0.16f;
    public Color muzzleColor = new Color(1f, 0.62f, 0.25f, 1f);
    public float muzzleFlashSize = 4.5f;
    public float bulletImpactSize = 4f;

    [Header("ไม้เท้า (Energy Blast)")]
    public float blastSpeed = 24f;
    public Color blastCoreColor = new Color(0.9f, 0.97f, 1f, 1f);
    public Color blastColor = new Color(0.55f, 0.4f, 1f, 1f);
    public float blastSize = 2.8f;
    [Tooltip("ระยะส่ายออกข้างระหว่างทาง (หน่วยโลก)")]
    public float blastWobble = 0.9f;
    public float castFlashSize = 5f;
    public float blastImpactSize = 9f;

    [Header("มีดปา (Knife: Throwing Knife ของ Dagger)")]
    [Tooltip("ความเร็วมีด (หน่วยโลก/วินาที)")]
    public float knifeSpeed = 42f;
    [Tooltip("ความเร็วหมุนตีลังกา (องศา/วินาที)")]
    public float knifeSpin = 1440f;
    [Tooltip("ขนาดมีดเทียบกับดาบในมือ")]
    public float knifeScale = 1.3f;
    public Color knifeTrailColor = new Color(0.82f, 0.92f, 1f, 1f);
    public float knifeTrailWidth = 0.35f;
    public float knifeTrailTime = 0.14f;
    public float knifeImpactSize = 3f;

    [Header("ลูกพลังงานของศัตรู (Wand Guardian: EnemyAnimationSet.projectileEnergyBlast)")]
    public Color enemyBlastCoreColor = new Color(0.9f, 0.97f, 1f, 1f);
    public Color enemyBlastColor = new Color(0.55f, 0.4f, 1f, 1f);
    public float enemyBlastSize = 2.8f;

    [Header("ทั่วไป")]
    [Tooltip("เวลาบินสูงสุด (วินาทีจริง) กันกระสุนค้าง")]
    public float maxFlightTime = 2.5f;
    public int sortingOrder = 30;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    // ยิง: from = ปากกระบอก/ปลายไม้เท้า, target = จุดเป้า (อ่านใหม่ทุกเฟรม เป้าขยับก็ตามไป), onArrive = ตอนถึงเป้า (ลงดาเมจ)
    public void Fire(BattleProjectileKind kind, Vector3 from, Func<Vector3> target, Action onArrive)
    {
        Vector3 to = target != null ? target() : from + transform.forward;
        Vector3 direction = (to - from).sqrMagnitude > 0.0001f ? (to - from).normalized : Vector3.forward;
        SoundManager.Play(kind == BattleProjectileKind.EnergyBlast ? Sfx.SpellCast : kind == BattleProjectileKind.Knife ? Sfx.KnifeThrow : Sfx.GunShot);

        if (kind == BattleProjectileKind.EnergyBlast) BlastCast(from, direction, blastColor, blastCoreColor);
        else if (kind == BattleProjectileKind.Knife)
        {
            // ประกายวาบเล็ก ๆ ตอนมีดหลุดจากมือ
            Flash(from, knifeTrailColor, knifeImpactSize * 0.45f, 0.1f);
            Sparks(from, direction, knifeTrailColor, 5, 6f, 14f, 0.3f, 0.18f, 30f);
        }
        else
        {
            Flash(from, muzzleColor, muzzleFlashSize, 0.14f);
            Flash(from + direction * muzzleFlashSize * 0.3f, bulletColor, muzzleFlashSize * 0.6f, 0.1f);
            Flash(from + direction * muzzleFlashSize * 0.6f, muzzleColor, muzzleFlashSize * 0.4f, 0.08f);
            Sparks(from, direction, muzzleColor, 12, 14f, 34f, 0.5f, 0.28f, 25f);
            Smoke(from, direction, 5, muzzleFlashSize * 0.7f);
        }

        GameObject go = new GameObject(kind + "_Shot");
        go.transform.position = from;
        go.AddComponent<PlayerShot>().Init(this, kind, from, target, onArrive);
    }

    // ตอนกระสุนถึงเป้า
    public void Impact(BattleProjectileKind kind, Vector3 position, Vector3 direction)
    {
        SoundManager.Play(kind == BattleProjectileKind.EnergyBlast ? Sfx.BlastImpact : kind == BattleProjectileKind.Knife ? Sfx.KnifeImpact : Sfx.ShotImpact);
        if (kind == BattleProjectileKind.EnergyBlast) BlastImpact(position, direction, blastColor, blastCoreColor);
        else if (kind == BattleProjectileKind.Knife)
        {
            // มีดปัก: ประกายโลหะกระเด็นสวนทาง + แสงวาบสั้น
            Flash(position, knifeTrailColor, knifeImpactSize, 0.12f);
            Sparks(position, -direction, new Color(1f, 0.86f, 0.6f, 1f), 14, 10f, 26f, 0.35f, 0.25f, 70f);
            Ring(position, knifeTrailColor, knifeImpactSize * 0.9f, 0.18f);
        }
        else
        {
            Flash(position, bulletColor, bulletImpactSize, 0.14f);
            Flash(position, muzzleColor, bulletImpactSize * 0.6f, 0.1f);
            Sparks(position, -direction, muzzleColor, 16, 12f, 30f, 0.5f, 0.3f, 75f);
            Ring(position, bulletColor, bulletImpactSize * 1.1f, 0.22f);
        }
    }

    // แสงร่ายตอนปล่อยลูกพลังงาน (ผู้เล่นและศัตรู)
    public void BlastCast(Vector3 from, Vector3 direction, Color main, Color core)
    {
        Flash(from, main, castFlashSize, 0.3f);
        Flash(from, core, castFlashSize * 0.45f, 0.2f);
        Ring(from, main, castFlashSize * 0.9f, 0.25f);
        Sparks(from, direction, main, 14, 8f, 22f, 0.6f, 0.4f, 70f);
    }

    // ระเบิดพลังงานตอนลูกพลังงานถึงเป้า (ผู้เล่นและศัตรู)
    public void BlastImpact(Vector3 position, Vector3 direction, Color main, Color core)
    {
        Flash(position, main, blastImpactSize, 0.4f);
        Flash(position, core, blastImpactSize * 0.4f, 0.22f);
        Ring(position, main, blastImpactSize * 1.2f, 0.5f);
        Ring(position, core, blastImpactSize * 0.7f, 0.3f);
        Sparks(position, -direction, main, 26, 10f, 28f, 0.7f, 0.5f, 180f);
        Sparks(position, -direction, core, 10, 6f, 16f, 0.45f, 0.35f, 120f);
    }

    // ---------------------------------------------------------------- ชิ้นส่วนเอฟเฟกต์
    // แสงวาบลูกเดียว: โตเร็วแล้วจาง
    public void Flash(Vector3 position, Color color, float size, float life)
    {
        ParticleSystem ps = MakeSystem("Flash", position, Additive(Dot), life);
        var main = ps.main;
        main.startSize = size;
        main.startColor = color;
        main.startSpeed = 0f;
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.15f)));
        FadeOut(ps, color);
        ps.Play();
        ps.Emit(1);
    }

    // วงแหวนขยาย (หันเข้ากล้อง)
    public void Ring(Vector3 position, Color color, float size, float life)
    {
        ParticleSystem ps = MakeSystem("Ring", position, Additive(RingTexture), life);
        var main = ps.main;
        main.startSize = size;
        main.startColor = color;
        main.startSpeed = 0f;
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f)));
        FadeOut(ps, color);
        ps.Play();
        ps.Emit(1);
    }

    // ประกายเส้น ๆ พุ่งออกเป็นกรวยตาม direction (angle 180 = ครึ่งทรงกลม)
    public void Sparks(Vector3 position, Vector3 direction, Color color, int count, float minSpeed, float maxSpeed, float size, float life, float angle)
    {
        ParticleSystem ps = MakeSystem("Sparks", position, Additive(Dot), life);
        var main = ps.main;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startColor = color;
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = angle >= 179f ? ParticleSystemShapeType.Hemisphere : ParticleSystemShapeType.Cone;
        shape.angle = Mathf.Min(angle * 0.5f, 89f);
        shape.radius = 0.05f;
        ps.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction : Vector3.forward);
        // หน่วงความเร็ว (ประกายพุ่งแรงแล้วชะลอ) ไม่จำกัดความเร็วสูงสุด
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 1000f;
        limit.drag = 4f;
        limit.multiplyDragByParticleSize = false;
        limit.multiplyDragByParticleVelocity = false;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.04f;
        renderer.lengthScale = 2.5f;
        FadeOut(ps, color);
        ps.Play();
        ps.Emit(count);
    }

    // ควันจาง ๆ ที่ปากกระบอก
    public void Smoke(Vector3 position, Vector3 direction, int count, float size)
    {
        ParticleSystem ps = MakeSystem("Smoke", position, SmokeMaterial, 0.6f);
        var main = ps.main;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        Color smoke = new Color(0.75f, 0.72f, 0.7f, 0.35f);
        main.startColor = smoke;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.05f;
        ps.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction : Vector3.forward);
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f)));
        FadeOut(ps, smoke);
        ps.Play();
        ps.Emit(count);
    }

    public ParticleSystem MakeSystem(string name, Vector3 position, Material material, float life, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = position;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = Mathf.Max(0.05f, life);
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = life;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 64;
        main.stopAction = parent == null ? ParticleSystemStopAction.Destroy : ParticleSystemStopAction.None;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.enabled = false;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = sortingOrder;
        if (parent == null && Application.isPlaying) Destroy(go, life + 1f);
        // ยังไม่ Play: ผู้เรียกตั้งค่า (เช่น duration) ให้เสร็จก่อน แล้วค่อย Play + Emit (ตั้ง duration ระหว่างเล่นไม่ได้)
        return ps;
    }

    public static void FadeOut(ParticleSystem ps, Color color)
    {
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = gradient;
    }

    // ---------------------------------------------------------------- วัสดุ / ภาพที่วาดในโค้ด
    private static Texture2D dot, ring, line;
    private static Material smokeMaterial;
    private static readonly System.Collections.Generic.Dictionary<Texture2D, Material> additive = new System.Collections.Generic.Dictionary<Texture2D, Material>();

    public static Texture2D Dot => dot != null ? dot : dot = MakeTexture(64, (u, v) =>
    {
        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
        float a = Mathf.Clamp01(1f - d);
        return a * a;
    });

    public static Texture2D RingTexture => ring != null ? ring : ring = MakeTexture(128, (u, v) =>
    {
        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
        float band = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.14f);
        float inner = Mathf.Clamp01(1f - d) * 0.25f;
        return Mathf.Clamp01(band * band + inner * inner);
    });

    // เส้นแสงของ Tracer: นุ่มด้านขวาง เต็มตามยาว
    public static Texture2D LineTexture => line != null ? line : line = MakeTexture(64, (u, v) =>
    {
        float across = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 2f);
        return across * across;
    });

    private static Texture2D MakeTexture(int size, Func<float, float, float> alpha)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha((x + 0.5f) / size, (y + 0.5f) / size));
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    // แสงแบบบวก (URP Particles/Unlit, Additive) แบบเดียวกับ SoftGlow
    public static Material Additive(Texture2D texture)
    {
        if (additive.TryGetValue(texture, out Material cached) && cached != null) return cached;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material material;
        if (shader == null) material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
        else
        {
            material = new Material(shader);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent + 60;
        }
        material.hideFlags = HideFlags.DontSave;
        additive[texture] = material;
        return material;
    }

    private static Material SmokeMaterial
    {
        get
        {
            if (smokeMaterial == null) smokeMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = Dot, renderQueue = (int)RenderQueue.Transparent + 40, hideFlags = HideFlags.DontSave };
            return smokeMaterial;
        }
    }
}
