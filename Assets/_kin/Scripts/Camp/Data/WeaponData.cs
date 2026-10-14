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
    public float speed;
    public float postureMultiplier;
    public float weight;
    public float maxDurability;
    public float freeAimMultiplier = 1.5f;

    [Header("Crafting Base Cost (ราคาตั้งต้น)")]
    public int gemStoneCost;
    public int monsterPartCost;

    [Header("Weapon Skills")]
    public List<WeaponSkill> availableSkills; 

    [Header("Forge Status (ห้ามปรับเองตอนเล่น)")]
    public int requiredCampLevel = 1; 
    public bool isUnlocked = false; 
    public int currentLevel = 1;
    public int maxLevel = 5;      

    // ==========================================
    // 🌟 โซนใหม่: กำหนดค่าอัปเกรดตายตัว (Fixed Stats)
    // ==========================================
    [Header("Upgrade Values (สเตตัสที่ได้เพิ่มต่อ 1 เลเวล)")]
    public float attackIncreasePerLevel = 5f; // เพิ่ม ATK ทีละเท่าไหร่
    public float speedIncreasePerLevel = -2f; // เพิ่ม/ลด Speed ทีละเท่าไหร่

    // --- ฟังก์ชันคำนวณสเตตัส ---
    public float GetCurrentDamage() 
    { 
        return attackDamage + (attackIncreasePerLevel * (currentLevel - 1)); 
    }
    public float GetCurrentSpeed() 
    { 
        return speed + (speedIncreasePerLevel * (currentLevel - 1)); 
    }

    public float GetNextLevelDamage() 
    { 
        return attackDamage + (attackIncreasePerLevel * currentLevel); 
    }
    public float GetNextLevelSpeed() 
    { 
        return speed + (speedIncreasePerLevel * currentLevel); 
    }

    // ราคาวัตถุดิบ (ยิ่งเลเวลสูง ยิ่งใช้ของเยอะขึ้น)
    public int GetNextLevelGemCost() 
    { 
        return gemStoneCost * currentLevel; 
    }
    public int GetNextLevelMonsterPartCost() 
    { 
        return monsterPartCost * currentLevel; 
    }

    [ContextMenu("Reset Weapon Status")]
    public void ResetWeaponStatus()
    {
        isUnlocked = false;
        currentLevel = 1;
    }
}