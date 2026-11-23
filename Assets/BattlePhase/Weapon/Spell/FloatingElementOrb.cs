using UnityEngine;

public class FloatingElementOrb : MonoBehaviour
{
    public ElementType elementType;
    [Tooltip("แสดงลูกกลมเดิม (ปิดเมื่อใช้ ElementEnergyFX เป็นหน้าตาพลังงานของธาตุ)")]
    public bool showMesh = true;

    [Header("Living Movement Settings")]
    public float moveSpeed = 1.2f;      // ความเร็วในการเคลื่อนที่
    public float wanderRange = 2.5f;    // ระยะการเดินสำรวจรอบจุดเกิด
    public float heightRange = 1.0f;    // ระยะความสูงขึ้นลง

    private Vector3 originPosition;     // จุดกึ่งกลางที่ Orb จะลอยวนเวียนอยู่
    private float seedX, seedY, seedZ;    // ค่าสุ่มเฉพาะตัวของแต่ละลูก (กันไม่ให้ขยับพร้อมกัน)

    private MeshRenderer meshRenderer;
    private Collider col;
    private bool isVisible = false;
    private Vector3 baseScale;
    private float targetScale = 1f, currentScale = 1f;

    public bool IsVisible => isVisible;

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
        baseScale = transform.localScale;

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

        currentScale = Mathf.Lerp(currentScale, targetScale, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        if (baseScale != Vector3.zero) transform.localScale = baseScale * currentScale;
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (meshRenderer != null) meshRenderer.enabled = isVisible && showMesh;
        if (col != null) col.enabled = isVisible;
        // แสงเรือง/ประกายที่เป็นลูกของ Orb ซ่อนตามไปด้วย
        foreach (Transform child in transform) child.gameObject.SetActive(isVisible);
        if (!visible) targetScale = 1f;
    }

    public void SetHighlighted(bool highlighted, float scale)
    {
        targetScale = highlighted ? scale : 1f;
    }

    public void Harvest()
    {
        Debug.Log($"<color=cyan>✨ Harvested {elementType} element!</color>");
        SoftGlow.Puff(transform.position, Vector3.up, ElementTypes.GetColor(elementType), 18);
        Destroy(gameObject);
    }
}
