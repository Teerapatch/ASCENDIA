using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Ascendia/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public float baseDamage;
    public float weight;
    public float speed;
    public float maxDurability;
    public float currentDurability; 
    public float freeAimMultiplier = 1.5f;

    // --- [เพิ่มใหม่] คลังสกิลของอาวุธชิ้นนี้ ---
    [Header("Weapon Skills")]
    public List<WeaponSkill> availableSkills; 
}