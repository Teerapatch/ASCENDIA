using UnityEngine;

[RequireComponent(typeof(PlayerStateManager))]
public class PlayerClimbController : MonoBehaviour
{
    [Header("Settings")]
    public float verticalSpeed = 5f;
    public float horizontalSpeed = 3f;
    public float minX = -4f;
    public float maxX = 4f;

    [Tooltip("ถ้าน้ำหนักเต็มกระเป๋า จะให้ปีนช้าลงเหลือกี่เปอร์เซ็นต์? (0.4 = 40% ของความเร็วปกติ)")]
    public float minSpeedMultiplier = 0.4f;

    [Header("Mesh Climbing (ปีนตามผิวหินของ MountainTile)")]
    [Tooltip("ปีนตาม MeshCollider ของหิน (ClimbableWall): ซ้าย-ขวาเลื่อนไปตามผิวโค้ง และหันหน้าเข้าหาผิวหิน / ปิด = ปีนบนระนาบแบนแบบเดิม")]
    public bool followSurface = true;
    [Tooltip("ระยะห่างจากผิวหินถึงจุดกลางตัว")]
    public float surfaceOffset = 0.6f;
    [Tooltip("ยิงหาผิวหินจากด้านหลังตัวกี่หน่วย (กันกรณีหินโผล่มาด้านหน้าตัว)")]
    public float surfaceProbeBack = 8f;
    public float surfaceProbeDistance = 20f;
    [Tooltip("ระยะกระจายของรังสีที่ใช้เฉลี่ยทิศผิวหิน (หินโลว์โพลีมีหน้าตัดเหลี่ยม ยิงหลายเส้นให้ตัวไม่สั่น)")]
    public Vector2 surfaceSampleSpread = new Vector2(0.5f, 0.7f);
    [Tooltip("ความไวในการเกาะผิว (มาก = ติดผิวเร็ว)")]
    public float surfaceFollowSharpness = 10f;
    [Tooltip("ความไวในการหมุนตัวตามผิว")]
    public float surfaceTurnSharpness = 8f;
    [Tooltip("หันตัวตามผิวได้มากสุดกี่องศาจากทิศของกำแพง (กันปีนอ้อมไปด้านหลังหอหิน)")]
    public float maxSurfaceAngle = 70f;

    // ทิศของหน้าผา (แกน Z ของกล่องขอบเขตใน ClimbableWall) ใช้เป็นแกนอ้างอิงของมุมตัวและสำรองเมื่อยิงไม่โดนผิว
    private Vector3 wallFacing;
    // ไทล์ที่กำลังเกาะอยู่ (ขอบซ้าย-ขวามาจาก BoxCollider ของไทล์นี้) ถ้าไม่มีใช้ minX..maxX
    private ClimbableWall currentWall;

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

        if (stateManager.currentState != PlayerStateManager.State.Climbing)
        {
            wallFacing = Vector3.zero; // เริ่มปีนรอบใหม่ค่อยจำทิศกำแพงใหม่
            currentWall = null;
            return;
        }

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
        // ปีนตามผิวหิน: เลื่อนตามแนวสัมผัสของผิว (ขวามือของตัวที่หันเข้าหาหิน) แต่ไม่ให้เกินความกว้าง BoxCollider ของไทล์
        Vector3 lateral = followSurface ? Vector3.Cross(Vector3.up, Flat(transform.forward, Vector3.forward)) : Vector3.right;
        Vector3 step = lateral * (h * currentHorzSpeed * Time.deltaTime);
        transform.position += ClampStepToBounds(transform.position, step);

        // --- ปีนขึ้น/ลง: หน้าผา 1 ชิ้นต่อชั้นอยู่กับที่ ตัวผู้เล่นเป็นคนเคลื่อนที่ ลงต่ำกว่าจุดเริ่ม/สูงเกินโซน Node ไม่ได้ ---
        if (Mathf.Abs(v) > 0.1f)
        {
            Vector3 climbPos = transform.position;
            float nextY = climbPos.y + Mathf.Sign(v) * currentVertSpeed * Time.deltaTime;
            if (currentWall != null)
            {
                // เทียบกับตำแหน่งเดิมด้วย: ชั้นแรกเริ่มปีนจากพื้นซึ่งต่ำกว่า climbStart ต้องปีนขึ้นได้ ไม่ถูกดึงขึ้นทันที
                nextY = v > 0f
                    ? Mathf.Min(nextY, Mathf.Max(climbPos.y, currentWall.MaxClimbY))
                    : Mathf.Max(nextY, Mathf.Min(climbPos.y, currentWall.MinClimbY));
            }
            climbPos.y = nextY;
            transform.position = climbPos;
        }

