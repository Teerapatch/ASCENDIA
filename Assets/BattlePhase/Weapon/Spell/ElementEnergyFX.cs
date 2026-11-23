using UnityEngine;
using UnityEngine.Rendering;

// หน้าตา Orb ธาตุที่ลอยอยู่ (SpellOrbManager): เป็นพลังงานของธาตุนั้น ๆ แทนลูกกลม สร้างด้วย Particle ในโค้ดทั้งหมด
//  - ทุกธาตุ: แกนสว่าง + รัศมีเรืองเต้น
//  - Fire : เปลวไฟพุ่งขึ้นจากลูก (World space ไฟลากตามตอนลอย) + ลูกไฟเล็ก ๆ ปลิว
//  - Water: หยดน้ำโคจรรอบลูก + ฟองลอยขึ้นช้า ๆ
//  - Wind : เส้นลมหมุนวนรอบลูก + ประกายลมจาง ๆ
//  - Earth: เศษหินโคจร (ทึบ) + ฝุ่นสีอำพัน
// เป็นลูกของ Orb: FloatingElementOrb.SetVisible เปิด/ปิดลูกทั้งหมด (Particle ตั้งเล่นเองตอนเปิด + Prewarm จึงเต็มทันที)
// ขนาดเป็นหน่วยของ Orb (Scaling Hierarchy) Orb ขยายตอนเล็งโดน อนุภาคจึงขยายตาม
public class ElementEnergyFX : MonoBehaviour
{
    public ElementType element;

    private static Material additive, solid;
    private static Texture2D streak;

    public static ElementEnergyFX Attach(GameObject orb, ElementType element)
    {
        var holder = new GameObject("ElementEnergy");
        holder.transform.SetParent(orb.transform, false);
        var fx = holder.AddComponent<ElementEnergyFX>();
        fx.element = element;
        fx.Build();
        return fx;
    }

    private void Build()
    {
        Color main = ElementTypes.GetColor(element);
        Color core = Color.Lerp(main, Color.white, 0.65f);

        // แกน + รัศมี (ทุกธาตุ)
        var coreFx = System("Core", Additive, ParticleSystemSimulationSpace.Local);
        Setup(coreFx, rate: 10f, life: new Vector2(0.35f, 0.5f), size: new Vector2(0.5f, 0.65f), speed: Vector2.zero, color: core);
        SizeCurve(coreFx, 0.7f, 1f, 0.8f);
        var halo = System("Halo", Additive, ParticleSystemSimulationSpace.Local);
        Setup(halo, rate: 5f, life: new Vector2(0.8f, 1.1f), size: new Vector2(1.5f, 1.9f), speed: Vector2.zero, color: new Color(main.r, main.g, main.b, 0.28f));
        SizeCurve(halo, 0.6f, 1f, 1.15f);

        switch (element)
        {
            case ElementType.Fire: BuildFire(main); break;
            case ElementType.Water: BuildWater(main); break;
            case ElementType.Wind: BuildWind(main); break;
            case ElementType.Earth: BuildEarth(main); break;
            default: BuildWind(main); break;
        }
    }

    private void BuildFire(Color main)
    {
        // เปลวไฟ: เหลืองขาว -> ส้ม -> แดง แล้วจาง พุ่งขึ้นและหดลง
        var flames = System("Flames", Additive, ParticleSystemSimulationSpace.World);
        Setup(flames, rate: 32f, life: new Vector2(0.4f, 0.75f), size: new Vector2(0.55f, 0.95f), speed: new Vector2(0.4f, 1.1f), color: Color.white);
        Shape(flames, ParticleSystemShapeType.Sphere, 0.28f);
        Gravity(flames, -1.6f);
        SizeCurve(flames, 1f, 0.75f, 0f);
        Gradient(flames, new[] { new Color(1f, 0.95f, 0.7f), new Color(1f, 0.55f, 0.15f), new Color(0.8f, 0.12f, 0.05f) }, new[] { 0f, 0.35f, 1f });
        Noise(flames, 0.6f, 2.5f);

        var embers = System("Embers", Additive, ParticleSystemSimulationSpace.World);
        Setup(embers, rate: 7f, life: new Vector2(0.7f, 1.2f), size: new Vector2(0.08f, 0.16f), speed: new Vector2(0.5f, 1.4f), color: new Color(1f, 0.7f, 0.3f));
        Shape(embers, ParticleSystemShapeType.Sphere, 0.45f);
        Gravity(embers, -0.8f);
        Noise(embers, 1.2f, 1.5f);
        FadeOut(embers);
    }

