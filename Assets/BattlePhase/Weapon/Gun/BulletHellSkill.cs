using System.Collections.Generic;
using UnityEngine;

// ยิงกระจายใส่ศัตรูทุกตัว จำนวนนัด = จำนวนครั้งที่ Gun ยิงสวนด้วย AP ที่ล้น (ไม่ลบ stack)
[CreateAssetMenu(fileName = "New Bullet Hell", menuName = "Ascendia/Skills/Bullet Hell")]
public class BulletHellSkill : WeaponSkill
{
    [Tooltip("ดาเมจต่อนัด = ดาเมจอาวุธ x ค่านี้")]
    public float damagePerHitMultiplier = 0.6f;
    [Tooltip("ดาเมจต่อนัดเพิ่มขึ้นต่อ 1 QTE ที่สำเร็จ (0.2 = +20%)")]
    public float bonusPerQTE = 0.2f;
    [Tooltip("ดาเมจ Posture ต่อนัด = ดาเมจต่อนัด x ค่านี้")]
    public float postureRatio = 0.1f;

    [Header("Hit Count")]
    [Tooltip("จำนวนนัดขั้นต่ำ (ยังไม่เคยยิงสวนเลยก็ยังยิงได้)")]
    public int minHits = 1;
    [Tooltip("จำนวนนัดสูงสุด กันดาเมจพุ่งเกินไป")]
    public int maxHits = 10;

    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccesses)
    {
        int hits = Mathf.Clamp(player.totalGunCounterAttacks, minHits, maxHits);
        float damagePerHit = WeaponDamage(player) * damagePerHitMultiplier * (1f + bonusPerQTE * qteSuccesses);

        Debug.Log($"<color=orange>[{skillName}] ยิง {hits} รอบใส่ศัตรูทุกตัว ({Mathf.RoundToInt(damagePerHit)} ต่อนัด)</color>");
        // รวมดาเมจทุกนัด PlayerCombatController จะแบ่งลงตามจังหวะยิงของท่า
        float total = damagePerHit * hits;
        foreach (EnemyController enemy in new List<EnemyController>(CombatManager.Instance.enemies))
        {
            player.DealDamage(enemy, total, total * postureRatio, WeaponType.Gun);
        }
    }
}
