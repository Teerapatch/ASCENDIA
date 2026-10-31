using System.Collections.Generic;
using UnityEngine;

// คลาสแม่แบบ Abstract (นำไปแปะ Object ไม่ได้ ต้องสร้างคลาสลูกมารับช่วงต่อ)
[System.Serializable]

public class QTEStep
{
    [Tooltip("เวลาหน่วงก่อนเริ่ม QTE รอบนี้ (ซิงก์กับ Animation ง้างอาวุธ)")]
    public float delayBeforeStart = 0.5f;

    [Tooltip("ความเร็วในการหมุนของเข็ม (องศาต่อวินาที)")]
    public float spinSpeed = 300f;

    [Tooltip("จุดเกิดของเข็มก่อนหมุนมาถึงเป้า (เช่น 180 คือครึ่งวงกลม)")]
    public float startingAngle = 180f;
}

// ระดับความเสียหาย Posture ตามตารางสกิล (ใช้แสดงผลในหน้าเลือกสกิล)
public enum SkillPostureRating { None, Low, Normal, High, Severe }

public abstract class WeaponSkill : ScriptableObject
{
    public string skillName;
    [TextArea] public string description;
    public int apCost;

    [Tooltip("ใช้แสดงผลในหน้าเลือกสกิลที่ Camp เท่านั้น")]
    public SkillPostureRating postureRating = SkillPostureRating.Low;

    [Header("QTE Settings")]
    public bool useQTE = true;
    public List<QTEStep> qteSteps;

    // ฟังก์ชันนี้บังคับให้คลาสลูกทุกตัวต้องเอาไปเขียนรายละเอียดของตัวเอง (Polymorphism)
    public abstract void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccesses);

    // เงื่อนไขพิเศษก่อนใช้สกิล (เช่น สกิลเวทที่ต้องมีธาตุใน Slot) reason = ข้อความสั้นที่โชว์ให้ผู้เล่น
    public virtual bool CanExecute(PlayerCombatController player, out string reason)
    {
        reason = null;
        return true;
    }

    // ดาเมจฐานของอาวุธที่ถืออยู่ (รวมเลเวลจาก Forge แล้ว)
    protected static float WeaponDamage(PlayerCombatController player)
    {
        return player.ActiveWeapon != null ? player.ActiveWeapon.baseDamage : 0f;
    }
}
