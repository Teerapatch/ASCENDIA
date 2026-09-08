using UnityEngine;
using System.Collections.Generic;

// สร้าง Struct สำหรับเก็บข้อมูลท่าโจมตีแต่ละท่า
[System.Serializable]
public struct EnemyAttack
{
    public string attackName;
    public int damage;
    public Vector2 parryDirection; // ทิศทางที่ผู้เล่นต้องปัดนิ้วเพื่อหลบ/ปัดป้องท่านี้
}

public class EnemyController : MonoBehaviour
{
    [Header("Base Stats")]
    public string enemyName = "Goblin";
    public float baseSpeed = 80f;
    public int maxHP = 100;
    public int currentHP;

    public int maxPosture = 100; // หลอดความทนทาน
    public int currentPosture;
    public bool isStaggered = false; // ติดสถานะเบรคหรือไม่

    [Header("Attack Patterns")]
    public List<EnemyAttack> attackList; // ใส่ท่าโจมตีใน Inspector ได้เลย

    private void Start()
    {
        currentHP = maxHP; // เซ็ต HP เต็มตอนเริ่มเกม
        currentPosture = maxPosture; // เซ็ต Posture เต็มตอนเริ่มเกม
    }

    // ฟังก์ชันนี้จะถูกเรียกโดย CombatManager เมื่อถึงคิวของศัตรูตัวนี้
    public void StartTurn()
    {
        Debug.Log($"<color=orange>{enemyName}'s Turn!</color>");

        if (isStaggered)
        {
            // ถ้าติด Stagger ให้ข้ามเทิร์นแล้วฟื้นฟู Posture กลับมา
            Debug.Log($"<color=yellow>{enemyName} is STAGGERED and skips a turn!</color>");
            isStaggered = false;
            currentPosture = maxPosture;
            CombatManager.Instance.EndCurrentTurn();
            return;
        }

        // 1. สุ่มเลือกท่าโจมตีจากที่มี (ในอนาคตสามารถเปลี่ยนเป็น AI เลือกท่าตามสถานการณ์ได้)
        int attackIndex = Random.Range(0, attackList.Count);
        EnemyAttack chosenAttack = attackList[attackIndex];

        Debug.Log($"{enemyName} uses {chosenAttack.attackName}!");

        // 2. ส่งข้อมูลท่าโจมตีไปให้ ParryManager ดำเนินการต่อ
        // (ParryManager จะนับเวลาและเป็นตัวสั่งจบเทิร์นเมื่อหมดเวลา หรือผู้เล่นปัดสำเร็จ)
        ParryManager.Instance.StartParryWindow(chosenAttack.parryDirection, chosenAttack.damage, this);
    }

    // ฟังก์ชันรับดาเมจเมื่อผู้เล่นโจมตี
    public void TakeDamage(int damage)
    {
        if (isStaggered) damage = Mathf.RoundToInt(damage * 1.5f);

        currentHP -= damage;
        Debug.Log($"{enemyName} took {damage} damage! (HP: {currentHP}/{maxHP})");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void TakePostureDamage(int postureDamage)
    {
        if (isStaggered) return; // ถ้าเบรคอยู่แล้วไม่ต้องลดอีก

        currentPosture -= postureDamage;
        Debug.Log($"{enemyName} took {postureDamage} Posture Damage! (Posture: {currentPosture}/{maxPosture})");

        if (currentPosture <= 0)
        {
            isStaggered = true;
            Debug.Log($"<color=yellow>!!! {enemyName}'s Posture is BROKEN !!!</color>");
        }
    }

    private void Die()
    {
        Debug.Log($"<color=red>{enemyName} has been defeated!</color>");

        // แจ้ง CombatManager ให้ถอดตัวนี้ออกจากคิวเทิร์น
        CombatManager.Instance.RemoveEnemy(this);

        // ทำลาย Object หรือเล่น Animation ตาย
        Destroy(gameObject);
    }
}