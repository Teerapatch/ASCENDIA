using UnityEngine;

// ห้องปีน 1 ชั้น = หน้าผา 1 ชิ้น: สุ่ม Prefab จาก mountainPrefabs (ไม่ซ้ำกับชั้นก่อนหน้า) แล้วย้ายจุดเกิด/โซนเลือก Node ของห้องไปตาม Marker ของหน้าผานั้น
// ทำใน Awake เพราะ RoomManager.LoadNewRoom อ่านจุดเกิดทันทีหลัง Instantiate ส่วน NodeSpawner เสกโหนดตอน Start
public class MapGenerator : MonoBehaviour
{
    // หน้าผาของชั้นปัจจุบัน (NodeSelectionManager ใช้วางกล้องเลือก Node)
    public static ClimbableWall CurrentCliff { get; private set; }

    // static: ฉากถูกโหลดใหม่หลังการต่อสู้ (โหมด Single) ก็ยังจำชิ้นล่าสุดได้
    private static int lastCliffIndex = -1;

    [Header("Cliff Prefabs (สุ่ม 1 ชิ้นต่อชั้น)")]
    public GameObject[] mountainPrefabs;

    private void Awake()
    {
        // ทั้งหน้าผาของห้องใช้จุดอ้างอิงเดียวกัน มือที่ IK จับไว้จึงไม่หลุดเมื่อข้ามชิ้น Mesh
        if (!GetComponent<ClimbingSurface>()) gameObject.AddComponent<ClimbingSurface>();

        if (mountainPrefabs == null || mountainPrefabs.Length == 0) return;

        GameObject cliff = Instantiate(mountainPrefabs[PickCliffIndex()], transform);
        CurrentCliff = cliff.GetComponent<ClimbableWall>();
        if (CurrentCliff == null) return;

        CopyMarker(CurrentCliff.climbStart, "WallSpawnPoint");
        CopyMarker(CurrentCliff.nodeZone, "NodeSelectionZone");

        // โซน Node และโหนดลูกของมันถูกออกแบบไว้สเกลเล็ก ใช้สเกลของ Marker ให้เข้ากับโลกของอาร์ต
        Transform zone = transform.Find("NodeSelectionZone");
        if (zone != null && CurrentCliff.nodeZone != null) zone.localScale = CurrentCliff.nodeZone.lossyScale;
    }

    private int PickCliffIndex()
    {
        int index = Random.Range(0, mountainPrefabs.Length);
        if (mountainPrefabs.Length > 1 && index == lastCliffIndex)
        {
            // เลื่อนไปชิ้นอื่นแบบสุ่ม จะได้ไม่ซ้ำกับชั้นก่อนหน้า
            index = (index + Random.Range(1, mountainPrefabs.Length)) % mountainPrefabs.Length;
        }
        lastCliffIndex = index;
        return index;
    }

    private void CopyMarker(Transform marker, string roomChildName)
    {
        Transform target = transform.Find(roomChildName);
        if (marker != null && target != null) target.SetPositionAndRotation(marker.position, marker.rotation);
    }
}
