using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerData", menuName = "Game Data/Player Data")]
public class PlayerData : ScriptableObject
{
    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float stamina = 100f;
    public float baseStaminaDrain = 10f; // << เพิ่มบรรทัดนี้เข้ามา

    [Header("Inventory & Stats")]
    public int ore = 0;
    public float weight = 0f;
    public float maxWeight = 30f;

    [Header("Lives")]
    public int piton = 3;

    public void ResetData()
    {
        stamina = maxStamina;
        ore = 0;
        weight = 0f; 
        piton = 3;
    }
}