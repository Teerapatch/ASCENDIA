using UnityEngine;
using System.Collections.Generic;

public class EnemyController : MonoBehaviour
{
    [Header("Base Stats")]
    public string enemyName = "Goblin";
    public float baseSpeed = 80f;
    public int maxHP = 100;
    public int currentHP;

    public int maxPosture = 100;
    public int currentPosture;
    public bool isStaggered = false;

    [Header("Attack Patterns (Multi-Hit)")]
    // ใช้โครงสร้างใหม่ที่เก็บข้อมูลหลายฮิตแทน
    public List<EnemyAttackPattern> attackPatterns;

    [Header("Visual Effects")]
    public ParticleSystem staggerParticle;

    private void Start()
    {
        currentHP = maxHP;
        currentPosture = maxPosture;
    }

    public void StartTurn()
    {
        Debug.Log($"<color=orange>{enemyName}'s Turn!</color>");

        if (isStaggered)
        {
            Debug.Log($"<color=yellow>{enemyName} is STAGGERED and skips a turn!</color>");
            isStaggered = false;
            currentPosture = maxPosture;

            if (staggerParticle != null)
            {
                staggerParticle.Stop();
            }

            CombatManager.Instance.EndCurrentTurn();
            return;
        }

        // 1. สุ่มท่าโจมตีจากชุดคอมโบ
        int attackIndex = Random.Range(0, attackPatterns.Count);
        EnemyAttackPattern chosenPattern = attackPatterns[attackIndex];

        Debug.Log($"{enemyName} uses {chosenPattern.attackName}!");

        // 2. ส่งทั้งชุดโจมตีไปให้ ParryManager ดำเนินการ
        ParryManager.Instance.StartParrySequence(chosenPattern, this);
    }

    public void TakeDamage(int damage)
    {
        if (isStaggered) damage = Mathf.RoundToInt(damage * 1.5f);

        currentHP -= damage;
        Debug.Log($"{enemyName} took {damage} damage! (HP: {currentHP}/{maxHP})");

        Color dmgColor = isStaggered ? Color.yellow : Color.white;
        DamagePopupManager.Instance.CreatePopup(transform.position, damage, isStaggered, dmgColor);

        if (currentHP <= 0) Die();
    }

    public void TakePostureDamage(int postureDamage)
    {
        if (isStaggered) return;

        currentPosture -= postureDamage;
        Debug.Log($"{enemyName} took {postureDamage} Posture Damage! (Posture: {currentPosture}/{maxPosture})");

        if (currentPosture <= 0)
        {
            isStaggered = true;
            Debug.Log($"<color=yellow>!!! {enemyName}'s Posture is BROKEN !!!</color>");

            if (staggerParticle != null)
            {
                staggerParticle.Play();
            }
        }
    }

    private void Die()
    {
        if (staggerParticle != null) staggerParticle.Stop();
        Debug.Log($"<color=red>{enemyName} has been defeated!</color>");
        CombatManager.Instance.RemoveEnemy(this);
        Destroy(gameObject);
    }
}

// ---------------------------------------------------------
// โครงสร้างข้อมูลท่าโจมตี (วางไว้ด้านล่างของไฟล์ หรือแยกไฟล์ก็ได้)
// ---------------------------------------------------------
[System.Serializable]
public class ParryStrike
{
    [Header("Timing (ซิงก์กับ Animation)")]
    [Tooltip("หน่วงเวลาก่อนฟันดาบนี้ (วินาที)")]
    public float delayBeforeStrike = 0.5f;

    [Tooltip("เวลาที่ผู้เล่นมีสิทธิ์ Parry (วินาที)")]
    public float parryWindowDuration = 0.8f;

    [Header("Consequences")]
    public float damageIfHit = 15f;
    public float postureDamageIfParried = 10f;

    [Header("Osu Slider Type")]
    [Tooltip("0=ขวา, 1=ซ้าย, 2=ลง, 3=ขึ้น, 4=ขวาบน, 5=ซ้ายบน, 6=ขวาล่าง ,7=ซ้ายล่าง")]
    public int sliderDirectionIndex = 0;
}

[System.Serializable]
public class EnemyAttackPattern
{
    public string attackName = "Triple Slash";
    public List<ParryStrike> strikes = new List<ParryStrike>();
}