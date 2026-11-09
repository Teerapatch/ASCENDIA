using System;
using System.Collections.Generic;
using UnityEngine;

// ตารางอนิเมชันการต่อสู้ของผู้เล่น สร้างด้วยเมนู Tools/ASCENDIA/Build Battle Animators แล้วปรับค่าใน Inspector ได้
[Serializable]
public class BattleClip
{
    [Tooltip("ชื่อ State ใน Animator")]
    public string state;
    [Range(0f, 1f), Tooltip("จังหวะที่โดนเป้า (สัดส่วนของคลิป)")]
    public float impact = 0.5f;
    [Tooltip("จังหวะที่อาวุธโดนแต่ละครั้ง (สัดส่วนของคลิป) ดาเมจจะถูกแบ่งตามจำนวนนี้")]
    public List<float> hits = new List<float>();
    [Tooltip("ความเร็วเล่นคลิป")]
    public float speed = 1f;
    [Tooltip("พุ่งเข้าไปตีถึงตัวศัตรู")]
    public bool melee;
}

[Serializable]
public class BattleSkillClip
{
    public string skillName;
    public BattleClip clip;
}

[Serializable]
public class BattleWeaponAnimation
{
    public WeaponType weapon;
    public GameObject model;
    public RuntimeAnimatorController controller;
    public string idleState = "WepIdle";
    public BattleClip attack;
    public List<BattleSkillClip> skills = new List<BattleSkillClip>();
    public List<string> parries = new List<string>();

    public BattleClip FindSkill(string skillName)
    {
        BattleSkillClip found = skills.Find(s => s.skillName == skillName);
        return found != null ? found.clip : attack;
    }
}

public class BattleAnimationSet : ScriptableObject
{
    [Header("ตำแหน่งโมเดลใต้ Player_Holder")]
    public Vector3 modelLocalPosition = new Vector3(0.2f, -0.139f, -1.37f);
    public Vector3 modelLocalEuler = new Vector3(0f, 45f, 0f);
    public Vector3 modelLocalScale = Vector3.one;

    [Header("จังหวะ")]
    [Tooltip("ระยะหยุดหน้าศัตรู (นับจากขอบตัวศัตรู หน่วยใน Characters)")]
    public float meleeStandoff = 2.4f;
    public float dashInDuration = 0.24f;
    public float dashBackDuration = 0.3f;
    [Tooltip("เวลาค้างก่อนจบเทิร์น ให้เห็นผลของการโจมตี")]
    public float recoveryDelay = 0.25f;
    [Tooltip("ช่วงเปลี่ยนท่า (วินาที)")]
    public float crossFade = 0.1f;
    [Tooltip("ความเร็วคลิปตอนง้างค้างรอ QTE")]
    public float qteHoldSpeed = 0.06f;

    public List<BattleWeaponAnimation> weapons = new List<BattleWeaponAnimation>();

    public BattleWeaponAnimation Find(WeaponType type)
    {
        return weapons.Find(w => w.weapon == type);
    }
}
