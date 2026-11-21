using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerStateManager))]
public class PlayerClimbController : MonoBehaviour
{
    [Header("Settings")]
    public float verticalSpeed = 5f;
    public float horizontalSpeed = 3f;
    public float minX = -4f;
    public float maxX = 4f;
    [Range(.1f, 1f), Tooltip("ตัวคูณความเร็วเฉพาะตอนปีนขึ้น (ลงและซ้าย-ขวาไม่เกี่ยว)")]
    public float upwardSpeedMultiplier = .3f;
    [Range(.1f, 1f), Tooltip("ตัวคูณความเร็วตอนปีนลง ให้ท่าปีนทันระยะที่เคลื่อนที่")]
    public float downwardSpeedMultiplier = .4f;
    [Min(.1f), Tooltip("ความเร่งตอนเริ่มขยับหรือเปลี่ยนทิศ (ปล่อยปุ่มแล้วหยุดทันที)")]
    public float climbAcceleration = 10f;
    [Min(.1f), Tooltip("ความหน่วงตอนปล่อยปุ่ม (มาก = หยุดเร็ว) ตัวจะผ่อนหยุดแทนการหยุดกึก")]
    public float climbDeceleration = 25f;

    [Header("กระโดดเร่งตอนปีน (Spacebar)")]
    [Tooltip("ระยะที่พุ่งไปตามผิวหิน (หน่วยอาร์ต) ไปทางที่กดอยู่ ไม่กดทิศ = ขึ้นบน")]
    public float jumpDistance = 5f;
    public float jumpDuration = .35f;
    public float jumpStaminaCost = 12f;
    [Tooltip("พุ่งตัวหนึ่งครั้งลด Stamina สูงสุดทันทีเท่าไร (คืนได้ด้วยสมุนไพร/การพัก)")]
    public float jumpMaxStaminaCost = 5f;
    public float jumpCooldown = .45f;

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
    [Tooltip("ความไวในการตามทิศผิวหิน (น้อย = นุ่ม ไม่กระตุกตามเหลี่ยมของหินโลว์โพลี)")]
    public float surfaceNormalSharpness = 12f;

    // ทิศของหน้าผา (แกน Z ของกล่องขอบเขตใน ClimbableWall) ใช้เป็นแกนอ้างอิงของมุมตัวและสำรองเมื่อยิงไม่โดนผิว
    private Vector3 wallFacing;
    // ไทล์ที่กำลังเกาะอยู่ (ขอบซ้าย-ขวามาจาก BoxCollider ของไทล์นี้) ถ้าไม่มีใช้ minX..maxX
    private ClimbableWall currentWall;

    private PlayerStateManager stateManager;
    private ClimbingContactIK contactIK;
    private PlayerLedgeMantle ledgeMantle;
    // x = ซ้าย-ขวาตามผิวหิน, y = ขึ้น-ลง
    private Vector2 climbVelocity;
    // ทิศตั้งฉากของผิวหินที่เกลี่ยแล้ว ใช้ทั้งแนวเคลื่อนที่และระยะเกาะ
    private Vector3 smoothedNormal;
    private bool hasSurface;
    private Vector2 jumpDirection;
    private float jumpTime = -1f, nextJumpAt;
    
    // 🌟 ตัวแปรเก็บค่าตัวคูณความเร็วแบบเรียลไทม์
    private float currentSpeedMultiplier = 1f;

    private void Awake()
    {
        stateManager = GetComponent<PlayerStateManager>();
        contactIK = GetComponentInChildren<ClimbingContactIK>();
        ledgeMantle = GetComponent<PlayerLedgeMantle>();
    }

