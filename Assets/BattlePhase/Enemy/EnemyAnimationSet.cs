using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// ตารางอนิเมชันของศัตรู 1 ชนิด สร้างด้วย Tools/ASCENDIA/Build Enemy Animators แล้วปรับค่าใน Inspector หรือ Enemy Parry Editor ได้
[Serializable]
public class EnemyStrike
{
    [Range(0f, 1f), Tooltip("จังหวะที่อาวุธฟันถึง หรือปล่อย Projectile (สัดส่วนของคลิป)")]
    public float time;
    [Tooltip("ทิศที่ผู้เล่นต้องลาก A = คำนวณจากการเคลื่อนที่ของอาวุธ (ไม่ใช้ถ้าศัตรูสุ่มทิศ)")]
    [ParryDirectionPad(true)]
    public int direction = ParryDirections.Auto;
    [Tooltip("ทิศที่ปลายอาวุธเคลื่อนที่ในพื้นที่ของโมเดล ใช้คำนวณทิศ Parry แบบอัตโนมัติ")]
    public Vector3 swing;
}

[Serializable]
public class EnemyMove
{
    public string state;
    public float length;
    [Tooltip("แก้ใน Enemy Parry Editor แล้ว เมนู Build Enemy Animators จะไม่เขียนทับจังหวะของท่านี้")]
    public bool edited;
    public List<EnemyStrike> strikes = new List<EnemyStrike>();
}

public enum EnemyAttackDelivery { Melee, Projectile }
public enum EnemyBasicAttackMode { RandomCount, Chain }

public class EnemyAnimationSet : ScriptableObject
{
    [Header("โมเดล")]
    public GameObject model;
    public RuntimeAnimatorController controller;
    public string idleState = "Idle";
    [Tooltip("ท่าเจ็บ เว้นว่าง = สะดุ้งอย่างเดียว")]
    public string hurtState;
    [Tooltip("ชื่อ Mesh ของอาวุธใน FBX ใช้หาจังหวะฟันและปลายอาวุธ")]
    public string weaponMeshName = "Blade";
    public string handBone = "DEF-hand.R";
    public string hipsBone = "DEF-spine1";

    [Header("ขนาดและตำแหน่ง")]
    [Tooltip("ความสูงของศัตรูในฉาก (หน่วยโลก)")]
    public float targetHeight = 6f;
    public float modelHeight = 1.7f;
    [Tooltip("พื้นอยู่ต่ำกว่าจุดเกิดกี่หน่วย")]
    public float groundBelowSpawn = 2.1f;

    [Header("การโจมตีปกติ")]
    [Tooltip("Melee = ฟันถึงตัว, Projectile = ปล่อยกระสุนวิ่งไปหาผู้เล่น")]
    public EnemyAttackDelivery delivery = EnemyAttackDelivery.Melee;
    public List<EnemyMove> basicAttacks = new List<EnemyMove>();
    [Tooltip("RandomCount = สุ่มท่า 1-2 ที, Chain = ตีต่อตามโอกาส (บอส)")]
    public EnemyBasicAttackMode basicMode = EnemyBasicAttackMode.RandomCount;
    public int basicMinMoves = 1;
    public int basicMaxMoves = 2;
    [Range(0f, 1f), Tooltip("Chain: โอกาสตีต่อหลังท่าแรก")]
    public float chainChance = 0.7f;
    [Range(0f, 1f), Tooltip("Chain: โอกาสที่ลดลงทุกครั้งที่ตีต่อ")]
    public float chainDecay = 0.1f;
    [Tooltip("สุ่มทิศ Parry ทุกครั้ง แทนการคำนวณจากอาวุธ")]
    public bool randomDirection = true;

    [Header("สกิล")]
    public string skillName = "Skill";
    [FormerlySerializedAs("dance")] public EnemyMove skill;
    [Range(0f, 1f), Tooltip("โอกาสใช้สกิลแทนการตีปกติ (ไม่ใช้ติดกัน 2 เทิร์น)")]
    [FormerlySerializedAs("danceChance")] public float skillChance = 0.3f;

    [Header("คำราม (เฉพาะตัวที่มี BossSummonBehavior)")]
    public string rageState;
    public float rageLength = 4f;
    [Range(0f, 1f), Tooltip("จังหวะคำรามที่ลูกน้องปรากฏ")]
    public float rageSummonTime = 0.5f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public GameObject projectileImpactPrefab;
    [Tooltip("เวลาบินจากมือถึงผู้เล่น (วินาที) ช่วง Parry เปิดตอนกระสุนใกล้ถึง")]
    public float projectileFlightTime = 0.7f;
    public float projectileArc = 1.5f;
    public float projectileScale = 1f;

    [Header("การ Parry")]
    [Tooltip("เปิดช่วง Parry ก่อนอาวุธฟันถึงกี่วินาที")]
    public float parryLead = 0.28f;
    public float parryWindow = 0.5f;
    [Tooltip("เวลาขึ้น Hint เตือนสูงสุด")]
    public float warningDuration = 0.1f;
    public float basicDamage = 10f;
    public float basicPosture = 10f;
    [FormerlySerializedAs("danceDamage")] public float skillDamage = 6f;
    [FormerlySerializedAs("dancePosture")] public float skillPosture = 6f;

    [Header("ท่าเจ็บ")]
    [Tooltip("ระยะถอยตอนโดน (หน่วยโลก)")]
    public float hurtKnockback = 0.5f;
    [Tooltip("มุมเอียงตัวตอนโดน (องศา)")]
    public float hurtLean = 14f;

    [Header("ท่ายืนนิ่ง (ไม่ให้ศัตรูหลายตัวขยับพร้อมกัน)")]
    [Range(0f, .4f), Tooltip("ความเร็วท่า Idle ของแต่ละตัวสุ่มต่างจากปกติได้มากสุดกี่ส่วน (0.15 = เร็ว/ช้ากว่ากัน 15%)")]
    public float idleSpeedVariation = .15f;
    [Tooltip("โยกลำตัวแบบสุ่มช้า ๆ ซ้อนบนท่า Idle (องศา)")]
    public float idleNoise = 1.4f;
    [Tooltip("หันหัวมองไปมาแบบสุ่มช้า ๆ (องศา)")]
    public float idleHeadLook = 6f;

    [Header("พรีวิวใน Enemy Parry Editor")]
    [Tooltip("มุมกล้อง Overview ของฉากต่อสู้")]
    public Vector3 previewCameraEuler = new Vector3(15f, 65f, 0f);
    [Tooltip("มุมหันของศัตรูเมื่อยืนที่จุดเกิดในฉาก")]
    [FormerlySerializedAs("previewBossYaw")] public float previewYaw = -90f;

    public bool HasSkill => skill != null && !string.IsNullOrEmpty(skill.state) && skill.strikes.Count > 0;
}
