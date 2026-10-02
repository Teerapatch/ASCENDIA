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

    // 🌟 เพิ่มสเตตัสความเร็วพื้นฐานตรงนี้
    [Header("Movement Stats")]
    public float baseClimbSpeed = 5f; 
    public float baseWalkSpeed = 5f;

    [Header("Lives")]
    public int piton = 3;

    [Header("Progression")]
    public int currentFloor = 1; 
    public int maxFloorBeforeCamp = 10; 

    public void ResetData()
    {
        stamina = maxStamina;
        ore = 0;
        weight = 0f; 
        piton = 3;
        currentFloor = 1; 
        // หมายเหตุ: ไม่ต้องรีเซ็ต Speed เพราะถือเป็นสเตตัสติดตัว
    }
}