using System.Collections.Generic;
using UnityEngine;

public enum StatusSkillTarget { Self, Target, AllEnemies }

// สกิลใส่บัฟ/ดีบัฟ (ทำดาเมจร่วมด้วยได้)
// Vanish Blade (Evasion), Expose Weakness, Berserk, Unyielding (Shatter), Trick Shot (Slow)
[CreateAssetMenu(fileName = "New Status Skill", menuName = "Ascendia/Skills/Status Skill")]
public class StatusSkill : WeaponSkill
{
    [Header("Status")]
    public StatusSkillTarget applyTo = StatusSkillTarget.Target;
    public CombatStatusType status = CombatStatusType.ExposeWeakness;
    [Tooltip("ความแรง (0.3 = 30%) / จำนวนครั้งสำหรับ Evasion")]
    public float value = 0.3f;
    [Tooltip("จำนวนเทิร์นของตัวที่ติดสถานะ (-1 = อยู่จนกว่าจะถูกใช้ เช่น Evasion / Shatter)")]
    public int turns = 2;
    [Tooltip("ข้อความลอยตอนติดสถานะ")]
    public string popupText = "EXPOSED";

    [Header("Damage (ไม่บังคับ)")]
    [Tooltip("ดาเมจ = ดาเมจอาวุธ x ค่านี้ (0 = ไม่ทำดาเมจ)")]
    public float damageMultiplier = 0f;
    [Tooltip("ดาเมจ Posture = ดาเมจอาวุธ x ค่านี้ (0 = ไม่ทำ)")]
    public float postureMultiplier = 0f;
    [Tooltip("ดาเมจ/Posture เพิ่มขึ้นต่อ 1 QTE ที่สำเร็จ (0.25 = +25%)")]
    public float bonusPerQTE = 0.25f;

    private static readonly Color DebuffColor = new Color(0.8f, 0.5f, 1f);

    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccesses)
    {
        if (applyTo == StatusSkillTarget.Self)
        {
            player.ApplySelfStatus(status, value, turns, popupText);
            Debug.Log($"<color=yellow>[{skillName}] ได้รับ {status} ({value}) {turns} เทิร์น</color>");
            return;
        }

        float qteBonus = 1f + bonusPerQTE * qteSuccesses;
        float damage = WeaponDamage(player) * damageMultiplier * qteBonus;
        float posture = WeaponDamage(player) * postureMultiplier * qteBonus;

        List<EnemyController> targets = applyTo == StatusSkillTarget.AllEnemies
            ? new List<EnemyController>(CombatManager.Instance.enemies)
            : new List<EnemyController> { target };

        foreach (EnemyController enemy in targets)
        {
            if (enemy == null || enemy.IsDead) continue;

            player.DealDamage(enemy, damage, posture);
            if (enemy.IsDead) continue;

            enemy.Statuses.Apply(status, value, turns);
            if (DamagePopupManager.Instance != null)
                DamagePopupManager.Instance.CreateTextPopup(enemy.transform.position + Vector3.up, popupText, DebuffColor);

            Debug.Log($"<color=magenta>[{skillName}] {enemy.enemyName} ติด {status} ({value}) {turns} เทิร์น</color>");
        }

        // Slow เปลี่ยนความเร็ว -> ลำดับเทิร์นที่คาดการณ์ไว้เปลี่ยนตาม
        if (status == CombatStatusType.Slow && CombatManager.Instance != null)
            CombatManager.Instance.RefreshTurnOrderUI();
    }
}