    private void BuildWater(Color main)
    {
        // หยดน้ำโคจรรอบลูก (Local + Orbital) สีฟ้าขาว
        var drops = System("Droplets", Additive, ParticleSystemSimulationSpace.Local);
        Setup(drops, rate: 16f, life: new Vector2(0.9f, 1.3f), size: new Vector2(0.14f, 0.28f), speed: Vector2.zero, color: Color.Lerp(main, Color.white, 0.4f));
        Shape(drops, ParticleSystemShapeType.Circle, 0.65f);
        Orbit(drops, new Vector3(0.6f, 3.2f, 0.4f), -0.15f);
        FadeInOut(drops);

        // ฟองลอยขึ้นช้า ๆ
        var bubbles = System("Bubbles", Additive, ParticleSystemSimulationSpace.World);
        Setup(bubbles, rate: 5f, life: new Vector2(0.9f, 1.4f), size: new Vector2(0.12f, 0.22f), speed: new Vector2(0.2f, 0.5f), color: new Color(0.75f, 0.95f, 1f, 0.8f));
        Shape(bubbles, ParticleSystemShapeType.Sphere, 0.4f);
        Gravity(bubbles, -0.4f);
        Noise(bubbles, 0.4f, 1f);
        FadeInOut(bubbles);
    }

    private void BuildWind(Color main)
    {
        // เส้นลมหมุนวน: ยืดตามความเร็ว (Stretch) โคจรรอบแกนตั้ง
        var gusts = System("Gusts", Additive, ParticleSystemSimulationSpace.Local);
        Setup(gusts, rate: 24f, life: new Vector2(0.45f, 0.7f), size: new Vector2(0.09f, 0.16f), speed: Vector2.zero, color: Color.Lerp(main, Color.white, 0.5f));
        Shape(gusts, ParticleSystemShapeType.Circle, 0.75f);
        Orbit(gusts, new Vector3(0.4f, 6.5f, 0.4f), 0.35f);
        var renderer = gusts.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.12f;
        renderer.lengthScale = 1.2f;
        FadeInOut(gusts);

        var motes = System("Motes", Additive, ParticleSystemSimulationSpace.World);
        Setup(motes, rate: 8f, life: new Vector2(0.6f, 1f), size: new Vector2(0.06f, 0.12f), speed: new Vector2(0.6f, 1.4f), color: new Color(0.85f, 1f, 0.9f, 0.9f));
        Shape(motes, ParticleSystemShapeType.Sphere, 0.5f);
        Noise(motes, 1.5f, 2f);
        FadeInOut(motes);
    }

    private void BuildEarth(Color main)
    {
        // เศษหินทึบสีน้ำตาลเข้มโคจรรอบลูก หมุนตัวเอง
        var rocks = System("Rocks", Solid, ParticleSystemSimulationSpace.Local);
        Setup(rocks, rate: 9f, life: new Vector2(1f, 1.5f), size: new Vector2(0.16f, 0.3f), speed: Vector2.zero, color: new Color(0.42f, 0.3f, 0.2f, 1f));
        Shape(rocks, ParticleSystemShapeType.Circle, 0.7f);
        Orbit(rocks, new Vector3(0.3f, 2.4f, 0.3f), 0f);
        var main2 = rocks.main;
        main2.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var rotation = rocks.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        FadeInOut(rocks);

        var dust = System("Dust", Additive, ParticleSystemSimulationSpace.World);
        Setup(dust, rate: 7f, life: new Vector2(0.8f, 1.3f), size: new Vector2(0.5f, 0.9f), speed: new Vector2(0.1f, 0.4f), color: new Color(main.r, main.g, main.b, 0.35f));
        Shape(dust, ParticleSystemShapeType.Sphere, 0.5f);
        SizeCurve(dust, 0.6f, 1f, 1.3f);
        FadeInOut(dust);
    }

