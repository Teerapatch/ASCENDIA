using UnityEngine;
using Unity.Cinemachine;

public class BattleCameraController : MonoBehaviour
{
    public static BattleCameraController Instance;

    [Header("Cinemachine")]
    [Tooltip("ลาก CinemachineCamera (ตัวจำลอง) มาใส่ที่นี่")]
    public CinemachineCamera battleVCam;

    [Header("Camera Settings")]
    public float transitionSpeed = 5f;
    public float defaultOrthoSize = 7f;
    public float zoomOrthoSize = 4f;
    public Vector3 offset = new Vector3(0, 0, -10f);

    private Vector3 defaultPosition;
    private Vector3 targetPosition;
    private float targetSize;

    private void Awake()
    {
        Instance = this;

        if (battleVCam == null)
            battleVCam = GetComponent<CinemachineCamera>();

        // 1. จำตำแหน่งจุดเริ่มต้นของ VCam ไว้เป็นจุด Default อัตโนมัติ (ไม่ต้องใช้ Transform อ้างอิงเพิ่ม)
        if (battleVCam != null)
        {
            defaultPosition = battleVCam.transform.position;
        }
        else
        {
            defaultPosition = transform.position;
        }

        // เซ็ตค่าเริ่มต้น
        targetPosition = defaultPosition;
        targetSize = defaultOrthoSize;

        // ดึงโครงสร้างเลนส์มาปรับค่าเริ่มต้น
        if (battleVCam != null)
        {
            var lens = battleVCam.Lens;
            lens.OrthographicSize = defaultOrthoSize;
            battleVCam.Lens = lens;
        }
    }

    private void Update()
    {
        if (battleVCam == null) return;

        // 1. Smooth Transition ตำแหน่ง (Lerp ใส่ VCam โดยตรง)
        battleVCam.transform.position = Vector3.Lerp(battleVCam.transform.position, targetPosition, Time.deltaTime * transitionSpeed);

        // 2. Smooth Transition ขนาดกล้อง (Zoom)
        // ใน Cinemachine 3 Lens เป็น Struct ต้องดึงออกมาแก้ แล้วยัดกลับเข้าไปใหม่
        var lens = battleVCam.Lens;
        lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, targetSize, Time.deltaTime * transitionSpeed);
        battleVCam.Lens = lens;
    }

    // เรียกฟังก์ชันนี้เมื่อเลือกศัตรู
    public void FocusOnTarget(Transform targetTransform)
    {
        targetPosition = targetTransform.position + offset;
        targetSize = zoomOrthoSize;
    }

    // เรียกฟังก์ชันนี้เมื่อจบเทิร์น หรือยกเลิกการเลือก
    public void ResetCamera()
    {
        targetPosition = defaultPosition;
        targetSize = defaultOrthoSize;
    }
}