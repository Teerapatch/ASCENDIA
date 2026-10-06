using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "New Dagger Skill", menuName = "Ascendia/Skills/Dagger Multi-Hit")]
public class DaggerMultiHitSkill : WeaponSkill
{
    [Header("Multi-Hit Settings")]
    public int numberOfHits = 3;

    [Tooltip("ความเสียหายต่อ 1 ฮิต")]
    public float damagePerHit = 5f;

    [Tooltip("จำนวน Combo Stack ที่จะได้ต่อ 1 ฮิต")]
    public int comboStacksPerHit = 1;

    [Tooltip("หน่วงเวลาระหว่างแต่ละฮิต (วินาที) เพื่อให้แอนิเมชันดูต่อเนื่อง")]
    public float timeBetweenHits = 0.2f;

    [Header("QTE Bonus")]
    public float qteDamagePerHit = 8f;


    // ฟังก์ชันนี้ถูกเรียกจาก PlayerCombatController
    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccessCount)
    {
        // 1. คำนวณความเสียหายต่อฮิต (ถ้า QTE ผ่าน ดาเมจต่อฮิตจะแรงขึ้น)
        float currentHitDamage = damagePerHit;
        if (useQTE && qteSuccessCount > 0)
        {
            currentHitDamage = qteDamagePerHit;
            Debug.Log($"<color=yellow>QTE Success! Damage per hit increased to {qteDamagePerHit}</color>");
        }

        // 2. เรียก Coroutine ให้ตัวละครทำแอ็กชันโจมตีหลายครั้ง
        player.StartCoroutine(PerformMultiHit(player, target, currentHitDamage));
    }

    private IEnumerator PerformMultiHit(PlayerCombatController player, EnemyController target, float hitDamage)
    {
        int finalDamagePerHit = Mathf.RoundToInt(hitDamage);
        int postureDamagePerHit = Mathf.RoundToInt(finalDamagePerHit * 0.3f); // ลดเกจเหลือง 30% ของดาเมจ

        for (int i = 0; i < numberOfHits; i++)
        {
            // หยุดถ้าศัตรูตายไปก่อนจะฟันครบ
            if (target == null || target.currentHP <= 0)
            {
                Debug.Log($"<color=gray>Target died before all hits connected. Stopped at hit {i}.</color>");
                break;
            }

            Debug.Log($"<color=orange>[{skillName}] Hit {i + 1} / {numberOfHits} !</color>");

            // --- 1. ทำดาเมจใส่ศัตรู ---
            target.TakeDamage(finalDamagePerHit);
            target.TakePostureDamage(postureDamagePerHit);

            // --- 2. [Passive: Dagger] เพิ่ม Combo Stack ตามจำนวนที่ตั้งไว้ ---
            //player.AddComboStack(comboStacksPerHit);
            player.AddComboStack(comboStacksPerHit, true);

            // --- 3. [Visual Feedback] สั่นกล้องเบาๆ ทุกฮิต ---
            if (CameraShakeManager.Instance != null)
            {
                // ฮิตสุดท้ายสั่นแรงหน่อย
                float shakeForce = (i == numberOfHits - 1) ? 0.6f : 0.3f;
                CameraShakeManager.Instance.Shake(shakeForce);
            }

            // (Optional: ใส่โค้ดเรียก Particle Effect เอฟเฟกต์ฟันตรงนี้)
            // (Optional: ใส่โค้ดเล่นเสียงฟันตรงนี้)

            // หน่วงเวลารอฮิตต่อไป
            yield return new WaitForSeconds(timeBetweenHits);
        }

        Debug.Log($"<color=magenta>[{skillName}] Finished! Total Combo Stacks gained: {numberOfHits * comboStacksPerHit}</color>");
    }
}