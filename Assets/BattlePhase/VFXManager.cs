using System.Collections;
using UnityEngine;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance;

    [Header("Parry Effects")]
    public GameObject parrySlashVFXPrefab; // Prefab ที่มี Component 'CurvedSlashEffect' และ 'LineRenderer'
    public GameObject parrySparkVFXPrefab; // Prefab ประกายไฟ

    [Header("Parry Success VFX")]
    [Tooltip("เอฟเฟกต์ตอน Parry สำเร็จ (ParryVFX) วางตรงจุดเดียวกับ Hint")]
    public GameObject parrySuccessVFXPrefab;
    [Tooltip("Prefab วนลูป: ปล่อยอนุภาคกี่วินาทีแล้วหยุด")]
    public float parrySuccessEmitDuration = 0.5f;
    [Tooltip("ลบทิ้งหลังเกิดกี่วินาที (เผื่อเวลาให้อนุภาคที่ปล่อยไปแล้วจางหาย)")]
    public float parrySuccessLifetime = 2f;
    [Tooltip("มุมกระจายของประกาย (องศา) รอบทิศที่ Parry: ประกายที่เดิมพุ่งรอบตัวจะถูกบีบให้พุ่งไปทางเดียว")]
    public float parrySparkSpread = 30f;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// เสกเอฟเฟกต์รอยฟันแบบเส้นโค้งตามการลาก
    /// </summary>
    public void SpawnParrySlash(Vector3 startPosition, Vector3 endPosition)
    {
        if (parrySlashVFXPrefab != null)
        {
            // สร้าง GameObject ของรอยฟัน
            GameObject vfxObj = Instantiate(parrySlashVFXPrefab, startPosition, Quaternion.identity);
            CurvedSlashEffect slashEffect = vfxObj.GetComponent<CurvedSlashEffect>();

            if (slashEffect != null)
            {
                slashEffect.Initialize(startPosition, endPosition, 0.5f);
            }
            else
            {
                Destroy(vfxObj, 0.5f);
            }
        }

        // เสกประกายไฟที่จุดปะทะปลายทาง (End Position) พุ่งไปตามทิศที่ลาก
        if (parrySparkVFXPrefab != null)
        {
            GameObject sparkObj = Instantiate(parrySparkVFXPrefab, endPosition, Quaternion.identity);
            Camera cam = Camera.main;
            if (cam != null) AimEmission(sparkObj, endPosition - startPosition, cam.transform.forward, parrySparkSpread);
            Destroy(sparkObj, 0.5f);
        }

        Debug.Log($"Spawned Curved Parry Slash from {startPosition} to {endPosition}");
    }

    /// <summary>
    /// เอฟเฟกต์ตอน Parry สำเร็จ (ใช้ร่วมกับรอยฟันด้านบน)
    /// </summary>
    /// rotation: แกน Z หันตามกล้อง และแกน +X ชี้ไปทางทิศที่ Parry (ประกายพุ่งไปทาง +X)
    public void SpawnParrySuccess(Vector3 position, Quaternion rotation, float scale, int sortingBoost = 0)
    {
        if (parrySuccessVFXPrefab == null)
        {
            Debug.LogWarning("VFXManager: ยังไม่ได้ใส่ parrySuccessVFXPrefab (ParryVFX)");
            return;
        }

        GameObject vfx = PlayTimed(parrySuccessVFXPrefab, position, rotation, scale, parrySuccessEmitDuration, parrySuccessLifetime);
        AimEmission(vfx, rotation * Vector3.right, rotation * Vector3.forward, parrySparkSpread);
        BoostSorting(vfx, sortingBoost);

        // เล่นด้วยเวลาจริง: ระเบิดแสงทันทีแม้ Hitstop หยุดเวลาเกมไว้
        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule main = ps.main;
            main.useUnscaledTime = true;
        }
    }

    // หันการพ่นอนุภาคทุกตัวใน VFX ไปทาง worldDirection (เรียกหลัง Instantiate ในเฟรมเดียวกัน ก่อนอนุภาคชุดแรกออก)
    // - Shape แบบทรงกลม/วงกลม (พุ่งรอบตัว) -> เปลี่ยนเป็นกรวยกว้าง spreadAngle องศา
    // - Shape ที่มีทิศอยู่แล้ว (เช่นกรวย) -> หมุนให้แกนชี้ไปทางเดียวกัน
    // - ตัวที่ไม่มี Shape (วงแหวน แสงแฟลร์ อยู่กับที่) ไม่ต้องแก้
    // - ปิดแรงโน้มถ่วง: Sparks / Sparks Delay / ตัว root ใน Prefab มี Gravity 0.5-1 ทำให้ประกายโค้งตกลงพื้น ไม่พุ่งตามทิศ
    // - เส้นแสงแบบ Billboard (Flare ยาวผอม เช่น 15x0.1) หันหน้าเข้ากล้องเสมอ มุมบนจอมาจาก startRotation ไม่ใช่การหมุนของ Object
    //   -> ตั้ง startRotation ให้แกนยาวของเส้นวางตามทิศบนจอ
    // worldUp ต้องตั้งฉากกับทิศ (ใช้แกนหน้าของกล้อง) เพื่อกำหนดการหมุนรอบแกนกรวยให้คงที่
    public static void AimEmission(GameObject vfx, Vector3 worldDirection, Vector3 worldUp, float spreadAngle)
    {
        if (vfx == null || worldDirection.sqrMagnitude < 0.0001f) return;

        Camera cam = Camera.main;
        float screenAngle = cam != null
            ? Mathf.Atan2(Vector3.Dot(worldDirection, cam.transform.up), Vector3.Dot(worldDirection, cam.transform.right)) * Mathf.Rad2Deg
            : 0f;

        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            AlignBillboardStreak(ps, screenAngle);

            ParticleSystem.ShapeModule shape = ps.shape;
            if (!shape.enabled) continue;

            ParticleSystem.MainModule main = ps.main;
            main.gravityModifier = 0f;

            switch (shape.shapeType)
            {
                case ParticleSystemShapeType.Sphere:
                case ParticleSystemShapeType.SphereShell:
                case ParticleSystemShapeType.Hemisphere:
                case ParticleSystemShapeType.HemisphereShell:
                case ParticleSystemShapeType.Circle:
                case ParticleSystemShapeType.CircleEdge:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = spreadAngle;
                    shape.arc = 360f;
                    break;
            }

            Vector3 localDir = ps.transform.InverseTransformDirection(worldDirection);
            Vector3 localUp = ps.transform.InverseTransformDirection(worldUp);
            shape.rotation = Quaternion.LookRotation(localDir, localUp).eulerAngles;
        }
    }

    // หมุนเส้นแสง Billboard (หันเข้ากล้อง, ขนาดแกน X/Y ไม่เท่ากัน) ให้แกนยาววางตามมุมบนจอ screenAngle (องศา, 0 = ขวา, ทวนเข็ม)
    // การหมุนของอนุภาคใน Unity เป็นบวก = ตามเข็มนาฬิกาบนจอ จึงต้องใส่ค่าลบ
    private static void AlignBillboardStreak(ParticleSystem ps, float screenAngle)
    {
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        if (r == null || r.renderMode != ParticleSystemRenderMode.Billboard || r.alignment != ParticleSystemRenderSpace.View) return;

        ParticleSystem.MainModule main = ps.main;
        if (!main.startSize3D || main.startRotation3D) return;

        float sizeX = main.startSizeX.constantMax;
        float sizeY = main.startSizeY.constantMax;
        if (Mathf.Approximately(sizeX, sizeY)) return; // ทรงกลม ไม่มีทิศ

        float longAxisAngle = sizeX > sizeY ? screenAngle : screenAngle - 90f;
        main.startRotation = -longAxisAngle * Mathf.Deg2Rad;
        main.flipRotation = 0f;
    }

    // ดัน Sorting Order ของทุก Renderer ใน VFX ขึ้น (คงลำดับภายในเดิม) ให้วาดทับอนุภาคประกอบฉาก
    public static void BoostSorting(GameObject vfx, int boost)
    {
        if (vfx == null || boost == 0) return;
        foreach (Renderer r in vfx.GetComponentsInChildren<Renderer>())
        {
            r.sortingOrder += boost;
        }
    }

    // เล่น VFX ที่ตั้งเป็น Loop ไว้แบบมีเวลาจบ: ปล่อยอนุภาค emitDuration วินาที แล้วลบทิ้งหลัง lifetime วินาที
    public GameObject PlayTimed(GameObject prefab, Vector3 position, Quaternion rotation, float scale, float emitDuration, float lifetime)
    {
        GameObject vfx = Instantiate(prefab, position, rotation);
        vfx.transform.localScale = prefab.transform.localScale * scale;

        if (emitDuration > 0f) StartCoroutine(StopEmittingAfter(vfx, emitDuration));
        Destroy(vfx, Mathf.Max(lifetime, emitDuration));
        return vfx;
    }

    private IEnumerator StopEmittingAfter(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (vfx == null) yield break;

        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}