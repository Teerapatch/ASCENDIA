using UnityEngine;
using Unity.Cinemachine;

// จุดเดียวสำหรับสั่งสั่นกล้องในฉากต่อสู้ (BattleCameraController ไม่มีระบบสั่นของตัวเองแล้ว)
[RequireComponent(typeof(CinemachineImpulseSource))] // บังคับให้ Unity ใส่ Component นี้ให้อัตโนมัติ
public class CameraShakeManager : MonoBehaviour
{
    public static CameraShakeManager Instance;
    private CinemachineImpulseSource impulseSource;

    private void Awake()
    {
        Instance = this;
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // เรียกได้จากทุกที่โดยไม่ต้องเช็ก null เอง (ถ้าไม่มี Manager ในฉากก็แค่ไม่สั่น)
    public static void TryShake(float force = 1f)
    {
        if (Instance != null) Instance.Shake(force);
    }

    // ฟังก์ชันสั่งสั่น (force: แรงสั่น)
    public void Shake(float force = 1f)
    {
        if (impulseSource != null)
        {
            // สร้างแรงสั่นสะเทือนแบบสุ่มทิศทาง 3 มิติ
            Vector3 randomVelocity = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized;

            impulseSource.GenerateImpulseWithVelocity(randomVelocity * force);
        }
    }
}
