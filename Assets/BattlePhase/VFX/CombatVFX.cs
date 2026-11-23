using UnityEngine;
using UnityEngine.VFX;

// VFX การต่อสู้ชุดใหม่ (Visual Effect Graph ของอาร์ตจาก VFX_test) Prefab อยู่ใน Resources/BattleVFX
//  - Slash: ทุกการฟันที่มีท่าเหวี่ยงอาวุธ ทั้งผู้เล่นและศัตรู วางรอยฟันในระนาบที่อาวุธเหวี่ยงจริง (3D)
//  - Hit / Parry / Hint: ปิดไว้ก่อน (Use* = false) ระหว่างทยอยใส่ทีละตัว ตอนปิดเกมใช้เอฟเฟกต์เดิม
public static class CombatVFX
{
    public const string Folder = "BattleVFX/";

    // เปิดทีละตัว: ปิด = ไม่สร้าง VFX ชุดใหม่ตัวนั้น (Parry / Hint กลับไปใช้ของเดิมใน VFXManager / ParryManager)
    public static bool UseSlash = true;
    public static bool UseHit = false;
    public static bool UseParry = true;
    public static bool UseHint = true;

    public static float SlashLifetime = 1f;
    public static float HitLifetime = 1.5f;
    public static float ParryLifetime = 2f;
    // ขนาด Hit / Parry / Hint = ความสูงตัว * ค่านี้ (ตัวทดสอบสูง 2 หน่วย = ขนาด 1)
    public static float SizePerHeight = 0.5f;