        if (followSurface) StickToSurface();
    }

    // ตัดก้าวซ้าย-ขวาให้หยุดพอดีขอบ: ใช้ความกว้าง BoxCollider ของไทล์ที่เกาะอยู่ ถ้าไม่มีใช้ minX..maxX
    // (ก้าวตามผิวโค้งมีหลายแกน จึงย่อทั้งก้าวแทนการ clamp แกนเดียว)
    private Vector3 ClampStepToBounds(Vector3 position, Vector3 step)
    {
        if (followSurface && currentWall != null && currentWall.bounds != null) return currentWall.ClampStep(position, step);

        float targetX = position.x + step.x;
        if (Mathf.Abs(step.x) > 0.0001f && (targetX < minX || targetX > maxX))
        {
            float allowed = Mathf.Clamp(targetX, minX, maxX) - position.x;
            step *= Mathf.Clamp01(allowed / step.x);
        }
        return step;
    }

    // เกาะผิวหิน: ยิงหลายเส้นรอบตัวไปทางที่หันหน้า เฉลี่ยจุด/ทิศของผิว แล้วหมุนตัวให้หันเข้าหาผิวและเลื่อนให้ห่าง surfaceOffset
    private void StickToSurface()
    {
        Vector3 facing = Flat(transform.forward, Vector3.forward);
        bool firstFrame = wallFacing == Vector3.zero;
        if (firstFrame) wallFacing = facing;

        if (!SampleSurface(facing, out Vector3 point, out Vector3 normal, out ClimbableWall wall)
            && !SampleSurface(wallFacing, out point, out normal, out wall))
        {
            return; // ช่องว่างระหว่างหิน: คงตำแหน่ง/ทิศเดิมไว้
        }
        // ทิศอ้างอิง = หน้าผาของไทล์ที่เกาะอยู่ (ไทล์แต่ละแบบหน้าผาเอียงไม่เท่ากัน) เฟรมแรกถ้ายังไม่เจอไทล์ใช้ทิศตัวไปก่อน
        if (wall != null)
        {
            currentWall = wall;
            wallFacing = Flat(wall.Facing, wallFacing);
        }
        else if (firstFrame) wallFacing = facing;

        // ทิศที่ควรหัน = สวนทาง normal (แนวนอน) แต่ไม่เกิน maxSurfaceAngle จากทิศกำแพง
        Vector3 surfaceFacing = Flat(-normal, facing);
        float angle = Mathf.Clamp(Vector3.SignedAngle(wallFacing, surfaceFacing, Vector3.up), -maxSurfaceAngle, maxSurfaceAngle);
        surfaceFacing = Quaternion.AngleAxis(angle, Vector3.up) * wallFacing;

        float turnT = 1f - Mathf.Exp(-surfaceTurnSharpness * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(Vector3.Slerp(facing, surfaceFacing, turnT), Vector3.up);

        Vector3 target = point - surfaceFacing * surfaceOffset;
        target.y = transform.position.y;
        float moveT = 1f - Mathf.Exp(-surfaceFollowSharpness * Time.deltaTime);
        Vector3 newPos = Vector3.Lerp(transform.position, target, moveT);
        if (currentWall != null && currentWall.bounds != null) newPos = currentWall.ClampPosition(newPos);
        else newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        transform.position = newPos;
    }

    // ยิง 5 เส้น (กลาง + ซ้าย/ขวา/บน/ล่าง) ไปตามทิศ dir คืนจุดเฉลี่ยและ normal เฉลี่ยของผิวหินที่โดน
    private bool SampleSurface(Vector3 dir, out Vector3 point, out Vector3 normal, out ClimbableWall wall)
    {
        point = Vector3.zero; normal = Vector3.zero; wall = null;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        Vector3 basePos = transform.position - dir * surfaceProbeBack;
        float dist = surfaceProbeBack + surfaceProbeDistance;
        int hits = 0;
        for (int i = 0; i < 5; i++)
        {
            Vector3 offset = i == 0 ? Vector3.zero
                : i == 1 ? -right * surfaceSampleSpread.x
                : i == 2 ? right * surfaceSampleSpread.x
                : i == 3 ? Vector3.up * surfaceSampleSpread.y
                : Vector3.down * surfaceSampleSpread.y;
            if (!ClimbableWall.Raycast(basePos + offset, dir, dist, out RaycastHit hit)) continue;
            // จุดเทียบกลับมาที่แนวกลางตัว (ตัด offset ออก) ให้เฉลี่ยแล้วได้ระยะลึกของจุดที่ตัวอยู่
            point += hit.point - offset;
            normal += hit.normal;
            if (wall == null) wall = hit.collider.GetComponentInParent<ClimbableWall>();
            hits++;
        }
        if (hits == 0) return false;
        point /= hits;
        normal.Normalize();
        return true;
    }

    private static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.04f ? v.normalized : fallback;
    }
}