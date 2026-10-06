using UnityEngine;

[CreateAssetMenu(fileName = "New Gun Skill", menuName = "Ascendia/Skills/Gun Target Lock")]
public class GunMarkSkill : WeaponSkill
{
    [Header("Target Lock Settings")]
    [Tooltip("ดาเมจนำร่องตอนยิงแปะมาร์ค (อาจจะเบาๆ แค่พอยั่วโมโห)")]
    public float initialDamage = 5f;

    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccessCount)
    {
        float currentDamage = initialDamage;
        if (useQTE && qteSuccessCount > 0)
        {
            currentDamage *= 1.5f;
            Debug.Log("QTE Success on Target Lock!");
        }

        int finalDamage = Mathf.RoundToInt(currentDamage);
        target.TakeDamage(finalDamage);

        // --- Mark ใส่เป้าหมาย ---
        target.ApplyGunMark();

        if (CameraShakeManager.Instance != null)
        {
            CameraShakeManager.Instance.Shake(0.4f);
        }
    }
}