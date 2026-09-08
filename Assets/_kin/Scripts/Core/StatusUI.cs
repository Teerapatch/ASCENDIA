using UnityEngine;
using TMPro;

public class StatusUI : MonoBehaviour
{
    public TMP_Text statusText;

    private void Update()
    {
        if (GameManager.Instance == null)
            return;

        PlayerData data =
            GameManager.Instance.playerData;

        statusText.text =
            $"HP: {data.hp} / {data.maxHP}\n" +
            $"STAMINA: {data.stamina:F0} / {data.maxStamina:F0}\n" +
            $"ORE: {data.ore}\n" +
            $"WEIGHT: {data.weight:F0}\n" +
            $"PITON: {data.piton}";
    }
}