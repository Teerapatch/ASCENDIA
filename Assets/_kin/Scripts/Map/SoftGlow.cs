using UnityEngine;
using UnityEngine.Rendering;

// อนุภาคจุดแสงที่สร้างจากโค้ด (ไม่ต้องมี Prefab): ประกายบอกของที่เก็บได้ และฝุ่น/เส้นลมตอนกระโดดเร่ง
// ขนาดเป็นหน่วยโลกของฉากปีน (กล้อง Orthographic สูง 50 หน่วย) จึงต้องใหญ่ระดับ 1 หน่วยขึ้นไปถึงจะเห็น
public static class SoftGlow
{
    private static Material additive, dust;
    private static Texture2D dot;

    private static Texture2D Dot
    {
        get
        {
            if (dot != null) return dot;
            const int size = 32;
            dot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), Vector2.one * (size - 1) * .5f) / (size * .5f);
                    float alpha = Mathf.Clamp01(1f - d);
                    dot.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }
            dot.Apply();
            return dot;
        }
    }

    // แสงแบบบวก (Additive): สว่างเด่นบนทุกพื้นหลัง ใช้กับประกาย
    private static Material Additive
    {
        get
        {
            if (additive != null) return additive;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return Dust;

            additive = new Material(shader);
            additive.SetTexture("_BaseMap", Dot);
            additive.SetColor("_BaseColor", Color.white);
            additive.SetFloat("_Surface", 1f);
            additive.SetFloat("_Blend", 2f);
            additive.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            additive.SetFloat("_DstBlend", (float)BlendMode.One);
            if (additive.HasProperty("_SrcBlendAlpha")) additive.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (additive.HasProperty("_DstBlendAlpha")) additive.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            additive.SetFloat("_ZWrite", 0f);
            additive.SetOverrideTag("RenderType", "Transparent");
            additive.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            additive.renderQueue = (int)RenderQueue.Transparent + 50;
            return additive;
        }
    }

    // ทึบแสงแบบปกติ: ใช้กับฝุ่น
    private static Material Dust
    {
        get
        {
            if (dust == null) dust = new Material(Shader.Find("Sprites/Default")) { mainTexture = Dot, renderQueue = (int)RenderQueue.Transparent + 40 };
            return dust;
        }
    }

    private static ParticleSystem Make(string name, Vector3 position, Transform parent, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        // ขนาดอนุภาคเป็นหน่วยโลก ไม่ขึ้นกับสเกลของพ่อ
        if (parent != null)
        {
            Vector3 s = parent.lossyScale;
            go.transform.localScale = new Vector3(1f / Mathf.Max(.001f, s.x), 1f / Mathf.Max(.001f, s.y), 1f / Mathf.Max(.001f, s.z));
        }

        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 20;
        return particles;
    }

    private static ParticleSystem.MinMaxGradient FadeInOut(Color color, float peakAlpha, float peakAt)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, peakAt), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    // ของที่เก็บได้: แสงเรืองเต้นช้า ๆ ที่ตัวของ + ประกายลอยขึ้นรอบ ๆ
    public static void Sparkle(Transform parent, Vector3 position, Color color, float radius)
    {
        ParticleSystem halo = Make("PickupHalo", position, parent, Additive);
        ParticleSystem.MainModule haloMain = halo.main;
        haloMain.loop = true;
        haloMain.startLifetime = 1.8f;
        haloMain.startSpeed = 0f;
        haloMain.startSize = new ParticleSystem.MinMaxCurve(radius * 3.2f, radius * 4.2f);
        haloMain.maxParticles = 6;
        ParticleSystem.EmissionModule haloEmission = halo.emission;
        haloEmission.rateOverTime = 1.4f;
        ParticleSystem.ShapeModule haloShape = halo.shape;
        haloShape.enabled = false;
        ParticleSystem.ColorOverLifetimeModule haloFade = halo.colorOverLifetime;
        haloFade.enabled = true;
        haloFade.color = FadeInOut(color, .5f, .5f);
        halo.Play();

        ParticleSystem sparks = Make("PickupSparkle", position, parent, Additive);
        ParticleSystem.MainModule main = sparks.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(.35f, .9f);
        main.gravityModifier = -.08f;
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.rateOverTime = 12f;
        ParticleSystem.ShapeModule shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        ParticleSystem.ColorOverLifetimeModule fade = sparks.colorOverLifetime;
        fade.enabled = true;
        fade.color = FadeInOut(Color.Lerp(color, Color.white, .35f), 1f, .25f);
        ParticleSystem.SizeOverLifetimeModule shrink = sparks.sizeOverLifetime;
        shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .2f));
        sparks.Play();
    }

    // ฝุ่นฟุ้งครั้งเดียวจากจุดที่ถีบ/เกาะหิน แล้วลบตัวเอง
    public static void Puff(Vector3 position, Vector3 direction, Color color, int count)
    {
        ParticleSystem particles = Make("ClimbJumpPuff", position, null, Dust);
        if (direction.sqrMagnitude > .001f) particles.transform.rotation = Quaternion.LookRotation(direction);

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = .6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, .95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(.9f, 2.2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 50f;
        shape.radius = .6f;

        ParticleSystem.LimitVelocityOverLifetimeModule drag = particles.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.dampen = .12f;
        drag.limit = 1.2f;

        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        fade.color = FadeInOut(color, .8f, .15f);
        ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .5f, 1f, 1.4f));

        particles.Play();
        Object.Destroy(particles.gameObject, 2f);
    }

    // เส้นลมตามตัวระหว่างพุ่ง: ปล่อยจุดแสงตามทางแล้วหยุดเอง
    public static void Trail(Transform follow, Color color, float duration)
    {
        ParticleSystem particles = Make("ClimbJumpTrail", follow.position, follow, Additive);

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = duration;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .45f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(.8f, 1.6f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 70f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1f;

        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        fade.color = FadeInOut(color, .6f, .1f);
        ParticleSystem.SizeOverLifetimeModule shrink = particles.sizeOverLifetime;
        shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        particles.Play();
        Object.Destroy(particles.gameObject, duration + 1f);
    }
}
