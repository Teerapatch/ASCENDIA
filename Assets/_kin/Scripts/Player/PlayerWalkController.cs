using UnityEngine;
using UnityEngine.InputSystem; // 🌟 1. เรียกใช้งาน NameSpace ของระบบ Input ใหม่

[RequireComponent(typeof(PlayerStateManager), typeof(PlayerCameraController))]
public class PlayerWalkController : MonoBehaviour
{
    [Header("Settings")]
    public float walkSpeed = 1.4f;
    public float runSpeed = 3.5f;
    public float wallDetectDistance = 0.8f;

    [Header("Input Configuration")]
    public InputActionReference moveAction; // 🌟 2. สร้างช่องรับค่า Action จาก Inspector

    private float reattachAt;
    public void DelayWallAttachment(float seconds) { reattachAt = Time.time + seconds; }
    private PlayerStateManager stateManager;
    private PlayerCameraController camController;

    private void Awake()
    {
        stateManager = GetComponent<PlayerStateManager>();
        camController = GetComponent<PlayerCameraController>();
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
        // 🌟 4. อ่านค่าแบบ Vector2 (รองรับทั้งปุ่ม WASD, ลูกศร และก้าน Analog ของจอยสติ๊กพร้อมกัน)
        Vector2 inputVal = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        Vector3 movement = Vector3.ClampMagnitude(new Vector3(inputVal.x, 0, inputVal.y), 1f);

        if (movement.magnitude > 0.1f)
        {
            bool running = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            transform.Translate(movement * (running ? runSpeed : walkSpeed) * Time.deltaTime, Space.World);
            transform.forward = Vector3.Slerp(transform.forward, movement, Time.deltaTime * 10f);
        }

        // เซนเซอร์ชนกำแพง
        if (Time.time >= reattachAt && movement.sqrMagnitude > .02f && Physics.Raycast(transform.position, movement.normalized, out RaycastHit hit, wallDetectDistance))
        {
            if (hit.collider.CompareTag("Wall"))
            {
                StartClimbing(hit);
            }
        }
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
