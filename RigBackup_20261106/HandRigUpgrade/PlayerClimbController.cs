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
    [Range(.1f, 1f), Tooltip("Upward pace only. Downward and sideways speeds are unchanged.")]
    public float upwardSpeedMultiplier = .3f;
    [Min(.1f)] public float climbAcceleration = 10f;
    private Vector3 climbVelocity;

    public static Vector3 RequestedVelocity(float h, float v, float horizontal, float vertical, float upwardMultiplier)
    {
        return new Vector3(h * horizontal, v * vertical * (v > 0 ? upwardMultiplier : 1f), 0);
    }

    [Tooltip("ถ้าน้ำหนักเต็มกระเป๋า จะให้ปีนช้าลงเหลือกี่เปอร์เซ็นต์? (0.4 = 40% ของความเร็วปกติ)")]
    public float minSpeedMultiplier = 0.4f;

    [Header("References")]
    public Transform environmentContainer; 

    private PlayerStateManager stateManager;
    private ClimbingContactIK contactIK;
    
    // 🌟 ตัวแปรเก็บค่าตัวคูณความเร็วแบบเรียลไทม์
    private float currentSpeedMultiplier = 1f;

    private void Awake()
    {
        stateManager = GetComponent<PlayerStateManager>();
        contactIK = GetComponentInChildren<ClimbingContactIK>();
    }

    private void Update()
    {
        if (contactIK != null) contactIK.SetClimbVelocity(Vector3.zero);
        if (GameManager.Instance == null) { climbVelocity = Vector3.zero; return; }
        
        // 🌟 1. อัปเดตตัวคูณความเร็วตลอดเวลา (Real-time Update) 
        // ไม่ว่าผู้เล่นจะทำอะไรอยู่ ถ้าน้ำหนักเปลี่ยน ความเร็วจะคำนวณรอไว้เลย
        UpdateSpeedMultiplier();

        if (stateManager.currentState != PlayerStateManager.State.Climbing) { climbVelocity = Vector3.zero; return; }

        if (contactIK != null && !contactIK.HasWall) { climbVelocity = Vector3.zero; return; }
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

                AdvanceClimb(h, v, Time.deltaTime);
    }

    public void AdvanceClimb(float h, float v, float deltaTime)
    {
if ((h < 0 && transform.position.x <= minX) || (h > 0 && transform.position.x >= maxX)) h = 0;
        if (!environmentContainer && (v < 0 || transform.position.y >= maxPlayerHeight)) v = 0;
        Vector3 desired = RequestedVelocity(h, v, horizontalSpeed * currentSpeedMultiplier,
            verticalSpeed * currentSpeedMultiplier, upwardSpeedMultiplier);
        climbVelocity = Vector3.MoveTowards(climbVelocity, desired, climbAcceleration * deltaTime);
        // Release input stops that axis immediately; acceleration only softens starting/reversing.
        if (h == 0) climbVelocity.x = 0;
        if (v == 0) climbVelocity.y = 0;
        if (contactIK)
        {
            contactIK.SetClimbVelocity(climbVelocity);
            if (contactIK.MovementMultiplier <= 0) return;
        }
        Vector3 delta = climbVelocity * deltaTime;
        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x + delta.x, minX, maxX);
        if (delta.y > 0)
        {
            float rise = Mathf.Min(delta.y, Mathf.Max(0, maxPlayerHeight - position.y));
            position.y += rise;
            if (environmentContainer) environmentContainer.Translate(Vector3.down * (delta.y - rise), Space.World);
        }
        else if (delta.y < 0 && environmentContainer)
            environmentContainer.Translate(Vector3.up * -delta.y, Space.World);
        transform.position = position;
    }
}
