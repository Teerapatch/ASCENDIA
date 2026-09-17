using UnityEngine;

[RequireComponent(typeof(PlayerStateManager), typeof(PlayerCameraController))]
public class PlayerWalkController : MonoBehaviour
{
    [Header("Settings")]
    public float walkSpeed = 5f;
    public float wallDetectDistance = 0.8f;

    private PlayerStateManager stateManager;
    private PlayerCameraController camController;

    private void Awake()
    {
        stateManager = GetComponent<PlayerStateManager>();
        camController = GetComponent<PlayerCameraController>();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (stateManager.currentState != PlayerStateManager.State.Walking) return;

        HandleWalking();
    }

    private void HandleWalking()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 movement = new Vector3(h, 0, v).normalized;

        if (movement.magnitude > 0.1f)
        {
            transform.Translate(movement * walkSpeed * Time.deltaTime, Space.World);
            transform.forward = Vector3.Slerp(transform.forward, movement, Time.deltaTime * 10f);
        }

        // เซนเซอร์ชนกำแพง
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, wallDetectDistance))
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
        
        // 2. ดึงตัวติดกำแพง
        transform.forward = -hit.normal;
        float capsuleRadius = 0.6f;
        Vector3 stickPos = hit.point + (hit.normal * capsuleRadius);
        transform.position = new Vector3(stickPos.x, transform.position.y, stickPos.z);

        // 3. สั่งตากล้องให้เปลี่ยนมุม
        camController.SwitchToClimbCam(transform.eulerAngles.y);
    }
}