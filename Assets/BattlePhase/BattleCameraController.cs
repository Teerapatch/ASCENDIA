using UnityEngine;
using System.Collections;

public class BattleCameraController : MonoBehaviour
{
    public static BattleCameraController Instance;

    [Header("Camera Settings")]
    public float transitionSpeed = 5f;
    public float defaultOrthoSize = 7f; // ขนาดกล้องตอนมองเห็นทั้งฉาก
    public float zoomOrthoSize = 4f;    // ขนาดกล้องตอนซูมเจาะจงเป้าหมาย
    public Vector3 offset = new Vector3(0, 0, -10f); // ระยะห่างกล้องในแกน Z

    private Camera cam;
    private Vector3 defaultPosition;
    private Vector3 targetPosition;
    private float targetSize;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        defaultPosition = transform.position;

        // เซ็ตค่าเริ่มต้น
        targetPosition = defaultPosition;
        targetSize = defaultOrthoSize;
        cam.orthographicSize = defaultOrthoSize;
    }

    private void Update()
    {
        // ทำ Smooth transition ทุกๆ เฟรม
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * transitionSpeed);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * transitionSpeed);
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