    private static GameObject slash, hit, parry, hint;
    private static bool loaded;

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        slash = Resources.Load<GameObject>(Folder + "VFX_Slash");
        hit = Resources.Load<GameObject>(Folder + "VFX_Hit");
        parry = Resources.Load<GameObject>(Folder + "VFX_Parry");
        hint = Resources.Load<GameObject>(Folder + "VFX_Hint");
    }

    public static GameObject SlashPrefab { get { Load(); return slash; } }
    public static GameObject HintPrefab { get { Load(); return UseHint ? hint : null; } }
    public static bool HasParry { get { Load(); return UseParry && parry != null; } }

    public static float SizeFor(float bodyHeight) => Mathf.Max(0.5f, bodyHeight * SizePerHeight);

    // หมุนให้แกน X ของรากชี้ตามทิศบนจอ (ศูนย์ = สุ่ม)
    public static Quaternion ScreenRotation(Vector2 screenDirection)
    {
        Camera cam = Camera.main;
        if (screenDirection.sqrMagnitude < 0.0001f) screenDirection = Random.insideUnitCircle.normalized;
        float angle = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg;
        Quaternion view = cam != null ? cam.transform.rotation : Quaternion.identity;
        return view * Quaternion.Euler(0f, 0f, angle);
    }

    // ---------------- Slash ----------------
    // อนุภาคของ Slash_01.vfx เป็น Mesh วงแหวน (SlashMesh02 รัศมี 2.4) หมุนเริ่มที่ (-90, 45, 0) แล้วหมุนรอบแกน Z ของ Mesh ทีละ +2°
    // Slash_Shader สว่างสุดที่ขอบนอกมุม -90 ของ Mesh (UV 0.5, 0.5) แล้วจางไปทางด้านบนของวง = รอยโค้งที่โค้งออกด้านล่าง
    // ค่าพวกนี้อยู่ในพื้นที่ของ VisualEffect ทุกการหมุน/ย่อขยายของลูกใน Prefab VFX_Slash จึงถูกนับเข้าไปเอง (แก้ Prefab ได้อิสระ)
    public static readonly Quaternion SlashParticleAngle = Quaternion.Euler(-90f, 45f, 0f);
    public static readonly Vector3 SlashMeshEdge = new Vector3(0f, -2.4f, 0f);

    // ให้ระนาบรอยฟันหันเข้าหากล้องแค่ไหน (0 = ตามระนาบที่อาวุธเหวี่ยงจริง, 1 = หันหน้าเข้ากล้องเต็มที่) กันมองเห็นแค่สันตอนฟันพุ่งเข้าหากล้อง
    public static float SlashFaceCamera = 0.35f;
    // รัศมีรอยโค้ง = ระยะจากกลางตัวผู้ฟันถึงจุดโดน * ค่านี้ (ศูนย์กลางวงจึงอยู่ราวตัวผู้ฟัน) จำกัดตามความสูงตัวที่โดน
    public static float SlashReachScale = 1f;
    public static float SlashMinRadiusPerHeight = 0.35f;
    public static float SlashMaxRadiusPerHeight = 1.2f;

    public struct SlashPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public float scale; // คูณสเกลของราก Prefab
    }

    // ท่าทางของราก VFX_Slash ที่ทำให้รอยโค้งทับเส้นโค้งที่กำหนด: edge = จุดที่สว่างที่สุด (กลางรอย), outward = ทิศจากศูนย์กลางวงออกไปที่จุดนั้น,
    // sweep = ทิศที่ฟัน ณ จุดนั้น (ตั้งฉากกับ outward), radius = รัศมีวง อ่านการจัดวางของลูกใน Prefab ทุกครั้ง จึงแก้ Prefab ได้อิสระ
    public static bool SlashPoseFromArc(Vector3 edge, Vector3 outward, Vector3 sweep, float radius, out SlashPose pose)
    {
        pose = default;
        Load();
        if (slash == null) return false;
        VisualEffect fx = slash.GetComponentInChildren<VisualEffect>(true);
        if (fx == null || outward.sqrMagnitude < 1e-6f || sweep.sqrMagnitude < 1e-6f) return false;
        Transform root = slash.transform;
        Matrix4x4 inRoot = Matrix4x4.TRS(root.position, root.rotation, Vector3.one).inverse * fx.transform.localToWorldMatrix;
        Vector3 sweepR = inRoot.MultiplyVector(SlashParticleAngle * Vector3.right).normalized;
        Vector3 outR = inRoot.MultiplyVector(SlashParticleAngle * Vector3.down).normalized;
        Vector3 edgeR = inRoot.MultiplyPoint3x4(SlashParticleAngle * SlashMeshEdge);
        float radiusR = Vector3.Distance(edgeR, inRoot.MultiplyPoint3x4(Vector3.zero));
        float rootScale = Mathf.Max(1e-4f, root.localScale.x);
        if (radiusR < 1e-4f) return false;

        pose.rotation = Quaternion.LookRotation(sweep, outward) * Quaternion.Inverse(Quaternion.LookRotation(sweepR, outR));
        pose.scale = Mathf.Max(0.01f, radius) / (radiusR * rootScale);
        pose.position = edge - pose.rotation * (edgeR * rootScale * pose.scale);
        return true;
    }

    // รอยฟันที่จัดไว้ใน Slash VFX Editor: ตำแหน่ง/มุม/ขนาดในพื้นที่ของโมเดลผู้ตี
    public static GameObject SlashAt(Transform model, SlashVFXEntry entry)
    {
        Load();
        if (!UseSlash || slash == null || model == null || entry == null || !entry.enabled) return null;
        GameObject vfx = Object.Instantiate(slash, model.TransformPoint(entry.position), model.rotation * Quaternion.Euler(entry.euler));
        vfx.transform.localScale = slash.transform.localScale * (entry.scale * model.lossyScale.x);
        Object.Destroy(vfx, SlashLifetime);
        return vfx;
    }

    // รอยฟันอัตโนมัติ (ท่าที่ยังไม่ได้จัดใน Slash VFX Editor): hitPoint = จุดที่อาวุธฟันถึง, worldSwing = ทิศที่ปลายอาวุธเคลื่อนในโลก,
    // attackerCenter = กลางตัวผู้ฟัน (จุดหมุนของการเหวี่ยง), bodyHeight = ความสูงตัวที่โดน (จำกัดขนาด)
    public static void Slash(Vector3 hitPoint, Vector3 worldSwing, Vector3 attackerCenter, float bodyHeight)
    {
        Load();
        if (!UseSlash || slash == null) return;
        Camera cam = Camera.main;
        Vector3 view = cam != null ? cam.transform.forward : Vector3.forward;

        // ทิศฟัน (v) กับทิศออกจากจุดหมุน (o) ในระนาบการเหวี่ยง
        Vector3 radial = hitPoint - attackerCenter;
        Vector3 v = worldSwing.sqrMagnitude > 0.0001f ? worldSwing.normalized : Vector3.Cross(radial, view).normalized;
        if (v.sqrMagnitude < 0.0001f) v = cam != null ? cam.transform.right : Vector3.right;
        Vector3 o = radial - Vector3.Dot(radial, v) * v;
        float reach = o.magnitude;
        o = reach > 0.01f ? o / reach : Vector3.Cross(v, view).normalized;
        if (o.sqrMagnitude < 0.0001f) o = Vector3.up;

        // เอียงระนาบเข้าหากล้องบางส่วน แล้วจัด v / o ให้อยู่ในระนาบใหม่
        if (cam != null && SlashFaceCamera > 0f)
        {
            Vector3 normal = Vector3.Cross(v, o).normalized;
            Vector3 toCamera = Vector3.Dot(normal, -view) >= 0f ? -view : view;
            Vector3 facing = Vector3.Slerp(normal, toCamera, SlashFaceCamera).normalized;
            Vector3 v2 = Vector3.ProjectOnPlane(v, facing);
            if (v2.sqrMagnitude > 0.0001f)
            {
                v = v2.normalized;
                o = Vector3.ProjectOnPlane(o, facing);
                o = (o - Vector3.Dot(o, v) * v).normalized;
                if (o.sqrMagnitude < 0.0001f) o = Vector3.Cross(facing, v).normalized;
            }
        }

        float h = Mathf.Max(0.5f, bodyHeight);
        float radius = Mathf.Clamp(reach * SlashReachScale, h * SlashMinRadiusPerHeight, h * SlashMaxRadiusPerHeight);
        if (!SlashPoseFromArc(hitPoint, o, v, radius, out SlashPose pose)) return;
        Spawn(slash, pose.position, pose.rotation, pose.scale, SlashLifetime, h * 0.5f);
    }

    // โดนดาเมจ: ประกายกระแทกที่กลางลำตัว หมุนตามทิศของแรงที่โดนบนจอ (ไม่รู้ทิศ = สุ่ม)
    public static void Hit(Vector3 position, float bodyHeight, Vector2 hitScreenDirection = default)
    {
        Load();
        if (!UseHit) return;
        Spawn(hit, position, ScreenRotation(hitScreenDirection), SizeFor(bodyHeight), HitLifetime, SizeFor(bodyHeight));
    }

    // Parry สำเร็จ: อยู่ในโลก 3D (ไม่แบนติดจอ) +Z ของ Prefab ชี้ไปทาง forward (ผู้เล่นหาศัตรูที่ตี)
    // แนวประกาย (แกน X ของ Prefab) หมุนรอบแกนนั้นให้ตรงกับทิศที่ลากบนจอ
    // ชุดของอาร์ตใหญ่กว่าตัวทดสอบหลายเท่า (กรวยประกายยาว 5 หน่วย) ถ้าขยายเท่า Hit ประกายจะกระจายเลยจอ
    public static float ParrySizePerHeight = 0.25f;

    public static void Parry(Vector3 position, Vector3 forward, Vector2 swipe, float bodyHeight)
    {
        Load();
        if (!UseParry) return;
        float size = Mathf.Max(0.2f, bodyHeight * ParrySizePerHeight);
        Spawn(parry, position, WorldRotation(forward, swipe), size, ParryLifetime, size);
    }

    // หมุนในโลก: +Z = forward (แนวราบ), +X = ทิศบนจอที่ฉายลงระนาบตั้งฉากกับ forward
    public static Quaternion WorldRotation(Vector3 forward, Vector2 screenDirection)
    {
        Camera cam = Camera.main;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
        forward.Normalize();
        if (screenDirection.sqrMagnitude < 0.0001f) screenDirection = Vector2.right;
        Vector3 side = cam != null ? cam.transform.right * screenDirection.x + cam.transform.up * screenDirection.y : new Vector3(screenDirection.x, screenDirection.y, 0f);
        side = Vector3.ProjectOnPlane(side, forward);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(Vector3.up, forward);
        side.Normalize();
        // LookRotation(f, f x s): แกนขวา = (f x s) x f = s
        return Quaternion.LookRotation(forward, Vector3.Cross(forward, side));
    }

    // กันซ้อน: VFX ตัวเดียวกันที่จุดเดิมภายในเวลานี้ (วินาทีจริง) จะไม่สร้างซ้ำ (เช่นสองระบบเรียกในเฟรมเดียวกัน หรือท่าตีรัวหลายฮิต)
    public static float MinRepeatInterval = 0.08f;
    private static readonly System.Collections.Generic.Dictionary<GameObject, (float time, Vector3 position)> lastSpawn
        = new System.Collections.Generic.Dictionary<GameObject, (float, Vector3)>();

    private static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float size, float lifetime, float sameSpotRadius)
    {
        if (prefab == null) return null;
        if (lastSpawn.TryGetValue(prefab, out var last) && Time.unscaledTime - last.time < MinRepeatInterval
            && (last.position - position).sqrMagnitude < sameSpotRadius * sameSpotRadius)
            return null;
        lastSpawn[prefab] = (Time.unscaledTime, position);
        GameObject vfx = Object.Instantiate(prefab, position, rotation);
        vfx.transform.localScale = prefab.transform.localScale * size;
        // ไม่ต้องเรียก Play(): VisualEffect ส่ง OnPlay เองตอนเปิดใช้ (initialEventName) เรียกซ้ำจะระเบิดสองรอบ
        Object.Destroy(vfx, lifetime);
        return vfx;
    }

    // ยิงซ้ำ (ส่ง OnPlay ใหม่) ใช้กับ Hint ที่ค้างนานกว่าหนึ่งรอบของเอฟเฟกต์
    public static void Replay(GameObject vfx)
    {
        if (vfx == null) return;
        foreach (VisualEffect effect in vfx.GetComponentsInChildren<VisualEffect>()) effect.Play();
    }

    // หยุดปล่อยของ VFX Graph (ใช้ตอนเก็บ Hint) ส่วนที่ปล่อยไปแล้วค่อย ๆ หายเอง
    public static void StopEmitting(GameObject vfx)
    {
        if (vfx == null) return;
        foreach (VisualEffect effect in vfx.GetComponentsInChildren<VisualEffect>()) effect.Stop();
    }
}
