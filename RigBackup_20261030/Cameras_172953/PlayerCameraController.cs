using UnityEngine;

public class PlayerCameraController : MonoBehaviour
{
    [Header("Cameras")]
    public GameObject walkCam;
    public GameObject climbCam;
    public Transform lookTarget;
    
    [Header("Camera Sway Settings (ใช้ทั้งตอนเดินและตอนปีน)")]
    public float cameraSwayAmount = 5f;   // องศาที่กล้องเอียงตามเมาส์ (ค่า 5 เท่ากับหน้าเลือก Node)
    public float cameraSwaySpeed = 5f;    // ความสมูทในการดึงกล้อง
    public float mouseSensitivity = 3f;   // ความไวเมาส์ (ใช้คำนวณตอนเมาส์ถูกล็อค)
    public bool requireRightClick = false; // ติ๊กถูกถ้าอยากให้ต้องคลิกขวาค้างถึงจะหันมองได้

    private float lockedPan, lockedTilt;
    private Vector3 lookTargetOffset;

    // จำค่าเริ่มต้นของกล้องตอนเดิน
    private float initialWalkPan;
    private float initialWalkTilt;

    // เก็บพิกัดเมาส์จำลอง (รองรับกรณีเมาส์ถูกซ่อน/ล็อคกลางจอ)
    private Vector2 virtualMousePos = Vector2.zero;

    private void Start()
    {
        if (lookTarget != null)
        {
            lookTargetOffset = lookTarget.localPosition;
            
            float startTilt = lookTarget.eulerAngles.x;
            if (startTilt > 180f) startTilt -= 360f;
            
            initialWalkPan = lookTarget.eulerAngles.y;
            initialWalkTilt = startTilt;

            lockedPan = initialWalkPan;
            lockedTilt = initialWalkTilt;

            lookTarget.SetParent(null);
        }
        
        SwitchToWalkCam();
    }

    private void Update()
    {
        if (lookTarget == null) return;

        // ให้จุดโฟกัสตามตำแหน่งผู้เล่นเสมอ
        lookTarget.position = transform.position + lookTargetOffset;

        bool canSway = !requireRightClick || Input.GetMouseButton(1);

        if (canSway)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                // กรณีเมาส์ถูกล็อคอยู่กลางจอ: จำลองระยะเลื่อนเมาส์ให้อยู่ในกรอบ -0.5 ถึง 0.5
                virtualMousePos.x += Input.GetAxis("Mouse X") * (mouseSensitivity * 0.02f);
                virtualMousePos.y += Input.GetAxis("Mouse Y") * (mouseSensitivity * 0.02f);
                virtualMousePos.x = Mathf.Clamp(virtualMousePos.x, -0.5f, 0.5f);
                virtualMousePos.y = Mathf.Clamp(virtualMousePos.y, -0.5f, 0.5f);
            }
            else
            {
                // กรณีเมาส์ไม่ถูกล็อค (เช่น เปิดกระเป๋า): คำนวณจากพิกัดหน้าจอเหมือนหน้าเลือก Node เป๊ะๆ
                virtualMousePos.x = Mathf.Clamp((Input.mousePosition.x / Screen.width) - 0.5f, -0.5f, 0.5f);
                virtualMousePos.y = Mathf.Clamp((Input.mousePosition.y / Screen.height) - 0.5f, -0.5f, 0.5f);
            }
        }
        else
        {
            // ถ้าตั้งค่าให้ต้องคลิกขวา แล้วปล่อยปุ่ม ให้ดึงมุมกล้องกลับมาตรงกลาง
            virtualMousePos = Vector2.zero;
        }

        // 1. มุมกล้องหลัก (ขึ้นอยู่กับว่ากำลังเดิน หรือกำลังปีนกำแพงฝั่งไหน)
        Quaternion baseRotation = Quaternion.Euler(lockedTilt, lockedPan, 0f);

        // 2. มุมเอียงนิดๆ ตามเมาส์ (สูตรเดียวกับ NodeSelectionManager)
        Quaternion swayRotation = Quaternion.Euler(-virtualMousePos.y * cameraSwayAmount, virtualMousePos.x * cameraSwayAmount, 0f);

        // 3. หมุนกล้องอย่างนุ่มนวล
        lookTarget.rotation = Quaternion.Lerp(lookTarget.rotation, baseRotation * swayRotation, Time.deltaTime * cameraSwaySpeed);
    }

    public void SwitchToClimbCam(float wallNormalAngleY)
    {
        virtualMousePos = Vector2.zero;
        lockedPan = wallNormalAngleY;
        lockedTilt = 0f; // ตอนปีนให้มองตรงไปที่กำแพง

        walkCam.SetActive(false);
        climbCam.SetActive(true);
    }

    public void SwitchToWalkCam()
    {
        virtualMousePos = Vector2.zero;
        lockedPan = initialWalkPan;
        lockedTilt = initialWalkTilt;
        
        climbCam.SetActive(false);
        walkCam.SetActive(true);
    }
}