    // ---------------------------------------------------------------- ตัวช่วยสร้าง Particle
    private ParticleSystem System(string name, Material material, ParticleSystemSimulationSpace space)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.duration = 2f;
        main.simulationSpace = space;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 80;
        var shape = ps.shape;
        shape.enabled = false;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 25;
        return ps;
    }

    private void Setup(ParticleSystem ps, float rate, Vector2 life, Vector2 size, Vector2 speed, Color color)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startColor = color;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        if (!ps.colorOverLifetime.enabled) FadeInOut(ps);
        ps.Play();
    }

    private static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = type;
        shape.radius = radius;
        shape.radiusThickness = type == ParticleSystemShapeType.Circle ? 0.2f : 1f;
        if (type == ParticleSystemShapeType.Circle) shape.rotation = new Vector3(90f, 0f, 0f); // วงแนวนอนรอบแกนตั้ง
    }

    private static void Gravity(ParticleSystem ps, float gravity)
    {
        var main = ps.main;
        main.gravityModifier = gravity;
    }

    private static void Orbit(ParticleSystem ps, Vector3 orbital, float radial)
    {
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalX = orbital.x;
        velocity.orbitalY = orbital.y;
        velocity.orbitalZ = orbital.z;
        velocity.radial = radial;
    }

    private static void Noise(ParticleSystem ps, float strength, float frequency)
    {
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = frequency;
        noise.scrollSpeed = 0.5f;
    }

    private static void SizeCurve(ParticleSystem ps, float start, float middle, float end)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, start), new Keyframe(0.4f, middle), new Keyframe(1f, end)));
    }

    private static void Gradient(ParticleSystem ps, Color[] colors, float[] times)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var keys = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++) keys[i] = new GradientColorKey(colors[i], times[i]);
        var gradient = new UnityEngine.Gradient();
        gradient.SetKeys(keys, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = gradient;
    }

    private static void FadeInOut(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new UnityEngine.Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = gradient;
    }

    private static void FadeOut(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new UnityEngine.Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = gradient;
    }

    // ---------------------------------------------------------------- วัสดุ
    private static Material Additive => additive != null ? additive : additive = BattleProjectiles.Additive(BattleProjectiles.Dot);

    // เศษหิน: ทึบแบบผสมโปร่งใส (Sprites/Default ใช้ได้ในฉาก 3D นี้) ภาพเหลี่ยมมุมเหมือนก้อนหิน
    private static Material Solid
    {
        get
        {
            if (solid != null) return solid;
            if (streak == null)
            {
                const int size = 32;
                streak = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                        // ก้อนเหลี่ยม: รูปหกเหลี่ยมไม่สมมาตรเล็กน้อย ขอบนุ่ม ด้านบนสว่างกว่า
                        float d = Mathf.Max(Mathf.Abs(u) * 0.95f + Mathf.Abs(v) * 0.45f, Mathf.Abs(v) * 1.05f);
                        float a = Mathf.Clamp01((0.92f - d) / 0.08f);
                        float shade = Mathf.Lerp(0.65f, 1.15f, v * 0.5f + 0.5f);
                        pixels[y * size + x] = new Color(Mathf.Min(1f, shade), Mathf.Min(1f, shade), Mathf.Min(1f, shade), a);
                    }
                streak.SetPixels(pixels);
                streak.Apply();
            }
            solid = new Material(Shader.Find("Sprites/Default")) { mainTexture = streak, renderQueue = (int)RenderQueue.Transparent + 45, hideFlags = HideFlags.DontSave };
            return solid;
        }
    }
}
