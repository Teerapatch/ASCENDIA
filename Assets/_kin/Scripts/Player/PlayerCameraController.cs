using UnityEngine;

public class PlayerCameraController : MonoBehaviour
{
    [Header("Cameras")]
    public GameObject walkCam;
    public GameObject climbCam;
    public Transform lookTarget;
    
    [Header("Settings")]
    public float mouseSensitivity = 3f;

    private float camPan, camTilt;
    private float lockedPan, lockedTilt;
    private Vector3 lookTargetOffset;

    // เพิ่มตัวแปรสำหรับจำค่าเริ่มต้นของกล้องตอนเดิน
    private float initialWalkPan;
    private float initialWalkTilt;

    private void Start()
    {
        if (lookTarget != null)
        {
            lookTargetOffset = lookTarget.localPosition;
            
            // อ่านค่าแกน X แล้วแปลงค่า 0-360 ให้เป็น -180 ถึง 180 ให้ถูกต้อง
            float startTilt = lookTarget.eulerAngles.x;
            if (startTilt > 180f) startTilt -= 360f;
            
            // จำค่าที่คุณตั้งไว้ใน Inspector
            initialWalkPan = lookTarget.eulerAngles.y;
            initialWalkTilt = startTilt;

            lockedPan = initialWalkPan;
            lockedTilt = initialWalkTilt;
            
            camPan = lockedPan;
            camTilt = lockedTilt;

            lookTarget.SetParent(null);
        }
        
        SwitchToWalkCam();
    }

    private void Update()
    {
        if (lookTarget == null) return;

        lookTarget.position = transform.position + lookTargetOffset;

        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            camPan += mouseX;
            camTilt -= mouseY;
            camTilt = Mathf.Clamp(camTilt, -30f, 70f);

            lookTarget.rotation = Quaternion.Euler(camTilt, camPan, 0f);
        }
        else
        {
            // ลดความเร็วจาก 10f เป็น 3f เพื่อให้กล้องค่อยๆ สวิงกลับแบบนุ่มๆ
            camPan = Mathf.LerpAngle(camPan, lockedPan, Time.deltaTime * 3f);
            camTilt = Mathf.LerpAngle(camTilt, lockedTilt, Time.deltaTime * 3f);
            lookTarget.rotation = Quaternion.Euler(camTilt, camPan, 0f);
        }
    }

    public void SwitchToClimbCam(float wallNormalAngleY)
    {
        lockedPan = wallNormalAngleY;
        lockedTilt = 0f; // ตอนปีนให้มองตรงไปที่กำแพง

        walkCam.SetActive(false);
        climbCam.SetActive(true);
    }

    public void SwitchToWalkCam()
    {
        // ใช้ค่าตั้งต้นที่คุณจัดไว้ใน Scene แทนการใช้ 0f
        lockedPan = initialWalkPan;
        lockedTilt = initialWalkTilt;
        
        climbCam.SetActive(false);
        walkCam.SetActive(true);
    }
}