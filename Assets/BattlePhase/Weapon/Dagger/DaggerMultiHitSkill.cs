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

    [Tooltip("ดาเมจ Posture ต่อฮิต = ดาเมจต่อฮิต x ค่านี้")]
    public float postureRatio = 0.3f;

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

        // 2. รวมดาเมจทุกฮิตเป็นก้อนเดียว PlayerCombatController จะแบ่งลงตามจังหวะฟันของท่า
        int totalDamage = Mathf.RoundToInt(currentHitDamage) * numberOfHits;
        player.DealDamage(target, totalDamage, totalDamage * postureRatio);
        player.AddComboStack(comboStacksPerHit * numberOfHits, true);
    }
}
