using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerData", menuName = "Game Data/Player Data")]
public class PlayerData : ScriptableObject
{
    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float stamina = 100f;
    public float baseStaminaDrain = 10f;

    [Header("Inventory & Stats")]
    public int ore = 0;
    public float weight = 0f;
    public float maxWeight = 30f;

    [Header("Lives")]
    public int piton = 3;

    // *** เพิ่มระบบจำว่าอยู่ชั้นไหน ***
    [Header("Progression")]
    public int currentFloor = 1; 
    public int maxFloorBeforeCamp = 10; // ครบ 10 ชั้นเจอแคมป์

    public void ResetData()
    {
        stamina = maxStamina;
        ore = 0;
        weight = 0f; 
        piton = 3;
        currentFloor = 1; // รีเซ็ตชั้นกลับมาที่ 1
    }
}