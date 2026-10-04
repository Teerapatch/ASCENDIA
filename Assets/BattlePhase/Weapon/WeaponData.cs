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
    public WeaponType weaponType;
    public string weaponName;
    public Sprite weaponIcon;

// 🌟 เพิ่มบรรทัดนี้: สีออร่าประจำอาวุธชิ้นนี้ (ตั้งค่าเริ่มต้นเป็นสีขาว)
    public Color auraColor = Color.white;

    public float baseDamage;
    public float weight;
    public float speed;
    public float maxDurability;
    public float currentDurability; 
    public float freeAimMultiplier = 1.5f;

    [Header("Weapon Skills")]
    public List<WeaponSkill> availableSkills; 

    [Header("Weapon Info")]
    public string description;

    [Header("Base Stats")]
    public float attackDamage;
    public float postureMultiplier;

    [Header("Crafting Cost")]
    public int gemStoneCost;
    public int monsterPartCost;
}