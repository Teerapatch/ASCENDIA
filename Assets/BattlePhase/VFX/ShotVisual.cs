using UnityEngine;

// หน้าตาของกระสุน 1 ลูก (ใช้ทั้งกระสุนผู้เล่น PlayerShot และกระสุนพลังงานของศัตรู EnemyProjectile)
// แกน (และรัศมีเต้น + หางประกาย สำหรับลูกพลังงาน) ติดกับตัวกระสุน + เส้นแสงตามหลัง (TrailRenderer บนตัวกระสุน)
public class ShotVisual
{
    public Transform root;
    public TrailRenderer trail;
    public ParticleSystem head, halo, sparkTrail;

    public static ShotVisual Build(BattleProjectiles owner, GameObject host, bool blast, Color main, Color core, float size, float trailTime, float trailWidth)
    {
        var visual = new ShotVisual();
        visual.root = new GameObject("Visual").transform;
        visual.root.SetParent(host.transform, false);

        // หัวกระสุน / แกนพลังงาน: อนุภาคค้างติดตัว (Local space)
        visual.head = Persistent(owner, "Core", core, size, visual.root);
        if (blast)
        {
            visual.halo = Persistent(owner, "Halo", main, size * 2.4f, visual.root);
            // หางประกายที่หลุดจากลูกพลังงาน (World space ค้างอยู่กลางทาง)
            ParticleSystem sparks = owner.MakeSystem("SparkTrail", host.transform.position, BattleProjectiles.Additive(BattleProjectiles.Dot), 0.35f, visual.root);
            var m = sparks.main;
            m.loop = true;
            m.duration = 1f;
            m.startSize = new ParticleSystem.MinMaxCurve(size * 0.25f, size * 0.6f);
            m.startColor = main;
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = size * 0.35f;
            var emission = sparks.emission;
            emission.rateOverDistance = 6f / Mathf.Max(0.2f, size);
            BattleProjectiles.FadeOut(sparks, main);
            sparks.Play();
            visual.sparkTrail = sparks;
        }

        // เส้นแสงตามหลัง
        TrailRenderer trail = host.AddComponent<TrailRenderer>();
        trail.sharedMaterial = BattleProjectiles.Additive(BattleProjectiles.LineTexture);
        trail.time = trailTime;
        trail.minVertexDistance = 0.1f;
        trail.widthMultiplier = trailWidth;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(core, 0f), new GradientColorKey(main, 0.4f), new GradientColorKey(main, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.sortingOrder = owner.sortingOrder;
        trail.autodestruct = false;
        visual.trail = trail;
        return visual;
    }

    // อนุภาคตัวเดียวที่อยู่ตลอดการบิน (ตามตัวกระสุน)
    private static ParticleSystem Persistent(BattleProjectiles owner, string name, Color color, float size, Transform parent)
    {
        ParticleSystem ps = owner.MakeSystem(name, parent.position, BattleProjectiles.Additive(BattleProjectiles.Dot), 30f, parent);
        var m = ps.main;
        m.simulationSpace = ParticleSystemSimulationSpace.Local;
        m.startSize = size;
        m.startColor = color;
        m.startSpeed = 0f;
        ps.Play();
        ps.Emit(1);
        return ps;
    }

    // รัศมีเต้น (เวลาจริงตั้งแต่ยิง)
    public void Pulse(float seconds)
    {
        if (halo != null) halo.transform.localScale = Vector3.one * (1f + Mathf.Sin(seconds * 30f) * 0.12f);
    }

    // ซ่อนตัวกระสุน ปล่อยเส้นแสง/ประกายที่ค้างให้จางเอง คืนเวลาที่ควรรอก่อนลบตัวกระสุน
    public float Finish()
    {
        if (head != null) head.Clear();
        if (halo != null) halo.Clear();
        if (sparkTrail != null)
        {
            sparkTrail.transform.SetParent(null, true);
            sparkTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(sparkTrail.gameObject, 0.6f);
        }
        if (trail != null) trail.emitting = false;
        return trail != null ? trail.time + 0.1f : 0.1f;
    }
}
