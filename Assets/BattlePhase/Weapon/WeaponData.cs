using System.Collections.Generic;
using UnityEngine;

public enum WeaponType { Gun, Dagger, LongSword, Spell }
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Ascendia/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public Sprite weaponIcon;

    public WeaponType weaponType = WeaponType.Dagger;

    public float baseDamage;
    public float weight;
    public float speed;
    public float maxDurability;
    public float currentDurability; 
    public float freeAimMultiplier = 1.5f;

    [Header("Weapon Skills")]
    public List<WeaponSkill> availableSkills; 
}