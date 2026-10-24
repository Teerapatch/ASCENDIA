using System.Collections.Generic;
using UnityEngine;

// ค่าตัวเลขของ enum ถูกเซฟลงในไฟล์ .asset ห้ามสลับลำดับหรือแทรกกลาง (เพิ่มต่อท้ายได้)
public enum WeaponType
{
    Gun = 0,      // อาวุธยิงระยะไกล (ธนู/ปืน)
    Dagger = 1,
    LongSword = 2,
    Spell = 3
}

// ไฟล์เดียวที่ใช้ร่วมกันทั้ง Camp (Forge/Loadout) และ BattlePhase (การต่อสู้)
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Ascendia/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Basic Info")]
    public WeaponType weaponType = WeaponType.Dagger;
    public string weaponName;
    public Sprite weaponIcon;
    public Color auraColor = Color.white;
    public string description;

    [Header("Base Stats (ค่าเริ่มต้นที่เลเวล 1)")]
    public float baseDamage;
    public float speed;
    public float weight;
    public float maxDurability;
    public float currentDurability;
    public float freeAimMultiplier = 1.5f;

    [Header("Posture Value")]
    public float postureDamageMultiplier = 1.5f;
    public float staggerDamageMultiplier = 1.5f;

    [Header("Spell Specific Settings")]
    public int maxElementSlots = 5;

    [Header("Weapon Skills")]
    public List<WeaponSkill> availableSkills;

    [Header("Crafting Base Cost (ราคาตั้งต้น)")]
    public int gemStoneCost;
    public int monsterPartCost;

    [Header("Forge Status (ห้ามปรับเองตอนเล่น)")]
    public int requiredCampLevel = 1;
    public bool isUnlocked = false;
    public int currentLevel = 1;
    public int maxLevel = 5;

    [Header("Upgrade Values (สเตตัสที่ได้เพิ่มต่อ 1 เลเวล)")]
    public float attackIncreasePerLevel = 5f; // เพิ่ม ATK ทีละเท่าไหร่
    public float speedIncreasePerLevel = -2f; // เพิ่ม/ลด Speed ทีละเท่าไหร่

    // --- ฟังก์ชันคำนวณสเตตัส ---
    public float GetCurrentDamage()
    {
        return baseDamage + (attackIncreasePerLevel * (currentLevel - 1));
    }
    public float GetCurrentSpeed()
    {
        return speed + (speedIncreasePerLevel * (currentLevel - 1));
    }

    public float GetNextLevelDamage()
    {
        return baseDamage + (attackIncreasePerLevel * currentLevel);
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

    /// <summary>
    /// สร้างตัวก๊อปปี้สำหรับใช้ในฉากต่อสู้ (ค่าที่เปลี่ยนระหว่างสู้จะไม่ไปแก้ไฟล์ .asset จริง)
    /// สเตตัสจากเลเวลของ Forge จะถูกอบลงใน baseDamage / speed ของตัวก๊อปปี้เลย
    /// </summary>
    public WeaponData CreateBattleInstance()
    {
        WeaponData instance = Instantiate(this);
        instance.name = name;
        instance.baseDamage = GetCurrentDamage();
        instance.speed = Mathf.Max(1f, GetCurrentSpeed());
        instance.currentLevel = 1; // อบเลเวลลงสเตตัสแล้ว กันไม่ให้ Get...() บวกซ้ำ
        instance.currentDurability = instance.maxDurability;
        return instance;
    }

    [ContextMenu("Reset Weapon Status")]
    public void ResetWeaponStatus()
    {
        isUnlocked = false;
        currentLevel = 1;
    }
}
