using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    public Transform target;

    [Header("Camera")]
    public float distance = 6f;
    public float mouseSensitivity = 3f;

    [Header("Vertical Rotation")]
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 70f;

    private float horizontalAngle = 0f;
    private float verticalAngle = 20f;

    private void LateUpdate()
    {
        if (target == null)
            return;

        HandleMouseInput();
        UpdateCameraPosition();
    }

    private void HandleMouseInput()
    {
        if (!Input.GetMouseButton(1))
            return;

        float mouseX =
            Input.GetAxis("Mouse X");

        float mouseY =
            Input.GetAxis("Mouse Y");

        horizontalAngle +=
            mouseX * mouseSensitivity;

        verticalAngle -=
            mouseY * mouseSensitivity;

        verticalAngle =
            Mathf.Clamp(
                verticalAngle,
                minVerticalAngle,
                maxVerticalAngle
            );
    }

    private void UpdateCameraPosition()
    {
        Quaternion rotation =
            Quaternion.Euler(
                verticalAngle,
                horizontalAngle,
                0f
            );

        Vector3 offset =
            rotation * Vector3.back * distance;

        transform.position =
            target.position + offset;

        transform.LookAt(target);
    }
}