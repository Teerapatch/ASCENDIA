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
    [Tooltip("ของในกระเป๋าระหว่างปีน เก็บไว้ที่นี่เพื่อให้อยู่รอดตอนโหลดฉาก (เช่น ไปสู้แล้วกลับมา)")]
    public List<InventorySlot> inventorySlots = new List<InventorySlot>();
    public int gemStone = 0;
    public int monsterPart = 0;
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
    public int currentCampLevel = 1;

    [Tooltip("กำหนดว่าช่วง Camp Level ที่เท่าไหร่คือช่วงสุดท้ายที่จะเจอบอสใหญ่ (เช่น ตั้งไว้ 4 = แคมป์ 1-3 จะเจอโหนด Camp ส่วนช่วงที่ 4 ตอนจบจะเจอโหนด Boss อันเดียว)")]
    public int finalCampLevel = 4;

    [Header("Active Loadout")]
    public WeaponData[] activeLoadout = new WeaponData[4];
    public int unlockedWeaponSlots = 1;

    [Header("Current Wielding")]
    public WeaponData currentWeaponInHand;

    // เช็คว่าตอนนี้อยู่ในช่วงสุดท้ายที่จะเจอบอสใหญ่หรือยัง
    public bool IsFinalBossStage => currentCampLevel >= finalCampLevel;

    // อาวุธที่ใส่อยู่ใน Loadout (ข้ามช่องว่าง) ใช้ส่งเข้าฉากต่อสู้
    public List<WeaponData> GetEquippedWeapons()
    {
        List<WeaponData> equipped = new List<WeaponData>();
        foreach (WeaponData weapon in activeLoadout)
        {
            if (weapon != null) equipped.Add(weapon);
        }
        return equipped;
    }

    public void ResetData()
    {
        stamina = maxStamina;
        inventorySlots.Clear();
        gemStone = 0;
        weight = 0f;
        piton = 3;
        currentFloor = 1;
        currentWeaponDUR = maxWeaponDUR;
        currentCampLevel = 1;
        unlockedWeaponSlots = 1;
        activeLoadout = new WeaponData[4];
        currentWeaponInHand = null;
    }

    public void ReachNextCamp()
    {
        currentCampLevel++;
        if (currentCampLevel <= 4)
        {
            unlockedWeaponSlots = currentCampLevel;
        }
    }
}