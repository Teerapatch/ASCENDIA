using UnityEngine;
using Unity.Cinemachine;

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

    // ฟังก์ชันสั่งสั่น (force: แรงสั่น, duration: ความนาน)
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