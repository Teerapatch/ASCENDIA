using UnityEngine;

// สกิลโจมตีเป้าหมายเดี่ยวแบบทั่วไป (Piercer Shot, Executioner, Quick Draw)
[CreateAssetMenu(fileName = "NewDamageSkill", menuName = "Ascendia/Skills/Damage Skill")]
public class DamageSkill : WeaponSkill
{
    public float baseDamageMultiplier = 1.5f;
    [Tooltip("ดาเมจโบนัสที่จะบวกเพิ่มต่อ 1 QTE ที่กดสำเร็จ")]
    public float bonusMultiplierPerQTE = 0.5f;

    [Header("Posture")]
    [Tooltip("ดาเมจ Posture = ดาเมจที่ทำ x ค่านี้")]
    public float postureRatio = 0f;

    [Header("Extra Effects")]
    [Tooltip("คูณดาเมจเพิ่มถ้าเป้าหมายติด Stagger อยู่ (Executioner = 2)")]
    public float staggeredBonusMultiplier = 1f;
    [Tooltip("คืน AP เมื่อกด QTE สำเร็จอย่างน้อย 1 ครั้ง (Quick Draw = 1)")]
    public int apRefundOnQTESuccess = 0;

    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccesses)
    {
        float totalMultiplier = baseDamageMultiplier + (bonusMultiplierPerQTE * qteSuccesses);
        float damage = WeaponDamage(player) * totalMultiplier;
        if (target.isStaggered) damage *= staggeredBonusMultiplier;

        int dealt = player.DealDamage(target, damage, damage * postureRatio);
        Debug.Log($"{skillName} ทำงาน! สร้างความเสียหาย {dealt} แก่ {target.enemyName}");

        if (apRefundOnQTESuccess > 0 && qteSuccesses > 0)
        {
            player.RestoreAP(apRefundOnQTESuccess);
            player.ShowPopup($"+{apRefundOnQTESuccess} AP", Color.cyan);
        }
    }
}
