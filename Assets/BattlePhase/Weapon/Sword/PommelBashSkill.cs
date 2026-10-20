using UnityEngine;

[CreateAssetMenu(fileName = "New Pommel Bash", menuName = "Ascendia/Skills/Pommel Bash")]
public class PommelBashSkill : WeaponSkill
{
    [Header("Pommel Bash Settings")]
    public float damageMultiplier = 0.4f;
    public float postureDamageMultiplier = 2.0f;

    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccessCount)
    {
        if (target == null || player == null || player.ActiveWeapon == null) return;

        float baseDmg = player.ActiveWeapon.baseDamage;
        int finalDamage = Mathf.RoundToInt(baseDmg * damageMultiplier);

        int basePosture = Mathf.RoundToInt(baseDmg * 0.5f);
        int finalPosture = Mathf.RoundToInt(basePosture * player.ActiveWeapon.postureDamageMultiplier * postureDamageMultiplier);

        if (useQTE && qteSuccessCount > 0)
        {
            finalDamage = Mathf.RoundToInt(finalDamage * (1f + qteSuccessCount * 0.15f));
            finalPosture = Mathf.RoundToInt(finalPosture * (1f + qteSuccessCount * 0.15f));
        }

        // ทำดาเมจและลดเกจ Posture ของเป้าหมายเดี่ยว
        target.TakeDamage(finalDamage);
        target.TakePostureDamage(finalPosture);

        Debug.Log($"<color=orange>🗡️ POMMEL BASH! Low DMG ({finalDamage}), Massive Posture DMG ({finalPosture})!</color>");

        // สั่นกล้องเบาๆ พอมีแรงกระแทก
        if (CameraShakeManager.Instance != null)
        {
            CameraShakeManager.Instance.Shake(0.7f);
        }
    }
}