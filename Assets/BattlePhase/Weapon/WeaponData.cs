using System.Collections.Generic;
using UnityEngine;

public enum WeaponType
{
    Dagger,
    LongSword,
    Bow,
    Spell
}

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Ascendia/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public float baseDamage;
    public float currentDurability;
   [Header("Basic Info")]
    public WeaponType weaponType;
    public string weaponName;
    public Sprite weaponIcon;
    public Color auraColor = Color.white;
    public string description;

    [Header("Base Stats (ค่าเริ่มต้น)")]
    public float attackDamage;
    public float speed;         // สปีด หรือ AP
    public float postureMultiplier;
    public float weight;
    public float maxDurability;
    public float freeAimMultiplier = 1.5f;

    [Header("Crafting Base Cost (ราคาตั้งต้น)")]
    public int gemStoneCost;
    public int monsterPartCost;

    [Header("Weapon Skills")]
    public List<WeaponSkill> availableSkills; 

    // ==========================================
    // 🌟 โซนใหม่: ระบบ Forge & Progression
    // ==========================================
    [Header("Forge Status (ห้ามปรับเองตอนเล่น)")]
    public bool isUnlocked = false; // คราฟต์แล้วหรือยัง? (ถ้ายัง = ต้องใช้ Craft, ถ้าคราฟต์แล้ว = ใช้ Upgrade)
    public int currentLevel = 1;
    public int maxLevel = 5;        // เลเวลตันที่เท่าไหร่

    [Header("Upgrade Paths (จำนวนครั้งที่อัปเกรด)")]
    public int damageUpgradeCount = 0; // อัปเกรดสายดาเมจไปกี่ครั้ง
    public int apUpgradeCount = 0;     // อัปเกรดสายลด AP ไปกี่ครั้ง

    // --- ฟังก์ชันคำนวณสเตตัสปัจจุบัน (เอาไปโชว์ใน UI ได้เลย) ---
    
    // สมมติ: อัปสายดาเมจ 1 ครั้ง เพิ่มดาเมจ 5 หน่วย
    public float GetCurrentDamage() 
    { 
        return attackDamage + (damageUpgradeCount * 5f); 
    }

    // สมมติ: อัปสาย AP 1 ครั้ง ลดค่า AP/Speed ลง 2 หน่วย (ยิ่งน้อยยิ่งดี)
    public float GetCurrentSpeed() 
    { 
        return speed - (apUpgradeCount * 2f); 
    }

    // คำนวณวัตถุดิบที่ต้องใช้ในเลเวลถัดไป (ยิ่งเลเวลสูง ยิ่งใช้ของเยอะขึ้น)
    public int GetNextLevelGemCost() 
    { 
        return gemStoneCost * currentLevel; 
    }
    public int GetNextLevelMonsterPartCost() 
    { 
        return monsterPartCost * currentLevel; 
    }

    // รีเซ็ตอาวุธกลับเป็นเลเวล 1 (เอาไว้เรียกใช้ตอนเริ่มเกมใหม่)
    [ContextMenu("Reset Weapon Status")]
    public void ResetWeaponStatus()
    {
        isUnlocked = false;
        currentLevel = 1;
        damageUpgradeCount = 0;
        apUpgradeCount = 0;
    }
}