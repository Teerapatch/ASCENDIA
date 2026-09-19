using UnityEngine;

[CreateAssetMenu(fileName = "NewDamageSkill", menuName = "Ascendia/Skills/Damage Skill")]
public class DamageSkill : WeaponSkill
{
    public float baseDamageMultiplier = 1.5f;
    [Tooltip("ดาเมจโบนัสที่จะบวกเพิ่มต่อ 1 QTE ที่กดสำเร็จ")]
    public float bonusMultiplierPerQTE = 0.5f;

    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccesses)
    {
        float totalMultiplier = baseDamageMultiplier + (bonusMultiplierPerQTE * qteSuccesses);
        int finalDamage = Mathf.RoundToInt(player.ActiveWeapon.baseDamage * totalMultiplier);

        Debug.Log($"{skillName} ทำงาน! สร้างความเสียหาย {finalDamage} แก่ {target.enemyName}");

        target.TakeDamage(finalDamage);
    }
}