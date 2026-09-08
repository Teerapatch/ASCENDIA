using UnityEngine;

public class SafeZone : MonoBehaviour
{
    public float staminaRecovery = 20f;

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerData data =
            GameManager.Instance.playerData;

        data.stamina +=
            staminaRecovery * Time.deltaTime;

        if (data.stamina > data.maxStamina)
        {
            data.stamina = data.maxStamina;
        }
    }
}