using UnityEngine;

public class FloatingElementOrb : MonoBehaviour
{
    public ElementType elementType;

    [Header("Living Movement Settings")]
    public float moveSpeed = 1.2f;      // ความเร็วในการเคลื่อนที่
    public float wanderRange = 2.5f;    // ระยะการเดินสำรวจรอบจุดเกิด
    public float heightRange = 1.0f;    // ระยะความสูงขึ้นลง

    private Vector3 originPosition;     // จุดกึ่งกลางที่ Orb จะลอยวนเวียนอยู่
    private float seedX, seedY, seedZ;    // ค่าสุ่มเฉพาะตัวของแต่ละลูก (กันไม่ให้ขยับพร้อมกัน)

    private MeshRenderer meshRenderer;
    private Collider col;
    private bool isVisible = false;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        col = GetComponent<Collider>();
        SetVisible(false);
    }

    // ฟังก์ชันรับพิกัดเริ่มต้นจาก Manager
    public void Initialize(Vector3 spawnPos)
    {
        originPosition = spawnPos;

        // สุ่มตัวเลขเริ่มต้นเพื่อให้ Perlin Noise ของแต่ละลูกไม่เหมือนกัน
        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(100f, 200f);
        seedZ = Random.Range(200f, 300f);
    }

    private void Update()
    {
        if (!isVisible) return;

        // ใช้ Perlin Noise คำนวณพิกัด Vector3 แบบสูตรสมูทเป็นธรรมชาติ
        float time = Time.time * moveSpeed;

        float x = Mathf.PerlinNoise(time + seedX, 0f) - 0.5f;
        float y = Mathf.PerlinNoise(0f, time + seedY) - 0.5f;
        float z = Mathf.PerlinNoise(time + seedZ, time + seedZ) - 0.5f;

        // คำนวณตำแหน่งใหม่รอบจุดกำเนิด
        Vector3 offset = new Vector3(x * wanderRange, y * heightRange, z * wanderRange);
        transform.position = originPosition + offset;
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (meshRenderer != null) meshRenderer.enabled = isVisible;
        if (col != null) col.enabled = isVisible;
    }

    public void Harvest()
    {
        Debug.Log($"<color=cyan>✨ Harvested {elementType} element!</color>");
        Destroy(gameObject);
    }
}