    private void Update()
    {
        // ตอน AutoClimbing ความเร็วของท่าปีนมาจาก NodeSelectionManager
        if (contactIK != null && stateManager.currentState != PlayerStateManager.State.AutoClimbing) contactIK.SetClimbVelocity(Vector3.zero);
        if (GameManager.Instance == null) { climbVelocity = Vector2.zero; return; }
        
        // 🌟 1. อัปเดตตัวคูณความเร็วตลอดเวลา (Real-time Update) 
        // ไม่ว่าผู้เล่นจะทำอะไรอยู่ ถ้าน้ำหนักเปลี่ยน ความเร็วจะคำนวณรอไว้เลย
        UpdateSpeedMultiplier();

        if (stateManager.currentState != PlayerStateManager.State.Climbing)
        {
            climbVelocity = Vector2.zero;
            wallFacing = Vector3.zero; // เริ่มปีนรอบใหม่ค่อยจำทิศกำแพงใหม่
            currentWall = null;
            hasSurface = false;
            if (jumpTime >= 0f && contactIK != null) contactIK.SetJump(Vector3.zero, -1f);
            jumpTime = -1f;
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

    // รับทั้ง Move Action (รวมจอย) และ WASD/ลูกศรผ่าน Input System
    public Vector2 ReadClimbInput()
    {
        if (PlayerStateManager.InputLocked) return Vector2.zero;
        PlayerWalkController walk = GetComponent<PlayerWalkController>();
        Vector2 input = walk != null && walk.moveAction != null ? walk.moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            int x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            int y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            if (x != 0 || y != 0) input = new Vector2(x, y);
        }
        if (Mathf.Abs(input.x) < 0.1f) input.x = 0f;
        if (Mathf.Abs(input.y) < 0.1f) input.y = 0f;
        return input;
    }

    private void HandleClimbing()
    {
        Vector2 input = ReadClimbInput();
        float h = input.x;
        float v = input.y;
        float dt = Time.deltaTime;

        // 🌟 2. ดึงความเร็วปัจจุบันมาใช้ (คูณกับตัวคูณแบบเรียลไทม์)
        float currentVertSpeed = verticalSpeed * currentSpeedMultiplier * (v > 0f ? upwardSpeedMultiplier : downwardSpeedMultiplier);
        float currentHorzSpeed = horizontalSpeed * currentSpeedMultiplier;

        // เร่งตอนเริ่มขยับ และผ่อนตอนปล่อยปุ่ม ตัวจึงไม่หยุดกึก
        climbVelocity.x = Mathf.MoveTowards(climbVelocity.x, h * currentHorzSpeed, (h == 0f ? climbDeceleration : climbAcceleration) * dt);
        climbVelocity.y = Mathf.MoveTowards(climbVelocity.y, v * currentVertSpeed, (v == 0f ? climbDeceleration : climbAcceleration) * dt);

        // แนวเคลื่อนที่อยู่บนระนาบของผิวหินจริง: ขึ้น-ลงไต่ตามความลาด ซ้าย-ขวาเลื่อนตามผิวโค้ง
        Vector3 facing = Flat(transform.forward, Vector3.forward);
        Vector3 upAlong = Vector3.up;
        Vector3 lateral = followSurface ? Vector3.Cross(Vector3.up, facing) : Vector3.right;
        if (followSurface && hasSurface)
        {
            Vector3 projectedUp = Vector3.ProjectOnPlane(Vector3.up, smoothedNormal);
            if (projectedUp.sqrMagnitude > 0.09f) upAlong = projectedUp.normalized;
            Vector3 projectedSide = Vector3.ProjectOnPlane(lateral, smoothedNormal);
            if (projectedSide.sqrMagnitude > 0.09f) lateral = projectedSide.normalized;
        }

        // กระโดดเร่ง: กด Spacebar พุ่งไปตามผิวหินระยะหนึ่ง แลกกับ Stamina และความเหนื่อย
        PlayerData playerData = GameManager.Instance.playerData;
        if (jumpTime < 0f && Time.time >= nextJumpAt && !GameExitMenu.IsOpen && !PlayerStateManager.InputLocked && playerData != null
            && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && playerData.stamina > jumpStaminaCost)
        {
            playerData.stamina -= jumpStaminaCost;
            playerData.AddFatigue(jumpMaxStaminaCost);
            jumpDirection = input.sqrMagnitude > .01f ? input.normalized : Vector2.up;
            jumpTime = 0f;
            // ฝุ่นจากผิวหินตรงที่ถีบตัว พุ่งสวนทางที่กระโดด
            Vector3 leap = lateral * jumpDirection.x + upAlong * jumpDirection.y;
            SoftGlow.Puff(transform.position - leap * 1.5f + facing * (surfaceOffset * .4f), -leap - facing * .4f, new Color(.93f, .95f, 1f), 26);
            SoftGlow.Trail(transform, new Color(.8f, .9f, 1f), jumpDuration);
            nextJumpAt = Time.time + jumpDuration + jumpCooldown;
        }
        Vector2 jumpDelta = Vector2.zero;
        bool jumping = jumpTime >= 0f;
        if (jumping)
        {
            // พุ่งแรงตอนต้นแล้วผ่อน (ease-out)
            float before = jumpTime / jumpDuration;
            jumpTime += dt;
            float after = Mathf.Min(1f, jumpTime / jumpDuration);
            jumpDelta = jumpDirection * (jumpDistance * ((1f - (1f - after) * (1f - after)) - (1f - (1f - before) * (1f - before))));
            if (contactIK != null) contactIK.SetJump(lateral * jumpDirection.x + upAlong * jumpDirection.y, after);
            if (after >= 1f)
            {
                jumpTime = -1f;
                if (contactIK != null) contactIK.SetJump(Vector3.zero, -1f);
                // มือกลับมาเกาะหิน: ฝุ่นเล็ก ๆ ตรงจุดที่คว้า
                SoftGlow.Puff(transform.position + upAlong * 1.2f + facing * (surfaceOffset * .6f), -facing, new Color(.93f, .95f, 1f), 10);
            }
        }

        Vector3 lateralStep = ClampStepToBounds(transform.position, lateral * (climbVelocity.x * dt + jumpDelta.x));

        // --- ปีนขึ้น/ลง: หน้าผา 1 ชิ้นต่อชั้นอยู่กับที่ ตัวผู้เล่นเคลื่อนที่ระหว่าง MinClimbY..MaxClimbY ---
        // จังหวะดึงตัวตามท่าปีน: เร็วตอนดึง ช้าตอนเอื้อมมือ (ระยะรวมต่อรอบเท่าเดิม)
        float rhythm = contactIK != null ? contactIK.PullFactor : 1f;
        float verticalAmount = climbVelocity.y * dt * rhythm + jumpDelta.y;
        Vector3 verticalStep = upAlong * (climbVelocity.y * dt + jumpDelta.y);
        float limit = 1f;
        if (currentWall != null && Mathf.Abs(verticalAmount * upAlong.y) > 0.00001f)
        {
            // เทียบกับตำแหน่งเดิมด้วย: ถ้าเริ่มต่ำกว่า climbStart ต้องปีนขึ้นได้ ไม่ถูกดึงกลับ
            float currentY = transform.position.y;
            float nextY = currentY + verticalAmount * upAlong.y;
            float clampedY = verticalAmount > 0f
                ? Mathf.Min(nextY, Mathf.Max(currentY, currentWall.MaxClimbY))
                : Mathf.Max(nextY, Mathf.Min(currentY, currentWall.MinClimbY));
            limit = Mathf.Clamp01((clampedY - currentY) / (nextY - currentY));
        }
        Vector3 climbStep = lateralStep + upAlong * (verticalAmount * limit);

        // ท่าปีนเดินตามความเร็วที่ขอ (ไม่รวมจังหวะดึง) ชนขอบแล้วท่าจะหยุดด้วย
        // ถ้า IK หาหินข้างหน้าไม่เจอ (ผิวหินเว้าลึก) ยังขยับต่อได้ ไม่งั้นผู้เล่นจะค้าง
        bool canMove = true;
        if (contactIK != null)
        {
            contactIK.SetClimbVelocity((lateralStep + verticalStep * limit) / Mathf.Max(dt, 0.0001f));
            canMove = jumping || !contactIK.HasWall || contactIK.MovementMultiplier > 0f;
        }
        if (canMove) transform.position += climbStep;

        if (followSurface) StickToSurface();

        // ปีนขึ้นไปเจอพื้นเรียบ: ขึ้นไปยืน
        if (verticalAmount > 0f && ledgeMantle != null && ledgeMantle.TryStartMantle(Flat(transform.forward, Vector3.forward), currentWall))
        {
            climbVelocity = Vector2.zero;
        }
        // ปีนลงมาถึงพื้นเรียบ: กลับไปยืน
        else if (verticalAmount < 0f && ledgeMantle != null && ledgeMantle.TryStandDown(Flat(transform.forward, Vector3.forward), currentWall))
        {
            climbVelocity = Vector2.zero;
        }
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

    // เกาะผิวหิน: ยิงหลายเส้นรอบตัวเข้าหาผิว (ตามแนวตั้งฉากของผิวล่าสุด) เฉลี่ยจุด/ทิศของผิว
    // แล้วรักษาระยะ surfaceOffset ตามแนวตั้งฉากจริง ตัวจึงแนบทั้งหินที่ยื่นและหินที่ลาดเอน
    private void StickToSurface()
    {
        Vector3 facing = Flat(transform.forward, Vector3.forward);
        bool firstFrame = wallFacing == Vector3.zero;
        if (firstFrame) wallFacing = facing;

        Vector3 probe = hasSurface && Mathf.Abs(smoothedNormal.y) < 0.75f ? -smoothedNormal : facing;
        if (!SampleSurface(probe, out Vector3 point, out Vector3 normal, out ClimbableWall wall)
            && !SampleSurface(facing, out point, out normal, out wall)
            && !SampleSurface(wallFacing, out point, out normal, out wall))
        {
            return; // ช่องว่างระหว่างหิน: คงตำแหน่ง/ทิศเดิมไว้
        }
        // ทิศอ้างอิง = หน้าผาของไทล์ที่เกาะอยู่ (ไทล์แต่ละแบบหน้าผาเอียงไม่เท่ากัน)
        if (wall != null)
        {
            currentWall = wall;
            wallFacing = Flat(wall.Facing, wallFacing);
        }
        else if (firstFrame) wallFacing = facing;

        // เกลี่ยทิศผิว: หินโลว์โพลีเปลี่ยนเหลี่ยมทีละหน้า ถ้าใช้ตรง ๆ ตัวจะกระตุก
        float dt = Time.deltaTime;
        smoothedNormal = hasSurface
            ? Vector3.Slerp(smoothedNormal, normal, 1f - Mathf.Exp(-surfaceNormalSharpness * dt)).normalized
            : normal;
        hasSurface = true;
        if (contactIK != null) contactIK.SetSurfaceNormal(smoothedNormal);

        // ทิศที่ควรหัน = สวนทาง normal (แนวนอน) แต่ไม่เกิน maxSurfaceAngle จากทิศกำแพง
        Vector3 surfaceFacing = Flat(-smoothedNormal, facing);
        float angle = Mathf.Clamp(Vector3.SignedAngle(wallFacing, surfaceFacing, Vector3.up), -maxSurfaceAngle, maxSurfaceAngle);
        surfaceFacing = Quaternion.AngleAxis(angle, Vector3.up) * wallFacing;

        float turnT = 1f - Mathf.Exp(-surfaceTurnSharpness * dt);
        transform.rotation = Quaternion.LookRotation(Vector3.Slerp(facing, surfaceFacing, turnT), Vector3.up);

        // ดันเข้า-ออกตามแนวตั้งฉากของผิว ให้ห่างผิว surfaceOffset พอดี
        float previousY = transform.position.y;
        float distance = Vector3.Dot(transform.position - point, smoothedNormal);
        float moveT = 1f - Mathf.Exp(-surfaceFollowSharpness * dt);
        Vector3 newPos = transform.position + smoothedNormal * ((surfaceOffset - distance) * moveT);
        if (currentWall != null)
        {
            newPos.y = Mathf.Clamp(newPos.y, Mathf.Min(previousY, currentWall.MinClimbY), Mathf.Max(previousY, currentWall.MaxClimbY));
        }
        if (currentWall != null && currentWall.bounds != null) newPos = currentWall.ClampPosition(newPos);
        else newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        transform.position = newPos;
    }

    // ยิง 5 เส้น (กลาง + ซ้าย/ขวา/บน/ล่าง) ไปตามทิศ dir คืนจุดเฉลี่ยและ normal เฉลี่ย
    private bool SampleSurface(Vector3 dir, out Vector3 point, out Vector3 normal, out ClimbableWall wall)
    {
        point = Vector3.zero; normal = Vector3.zero; wall = null;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        if (right.sqrMagnitude < 0.0001f) return false;
        right.Normalize();
        Vector3 up = Vector3.Cross(dir, right);
        Vector3 basePos = transform.position - dir * surfaceProbeBack;
        float dist = surfaceProbeBack + surfaceProbeDistance;
        int hits = 0;
        for (int i = 0; i < 5; i++)
        {
            Vector3 offset = i == 0 ? Vector3.zero
                : i == 1 ? -right * surfaceSampleSpread.x
                : i == 2 ? right * surfaceSampleSpread.x
                : i == 3 ? up * surfaceSampleSpread.y
                : -up * surfaceSampleSpread.y;
            if (!ClimbableWall.Raycast(basePos + offset, dir, dist, out RaycastHit hit)) continue;
            // จุดเทียบกลับมาที่แนวกลางตัว (ตัด offset ออก) ให้เฉลี่ยแล้วได้ระยะลึกของผิวตรงกลางตัว
            point += hit.point - offset;
            normal += hit.normal;
            if (wall == null) wall = hit.collider.GetComponentInParent<ClimbableWall>();
            hits++;
        }
        if (hits == 0 || normal.sqrMagnitude < 0.0001f) return false;
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