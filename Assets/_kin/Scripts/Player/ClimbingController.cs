using UnityEngine;

public class ClimbingController : MonoBehaviour
{
    [Header("Climbing")]
    public float climbSpeed = 2f;

    public float minX = -5f;
    public float maxX = 5f;

    public float minY = 0.5f;
    public float maxY = 14f;

    [Header("Stamina")]
    public float staminaDrain = 5f;

    private void Update()
    {
        HandleClimbing();
    }

    private void HandleClimbing()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 movement =
            new Vector3(horizontal, vertical, 0f);

        if (movement.magnitude > 0.1f)
        {
            movement.Normalize();

            Vector3 newPosition =
                transform.position +
                movement * climbSpeed * Time.deltaTime;

            newPosition.x =
                Mathf.Clamp(newPosition.x, minX, maxX);

            newPosition.y =
                Mathf.Clamp(newPosition.y, minY, maxY);

            transform.position = newPosition;

            DrainStamina();
        }
    }

    private void DrainStamina()
    {
        if (GameManager.Instance == null)
            return;

        PlayerData data =
            GameManager.Instance.playerData;

        // น้ำหนักยิ่งมาก ยิ่งใช้ Stamina มาก
        float weightModifier =
            data.weight * 0.1f;

        float drain =
            (staminaDrain + weightModifier)
            * Time.deltaTime;

        data.stamina -= drain;

        if (data.stamina < 0)
            data.stamina = 0;
    }
}