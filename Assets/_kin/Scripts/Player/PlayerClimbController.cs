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

    [Header("References")]
    public Transform environmentContainer; // ใส่ฉากที่เลื่อน

    private PlayerStateManager stateManager;

    private void Awake()
    {
        stateManager = GetComponent<PlayerStateManager>();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (stateManager.currentState != PlayerStateManager.State.Climbing) return;

        HandleClimbing();
    }

    private void HandleClimbing()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // ขยับซ้ายขวา
        Vector3 newPos = transform.position + new Vector3(h, 0, 0) * horizontalSpeed * Time.deltaTime;
        newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        transform.position = newPos;

        // ปีนขึ้น (ดึงคน หรือ ดึงฉาก)
        if (v > 0.1f)
        {
            if (transform.position.y < maxPlayerHeight)
            {
                transform.Translate(Vector3.up * verticalSpeed * Time.deltaTime, Space.World);
            }
            else
            {
                if (environmentContainer != null)
                    environmentContainer.Translate(Vector3.down * verticalSpeed * Time.deltaTime);
            }
        }
        // ปีนลง (ดึงฉากขึ้น)
        else if (v < -0.1f)
        {
            if (environmentContainer != null)
                environmentContainer.Translate(Vector3.up * verticalSpeed * Time.deltaTime);
        }
    }
}