using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusBarUI : MonoBehaviour
{
    public Slider staminaBar;

    public TMP_Text staminaText;
    public TMP_Text oreText;
    public TMP_Text weightText;

    private void Update()
    {
        if (GameManager.Instance == null)
            return;

        PlayerData data =
            GameManager.Instance.playerData;

        // Stamina Bar
        staminaBar.maxValue = data.maxStamina;
        staminaBar.value = data.stamina;

        // Text
        staminaText.text =
            $"STAMINA {data.stamina:F0}/{data.maxStamina:F0}";

        oreText.text =
            $"ORE {data.ore}";

        weightText.text =
            $"WEIGHT {data.weight:F0}/{data.maxWeight:F0}";
    }
}