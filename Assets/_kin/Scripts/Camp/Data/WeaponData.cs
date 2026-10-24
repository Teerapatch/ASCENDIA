using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// ค่า int ถูก serialize ลงไฟล์ Asset ห้ามสลับลำดับหรือแทรกกลาง (ถ้าจะเพิ่มให้ต่อท้าย)
public enum WeaponType
{
    Gun = 0,
    Dagger = 1,
    LongSword = 2,
    Spell = 3,
    Bow = 4
}

public enum ElementType { Fire, Water, Wind, Earth, None }

public static class ElementTypeUtil
{
    private static readonly ElementType[] PlayableElements = { ElementType.Fire, ElementType.Water, ElementType.Wind, ElementType.Earth };

    public static ElementType Random()
    {
        return PlayableElements[UnityEngine.Random.Range(0, PlayableElements.Length)];
    }
}

// คลาสข้อมูลอาวุธตัวเดียวที่ใช้ร่วมกันทั้ง Camp (Forge/Loadout) และ BattlePhase
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Ascendia/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Basic Info")]
    public WeaponType weaponType;
    public string weaponName;
    public Sprite weaponIcon;
    public Color auraColor = Color.white;
    public string description;

    [Header("Base Stats (ค่าเริ่มต้น Lv.1 — ตอนใช้งานให้เรียก GetCurrentDamage / GetCurrentSpeed)")]
    public float attackDamage;
    public float speed;
    public float weight;
    public float maxDurability;
    public float freeAimMultiplier = 1.5f;

    [Header("Posture Value")]
    [FormerlySerializedAs("postureMultiplier")]
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
    public float attackIncreasePerLevel = 5f;
    public float speedIncreasePerLevel = -2f;

    [Header("Runtime (BattlePhase ใช้กับตัวก๊อปปี้เท่านั้น)")]
    public float currentDurability;

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
