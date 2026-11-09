using UnityEngine;
using UnityEngine.InputSystem; // 🌟 1. เรียกใช้งาน NameSpace ของระบบ Input ใหม่

[RequireComponent(typeof(PlayerStateManager), typeof(PlayerCameraController))]
public class PlayerWalkController : MonoBehaviour
{
    [Header("Settings")]
    public float walkSpeed = 1.4f;
    public float runSpeed = 3.5f;
    public float wallDetectDistance = 0.8f;

    [Header("เดินบนพื้นเรียบของหน้าผา (หน่วยอาร์ต)")]
    [Tooltip("เดินตามความสูงของหิน และหยุดที่ขอบพื้น")]
    public bool followGround = true;
    [Range(.5f, 1f), Tooltip("ผิวที่ normal.y ถึงค่านี้เดินได้")]
    public float walkableNormalY = .75f;
    [Range(0f, 1f), Tooltip("ผิวที่ normal.y ต่ำกว่าค่านี้นับเป็นผนังให้ปีน")]
    public float wallMaxNormalY = .6f;
    public float maxStepHeight = 1.6f;
    public float maxStepDown = 3f;

    [Header("Input Configuration")]
    public InputActionReference moveAction; // 🌟 2. สร้างช่องรับค่า Action จาก Inspector

    private float reattachAt;
    public void DelayWallAttachment(float seconds) { reattachAt = Time.time + seconds; }
    private PlayerStateManager stateManager;
    private PlayerCameraController camController;
    private PlayerClimbController climbController;
    private PlayerLedgeMantle ledgeMantle;
    private CharacterController capsule;

    private void Awake()
    {
        stateManager = GetComponent<PlayerStateManager>();
        camController = GetComponent<PlayerCameraController>();
        climbController = GetComponent<PlayerClimbController>();
        ledgeMantle = GetComponent<PlayerLedgeMantle>();
        capsule = GetComponent<CharacterController>();
    }

    // 🌟 3. ต้องสั่งเปิด/ปิด การรับค่า Input เสมอเพื่อป้องกัน Memory Leak
    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (stateManager.currentState != PlayerStateManager.State.Walking) return;

        HandleWalking();
    }

    private void HandleWalking()
    {
        // 🌟 4. รับทั้ง Move Action และ WASD/ลูกศร ทิศเดินยึดตามมุมกล้อง
        Vector2 inputVal = climbController != null ? climbController.ReadClimbInput()
            : (moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero);
        Vector3 movement = Vector3.ClampMagnitude(CameraRelative(inputVal), 1f);
        if (movement.sqrMagnitude < 0.01f) return;

        // เซนเซอร์ชนกำแพง: เดินเข้าหน้าผาที่ชันแล้วเริ่มปีน
        if (Time.time >= reattachAt && ClimbableWall.Raycast(transform.position, movement.normalized, wallDetectDistance, out RaycastHit hit)
            && hit.normal.y < wallMaxNormalY)
        {
            StartClimbing(hit);
            return;
        }

        bool running = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        Vector3 next = transform.position + movement * (running ? runSpeed : walkSpeed) * Time.deltaTime;

        if (followGround)
        {
            ClimbableWall wall = MapGenerator.CurrentCliff;
            if (wall != null) next = wall.ClampPosition(next);

            float half = capsule != null ? capsule.height * .5f * Mathf.Abs(transform.lossyScale.y) : 1f;
            if (!FindGround(next, half, out float groundY))
            {
                // สุดขอบพื้น: ถ้าเดินออกหน้าผาให้ปีนลง ไม่อย่างนั้นหยุดอยู่ตรงขอบ
                if (ledgeMantle != null) ledgeMantle.TryClimbDown(movement);
                transform.forward = Vector3.Slerp(transform.forward, movement, Time.deltaTime * 10f);
                return;
            }
            if (groundY + half - transform.position.y > maxStepHeight) return;
            next.y = groundY + half;
        }

        transform.position = next;
        transform.forward = Vector3.Slerp(transform.forward, movement, Time.deltaTime * 10f);
    }

    private Vector3 CameraRelative(Vector2 input)
    {
        Camera cam = Camera.main;
        if (cam == null) return new Vector3(input.x, 0f, input.y);
        Vector3 forward = cam.transform.forward;
        Vector3 right = cam.transform.right;
        forward.y = 0f;
        right.y = 0f;
        return forward.normalized * input.y + right.normalized * input.x;
    }

    // หาพื้นใต้จุดที่จะก้าวไป (เฉพาะผิวที่เรียบพอ) ยิงจากจุดกลางตัวเพื่อไม่ชนหินที่อยู่เหนือหัว
    private bool FindGround(Vector3 at, float half, out float groundY)
    {
        groundY = 0f;
        if (!ClimbableWall.Raycast(at + Vector3.up * .5f, Vector3.down, .5f + half + maxStepDown, out RaycastHit hit) || hit.normal.y < walkableNormalY) return false;
        groundY = hit.point.y;
        return true;
    }

    private void StartClimbing(RaycastHit hit)
    {
        // 1. เปลี่ยนสถานะ
        stateManager.ChangeState(PlayerStateManager.State.Climbing);
        
        // 2. ดึงตัวติดกำแพง: หินอาร์ต (MeshCollider) ขรุขระ ใช้ทิศของหน้าผา (กล่องขอบเขตของไทล์) แทน normal ของจุดที่ชน
        ClimbableWall wall = hit.collider.GetComponentInParent<ClimbableWall>();
        Vector3 facing = wall != null ? wall.Facing : -hit.normal;
        facing.y = 0f;
        transform.forward = facing.normalized;
        float capsuleRadius = 0.6f;
        Vector3 stickPos = hit.point - (transform.forward * capsuleRadius);
        transform.position = new Vector3(stickPos.x, transform.position.y, stickPos.z);

        // 3. สั่งตากล้องให้เปลี่ยนมุม: กล้องใช้ทิศของไทล์ (มุมฉากอาร์ต) ส่วนตัวผู้เล่นหันเข้าหน้าผาที่อาจเอียงจากกล้อง
        float viewYaw = wall != null ? Quaternion.LookRotation(wall.ViewFacing).eulerAngles.y : transform.eulerAngles.y;
        camController.SwitchToClimbCam(viewYaw);
    }
}
