using UnityEngine;

[RequireComponent(typeof(PlayerStateManager))]
public class PlayerClimbController : MonoBehaviour
{
    [Header("Settings")]
    public float verticalSpeed = 5f;
    public float horizontalSpeed = 3f;
    public float minX = -4f;
    public float maxX = 4f;
    public float maxPlayerHeight = 5f;

    [Tooltip("ถ้าน้ำหนักเต็มกระเป๋า จะให้ปีนช้าลงเหลือกี่เปอร์เซ็นต์? (0.4 = 40% ของความเร็วปกติ)")]
    public float minSpeedMultiplier = 0.4f;

    [Header("References")]
    public Transform environmentContainer; 

    private PlayerStateManager stateManager;
    
    // 🌟 ตัวแปรเก็บค่าตัวคูณความเร็วแบบเรียลไทม์
    private float currentSpeedMultiplier = 1f;

    private void Awake()
    {
        stateManager = GetComponent<PlayerStateManager>();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        
        // 🌟 1. อัปเดตตัวคูณความเร็วตลอดเวลา (Real-time Update) 
        // ไม่ว่าผู้เล่นจะทำอะไรอยู่ ถ้าน้ำหนักเปลี่ยน ความเร็วจะคำนวณรอไว้เลย
        UpdateSpeedMultiplier();

        if (stateManager.currentState != PlayerStateManager.State.Climbing) return;

        HandleClimbing();
    }

    // ฟังก์ชันสำหรับคำนวณความหนืดจากน้ำหนัก
    private void UpdateSpeedMultiplier()
    {
        if (GameManager.Instance.playerData != null)
        {
            PlayerData data = GameManager.Instance.playerData;
            
            float weightPercent = data.weight / data.maxWeight;
            weightPercent = Mathf.Clamp01(weightPercent); 

            // คำนวณความเร็วแบบเรียลไทม์
            currentSpeedMultiplier = Mathf.Lerp(1f, minSpeedMultiplier, weightPercent);
        }
        else
        {
            currentSpeedMultiplier = 1f; // เซฟตี้: ถ้าไม่มีข้อมูล ให้เร็วปกติ
        }
    }

    private void HandleClimbing()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // 🌟 2. ดึงความเร็วปัจจุบันมาใช้ (คูณกับตัวคูณแบบเรียลไทม์)
        float currentVertSpeed = verticalSpeed * currentSpeedMultiplier;
        float currentHorzSpeed = horizontalSpeed * currentSpeedMultiplier;

        // --- ขยับซ้ายขวา ---
        Vector3 newPos = transform.position + new Vector3(h, 0, 0) * currentHorzSpeed * Time.deltaTime;
        newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        transform.position = newPos;

        // --- ปีนขึ้น ---
        if (v > 0.1f)
        {
            if (transform.position.y < maxPlayerHeight)
            {
                transform.Translate(Vector3.up * currentVertSpeed * Time.deltaTime, Space.World);
            }
            else
            {
                if (environmentContainer != null)
                    environmentContainer.Translate(Vector3.down * currentVertSpeed * Time.deltaTime);
            }
        }
        // --- ปีนลง ---
        else if (v < -0.1f)
        {
            if (environmentContainer != null)
                environmentContainer.Translate(Vector3.up * currentVertSpeed * Time.deltaTime);
        }
    }
}