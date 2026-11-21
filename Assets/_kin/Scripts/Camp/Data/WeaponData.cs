using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    [Tooltip("ไอคอนเส้นขาวสำหรับฉากต่อสู้ (UI_New/Battle/Icon_*) ใช้ในหน้าเปลี่ยนอาวุธ")]
    public Sprite battleIcon;
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

    public const int MaxEquippedSkills = 3;
    public const int MaxWeaponLevel = 10;

    // เลเวลที่ปลดล็อกสกิลตามลำดับใน skillPool: 2 อันแรกได้ตั้งแต่สร้างอาวุธ ที่เหลือได้ที่เลเวล 3, 5, 7, 9
    // อยากเปลี่ยนว่าสกิลไหนได้ก่อน ให้เรียง skillPool ใหม่ใน Inspector
    public static readonly int[] SkillUnlockLevels = { 1, 1, 3, 5, 7, 9 };

    [Header("Weapon Skills")]
    [Tooltip("สกิลทั้งหมดของอาวุธนี้ (6 สกิล) เรียงตามลำดับที่ปลดล็อก: 2 อันแรกตอนสร้างอาวุธ แล้ว Lv.3, 5, 7, 9\nพกเข้าฉากต่อสู้ได้สูงสุด 3 จากที่ปลดล็อกแล้ว")]
    [FormerlySerializedAs("availableSkills")]
    public List<WeaponSkill> skillPool = new List<WeaponSkill>();

    // สกิลที่ใช้ได้จริงในฉากต่อสู้ (ตั้งค่าตอน CreateBattleInstance ไม่ถูกเซฟลงไฟล์)
    [System.NonSerialized] public List<WeaponSkill> availableSkills = new List<WeaponSkill>();

    public static int SkillUnlockLevel(int poolIndex)
    {
        return poolIndex >= 0 && poolIndex < SkillUnlockLevels.Length ? SkillUnlockLevels[poolIndex] : MaxWeaponLevel;
    }

    public int SkillUnlockLevel(WeaponSkill skill) => SkillUnlockLevel(skillPool.IndexOf(skill));

    public bool IsSkillUnlocked(WeaponSkill skill)
    {
        int index = skillPool.IndexOf(skill);
        return skill != null && index >= 0 && currentLevel >= SkillUnlockLevel(index);
    }

    public List<WeaponSkill> GetUnlockedSkills()
    {
        return skillPool.FindAll(IsSkillUnlocked);
    }

    // สกิลที่ปลดล็อกพอดีที่เลเวลนี้ (null ถ้าเลเวลนี้ไม่ได้สกิล)
    public WeaponSkill SkillUnlockedAt(int level)
    {
        if (level <= 1) return null;
        for (int i = 0; i < skillPool.Count; i++)
        {
            if (skillPool[i] != null && SkillUnlockLevel(i) == level) return skillPool[i];
        }
        return null;
    }

    // สกิลถัดไปที่จะได้ และเลเวลที่ต้องถึง
    public WeaponSkill NextSkillToUnlock(out int level)
    {
        level = 0;
        for (int i = 0; i < skillPool.Count; i++)
        {
            if (skillPool[i] == null || currentLevel >= SkillUnlockLevel(i)) continue;
            level = SkillUnlockLevel(i);
            return skillPool[i];
        }
        return null;
    }

    // ค่าเริ่มต้นถ้าผู้เล่นยังไม่เคยเลือก = สกิลที่ปลดล็อกแล้ว 3 อันแรก
    public List<WeaponSkill> GetDefaultSkills()
    {
        List<WeaponSkill> valid = GetUnlockedSkills();
        return valid.GetRange(0, Mathf.Min(MaxEquippedSkills, valid.Count));
    }

    [Header("Crafting Base Cost (ราคาตั้งต้น)")]
    public int gemStoneCost;
    public int monsterPartCost;

    [Header("Forge Status (ห้ามปรับเองตอนเล่น)")]
    public int requiredCampLevel = 1;
    public bool isUnlocked = false;
    public int currentLevel = 1;
    public int maxLevel = MaxWeaponLevel;

    [Header("Level Growth (เพิ่มต่อ 1 เลเวล เทียบกับค่าเลเวล 1)")]
    [Tooltip("ATK เพิ่มกี่ส่วนต่อเลเวล (0.12 = +12% ของค่าเลเวล 1, Lv.10 = x2.08)")]
    public float damageGrowth = .12f;
    [Tooltip("Speed เพิ่มกี่ส่วนต่อเลเวล (Speed สูง = ได้เทิร์นถี่ขึ้น)")]
    public float speedGrowth = .015f;
    [Tooltip("Durability สูงสุดเพิ่มกี่ส่วนต่อเลเวล")]
    public float durabilityGrowth = .08f;
    [Tooltip("ตัวคูณ Posture เพิ่มกี่ส่วนต่อเลเวล")]
    public float postureGrowth = .04f;

    public int MaxLevel => Mathf.Clamp(maxLevel, 1, MaxWeaponLevel);
    public bool IsMaxLevel => currentLevel >= MaxLevel;

    // --- ฟังก์ชันคำนวณสเตตัสตามเลเวล ---
    private static float Grow(float value, float growth, int level) => value * (1f + growth * (Mathf.Max(1, level) - 1));

    public float GetDamageAt(int level) => Mathf.Round(Grow(baseDamage, damageGrowth, level) * 10f) / 10f;
    public float GetSpeedAt(int level) => Mathf.Round(Grow(speed, speedGrowth, level) * 10f) / 10f;
    public float GetDurabilityAt(int level) => Mathf.Round(Grow(maxDurability, durabilityGrowth, level));
    public float GetPostureAt(int level) => Grow(postureDamageMultiplier, postureGrowth, level);

    public float GetCurrentDamage() => GetDamageAt(currentLevel);
    public float GetCurrentSpeed() => GetSpeedAt(currentLevel);
    public float GetNextLevelDamage() => GetDamageAt(currentLevel + 1);
    public float GetNextLevelSpeed() => GetSpeedAt(currentLevel + 1);

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
    public WeaponData CreateBattleInstance(List<WeaponSkill> equippedSkills = null)
    {
        WeaponData instance = Instantiate(this);
        // พกได้เฉพาะสกิลที่ปลดล็อกแล้ว
        List<WeaponSkill> skills = equippedSkills != null ? equippedSkills.FindAll(IsSkillUnlocked) : GetDefaultSkills();
        instance.availableSkills = new List<WeaponSkill>(skills);
        instance.name = name;
        instance.baseDamage = GetCurrentDamage();
        instance.speed = Mathf.Max(1f, GetCurrentSpeed());
        instance.maxDurability = GetDurabilityAt(currentLevel);
        instance.postureDamageMultiplier = GetPostureAt(currentLevel);
        // อบเลเวลลงสเตตัสแล้ว ปิดการโตต่อในตัวก๊อปปี้ กันไม่ให้บวกซ้ำ
        instance.damageGrowth = instance.speedGrowth = instance.durabilityGrowth = instance.postureGrowth = 0f;
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
