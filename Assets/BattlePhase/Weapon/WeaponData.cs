using System.Collections.Generic;
using UnityEngine;

public enum WeaponType { Gun, Dagger, LongSword, Spell }
public enum ElementType { Fire, Water, Wind, Earth, None }
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

    [Header("Posture Value")]
    public float postureDamageMultiplier = 1.5f;
    public float staggerDamageMultiplier = 1.5f;

    [Header("Spell Specific Settings")]
    public int maxElementSlots = 5;

    [Header("Weapon Skills")]
    public List<WeaponSkill> availableSkills; 
}