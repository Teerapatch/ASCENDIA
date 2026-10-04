using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerData", menuName = "Game Data/Player Data")]
public class PlayerData : ScriptableObject
{
    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float stamina = 100f;
    public float baseStaminaDrain = 10f;

    [Header("Weapon System (DUR)")]
    public float maxWeaponDUR = 100f;
    public float currentWeaponDUR = 100f;

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
    [Header("Active Loadout (อาวุธ 4 ชนิดที่เลือกใช้)")]
    public WeaponData equippedDagger;
    public WeaponData equippedSword;
    public WeaponData equippedBow;
    public WeaponData equippedSpell;

    public void ResetData()
    {
        stamina = maxStamina;
        ore = 0;
        weight = 0f;
        piton = 3;
        currentFloor = 1;
        currentWeaponDUR = maxWeaponDUR;
        // 🌟 อาจจะเซ็ตอาวุธเริ่มต้น (ถ้ามี) หรือปล่อยเป็น null
        equippedDagger = null;
        equippedSword = null;
        equippedBow = null;
        equippedSpell = null;
        // หมายเหตุ: ไม่ต้องรีเซ็ต Speed เพราะถือเป็นสเตตัสติดตัว
    }
}