using System;
using System.Collections.Generic;
using UnityEngine;

// ข้อมูลอนิเมชันของบอส สร้างด้วยเมนู Tools/ASCENDIA/Build Boss Animator แล้วปรับค่าใน Inspector ได้
[Serializable]
public class BossStrike
{
    [Range(0f, 1f), Tooltip("จังหวะที่อาวุธฟันถึง (สัดส่วนของคลิป)")]
    public float time;
    [Tooltip("ทิศที่ผู้เล่นต้องลาก A = คำนวณจากการเคลื่อนที่ของอาวุธ")]
    [ParryDirectionPad(true)]
    public int direction = ParryDirections.Auto;
    [Tooltip("ทิศที่ปลายอาวุธเคลื่อนที่ในพื้นที่ของโมเดล ใช้คำนวณทิศ Parry แบบอัตโนมัติ")]
    public Vector3 swing;
}

[Serializable]
public class BossMove
{
    public string state;
    public float length;
    [Tooltip("แก้ใน Boss Parry Editor แล้ว เมนู Build Boss Animator จะไม่เขียนทับจังหวะของท่านี้")]
    public bool edited;
    public List<BossStrike> strikes = new List<BossStrike>();
}

public class BossAnimationSet : ScriptableObject
{
    public GameObject model;
    public RuntimeAnimatorController controller;
    public string idleState = "Boss-Idle";

    [Header("ขนาดและตำแหน่ง")]
    [Tooltip("ความสูงของบอสในฉาก (หน่วยโลก)")]
    public float targetHeight = 9f;
    public float modelHeight = 1.7f;
    [Tooltip("พื้นอยู่ต่ำกว่าจุดเกิดกี่หน่วย")]
    public float groundBelowSpawn = 2.1f;

    [Header("ท่าโจมตี")]
    public List<BossMove> basicAttacks = new List<BossMove>();
    public BossMove dance;
    public string rageState = "Boss_RageSkill";
    public float rageLength = 4f;
    [Range(0f, 1f), Tooltip("จังหวะคำรามที่ลูกน้องปรากฏ")]
    public float rageSummonTime = 0.5f;

    [Header("โอกาสตีต่อ")]
    [Range(0f, 1f), Tooltip("โอกาสตีต่อหลังท่าแรก")]
    public float chainChance = 0.7f;
    [Range(0f, 1f), Tooltip("โอกาสที่ลดลงทุกครั้งที่ตีต่อ")]
    public float chainDecay = 0.1f;
    [Range(0f, 1f), Tooltip("โอกาสใช้ Dance Skill แทนการตีปกติ (ไม่ใช้ติดกัน 2 เทิร์น)")]
    public float danceChance = 0.3f;

    [Header("การ Parry")]
    [Tooltip("เปิดช่วง Parry ก่อนอาวุธฟันถึงกี่วินาที")]
    public float parryLead = 0.28f;
    public float parryWindow = 0.5f;
    [Tooltip("เวลาขึ้น Hint เตือนสูงสุด")]
    public float warningDuration = 0.1f;
    public float basicDamage = 10f;
    public float basicPosture = 10f;
    public float danceDamage = 6f;
    public float dancePosture = 6f;

    [Header("พรีวิวใน Boss Parry Editor")]
    [Tooltip("มุมกล้อง Overview ของฉากต่อสู้")]
    public Vector3 previewCameraEuler = new Vector3(15f, 65f, 0f);
    [Tooltip("มุมหันของบอสเมื่อยืนที่จุดเกิดในฉาก")]
    public float previewBossYaw = -90f;
}
