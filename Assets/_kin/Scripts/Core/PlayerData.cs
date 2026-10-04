using UnityEngine;
using System.Collections.Generic;

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

    [Header("Movement Stats")]
    public float baseClimbSpeed = 5f;
    public float baseWalkSpeed = 5f;

    [Header("Lives")]
    public int piton = 3;

    [Header("Progression")]
    public int currentFloor = 1;
    public int maxFloorBeforeCamp = 10;
    public int currentCampLevel = 1; // 🌟 จำว่าตอนนี้ถึงแคมป์ที่เท่าไหร่แล้ว (1 ถึง 4)

    [Header("Active Loadout (อาวุธที่เลือกพกไปสู้)")]
    // 🌟 ใช้ List เก็บอาวุธที่เลือกไปสู้ สูงสุด 4 ชิ้น (เปลี่ยนตาม Camp Level)
    public List<WeaponData> activeLoadout = new List<WeaponData>();

    // 🌟 จำนวนช่องที่ปลดล็อกแล้ว (อิงจาก Camp Level) เริ่มต้นที่ 1 ช่อง
    public int unlockedWeaponSlots = 1;

    // 🌟 เผื่อไว้ใช้ตอนสู้จริง เพื่อเช็คว่ากำลังถืออาวุธชิ้นไหนอยู่ในมือ
    [Header("Current Wielding (อาวุธที่ถืออยู่ตอนสู้)")]
    public WeaponData currentWeaponInHand;

    public void ResetData()
    {
        stamina = maxStamina;
        ore = 0;
        weight = 0f;
        piton = 3;
        currentFloor = 1;
        currentWeaponDUR = maxWeaponDUR;

        currentCampLevel = 1;
        unlockedWeaponSlots = 1; // เริ่มใหม่ให้มี 1 ช่อง

        activeLoadout.Clear(); // ล้างของในกระเป๋าสู้
        currentWeaponInHand = null;
    }
    public void ReachNextCamp()
    {
        currentCampLevel++;
        if (currentCampLevel <= 4)
        {
            unlockedWeaponSlots = currentCampLevel; // ปลดล็อกช่องตามเลเวลแคมป์ (สูงสุด 4)
        }
    }
}