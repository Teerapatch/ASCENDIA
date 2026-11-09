using UnityEngine;
using Unity.Cinemachine;

// จุดเดียวสำหรับสั่งสั่นกล้องในฉากต่อสู้ (BattleCameraController แค่ขยับกล้อง)
// แรงหลายอันในเฟรมเดียวจะรวมเป็นอันที่แรงสุด เพื่อไม่ให้สั่นซ้อนจนมั่ว
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CameraShakeManager : MonoBehaviour
{
    public static CameraShakeManager Instance;

    [Tooltip("ตัวคูณแรงสั่นรวมทั้งเกม")]
    public float strengthMultiplier = 2.5f;

    private CinemachineImpulseSource impulseSource;
    private float pendingForce;
    private Vector3 pendingDirection;
    private bool capturing;
    private float capturedForce;

    private void Awake()
    {
        Instance = this;
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // เรียกได้จากทุกที่โดยไม่ต้องเช็ก null เอง
    public static void TryShake(float force = 1f)
    {
        if (Instance != null) Instance.Shake(force);
    }

    // สั่นแบบมีทิศ (ทิศบนจอ) ใช้กับจังหวะโดน ให้กล้องกระแทกไปทางเดียวกับแรงตี
    public static void Impact(float force, Vector2 screenDirection)
    {
        if (Instance != null) Instance.Queue(force, new Vector3(screenDirection.x, screenDirection.y, 0f));
    }

    // ระหว่าง Capture แรงสั่นจะถูกจำไว้ ไม่สั่นจริง สกิลจึงบอกความแรงได้ แต่กล้องสั่นตรงจังหวะที่ท่าโดน
    public static void BeginCapture()
    {
        if (Instance == null) return;
        Instance.capturing = true;
        Instance.capturedForce = 0f;
    }

    public static float EndCapture()
    {
        if (Instance == null) return 0f;
        Instance.capturing = false;
        return Instance.capturedForce;
    }

    public void Shake(float force = 1f)
    {
        if (capturing)
        {
            capturedForce = Mathf.Max(capturedForce, force);
            return;
        }
        Queue(force, Random.onUnitSphere);
    }

    private void Queue(float force, Vector3 direction)
    {
        if (force <= pendingForce) return;
        pendingForce = force;
        pendingDirection = direction;
    }

    private void LateUpdate()
    {
        if (pendingForce <= 0f) return;
        if (impulseSource != null) impulseSource.GenerateImpulseWithVelocity(pendingDirection.normalized * (pendingForce * strengthMultiplier));
        pendingForce = 0f;
    }
}
