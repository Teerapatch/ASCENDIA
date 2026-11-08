using UnityEngine;

// กำแพงที่ปีนได้ของ MountainTile: ใช้ Mesh ของหินอาร์ต (Cliff) เป็นพื้นผิวปีนจริง
// - Awake: ใส่ MeshCollider ให้ทุก Mesh ที่มองเห็นใต้ surfaceRoot และตั้งแท็ก Wall (PlayerWalkController เริ่มปีนเมื่อชนแท็กนี้)
// - bounds: BoxCollider (Trigger) ที่วางราบไปกับหน้าผา แกน X ของกล่อง = แนวซ้าย-ขวาของหน้าผา, แกน Z = ทิศที่หันเข้าหาหิน
//   ความกว้างกล่อง (size.x) คือขอบเขตสูงสุดที่ปีนซ้าย-ขวาได้
public class ClimbableWall : MonoBehaviour
{
    public const string WallTag = "Wall";

    [Tooltip("ราก Mesh ที่ใช้เป็นพื้นผิวปีน (ว่าง = ทั้งไทล์)")]
    public Transform surfaceRoot;
    [Tooltip("กล่องขอบเขตการปีน (วางราบกับหน้าผา) ใช้จำกัดซ้าย-ขวาและเป็นทิศที่ผู้ปีนหันเข้าหา")]
    public BoxCollider bounds;

    [Header("Markers (ย้ายได้ใน Prefab: MapGenerator ย้ายจุดของห้องมาตามนี้)")]
    [Tooltip("จุดเกิดต่ำสุด: LowestSpawnPoint ใน Cliff Prefab ปีนลงต่ำกว่านี้ไม่ได้ ตั้งให้ขอบล่างของจอไม่เห็นใต้หน้าผา")]
    public Transform climbStart;
    [Tooltip("โซนเลือก Node เกือบถึงยอด: ปีนถึงตรงนี้แล้วหยุด ยังเห็นหน้าผาเหนือหัว")]
    public Transform nodeZone;
    [Tooltip("ตำแหน่ง/มุมของกล้องตอนเลือก Node")]
    public Transform nodeCamPoint;

    public float MinClimbY => climbStart != null ? climbStart.position.y : float.NegativeInfinity;
    public float MaxClimbY => nodeZone != null ? nodeZone.position.y + 1f : float.PositiveInfinity;

    // ทิศที่ผู้ปีนหันเข้าหาหน้าผา
    public Vector3 Facing => Flatten(bounds != null ? bounds.transform.forward : transform.forward);
    // ทิศของกล้องตอนปีน (แกน +Z ของไทล์ = มุมเดียวกับฉากอาร์ต) หน้าผาเอียงจากกล้องได้ตามที่อาร์ตจัดไว้
    public Vector3 ViewFacing => Flatten(transform.forward);

    private void Awake()
    {
        Transform root = surfaceRoot != null ? surfaceRoot : transform;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;

            MeshCollider mc = mf.GetComponent<MeshCollider>();
            if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mf.gameObject.tag = WallTag;
        }

        // กล่องขอบเขตไม่ใช่ผิวปีน: เป็น Trigger ไม่ให้ขวางตัวผู้เล่น และไม่ติดแท็ก Wall (รังสีหาผิวจะข้ามไป)
        if (bounds != null) bounds.isTrigger = true;
    }

    // ตัดก้าว step ให้ตำแหน่งไม่ออกนอกความกว้างของกล่อง (แกน X ของกล่อง) คืนก้าวที่ย่อแล้ว
    public Vector3 ClampStep(Vector3 position, Vector3 step)
    {
        if (bounds == null) return step;
        float from = LocalX(position), to = LocalX(position + step);
        GetLateralRange(out float min, out float max);
        if (Mathf.Abs(to - from) > 0.0001f && (to < min || to > max))
        {
            float allowed = Mathf.Clamp(to, min, max) - from;
            step *= Mathf.Clamp01(allowed / (to - from));
        }
        return step;
    }

    // ดึงตำแหน่งกลับเข้าขอบซ้าย-ขวาของกล่อง (เลื่อนตามแกน X ของกล่องเท่านั้น)
    public Vector3 ClampPosition(Vector3 position)
    {
        if (bounds == null) return position;
        float x = LocalX(position);
        GetLateralRange(out float min, out float max);
        float clamped = Mathf.Clamp(x, min, max);
        return position + bounds.transform.right * ((clamped - x) * bounds.transform.lossyScale.x);
    }

    private float LocalX(Vector3 world) => bounds.transform.InverseTransformPoint(world).x;

    private void GetLateralRange(out float min, out float max)
    {
        min = bounds.center.x - bounds.size.x * 0.5f;
        max = bounds.center.x + bounds.size.x * 0.5f;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    // ยิงหาพื้นผิวกำแพงที่ใกล้ที่สุด (ข้ามคอลไลเดอร์อื่น เช่น ตัวผู้เล่น/ของที่เก็บได้/กล่องขอบเขต)
    public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit wallHit)
    {
        wallHit = default;
        float best = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance < best && hit.collider.CompareTag(WallTag))
            {
                best = hit.distance;
                wallHit = hit;
            }
        }
        return best < float.MaxValue;
    }
}
