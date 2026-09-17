using UnityEngine;
using UnityEngine.UI;

public class StatusBarUI : MonoBehaviour
{
    public Slider staminaBar;
    
    private void Update()
    {
        if (GameManager.Instance == null)
            return;

        PlayerData data = GameManager.Instance.playerData;

        // Stamina Bar
        staminaBar.maxValue = data.maxStamina;
        staminaBar.value = data.stamina;
    }
}