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

public abstract class WeaponSkill : ScriptableObject
{
    public string skillName;
    [TextArea] public string description;
    public int apCost;

    [Header("QTE Settings")]
    public bool useQTE = true;
    public List<QTEStep> qteSteps;

    // ฟังก์ชันนี้บังคับให้คลาสลูกทุกตัวต้องเอาไปเขียนรายละเอียดของตัวเอง (Polymorphism)
    public abstract void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccesses);
}