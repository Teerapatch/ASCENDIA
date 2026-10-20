using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Mighty Cleave", menuName = "Ascendia/Skills/Mighty Cleave")]
public class MightyCleaveSkill : WeaponSkill
{
    [Header("Mighty Cleave Settings")]
    [Tooltip("ตัวคูณดาเมจสำหรับศัตรูทุกตัว")]
    public float damageMultiplier = 1.1f;
    [Tooltip("ตัวคูณ Posture สำหรับศัตรูทุกตัว")]
    public float postureDamageMultiplier = 1.2f;

    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccessCount)
    {
        if (player == null || player.ActiveWeapon == null || CombatManager.Instance == null) return;

        float baseDmg = player.ActiveWeapon.baseDamage;
        int finalDamage = Mathf.RoundToInt(baseDmg * damageMultiplier);
        int finalPosture = Mathf.RoundToInt(baseDmg * 0.5f * player.ActiveWeapon.postureDamageMultiplier * postureDamageMultiplier);

        if (useQTE && qteSuccessCount > 0)
        {
            finalDamage = Mathf.RoundToInt(finalDamage * (1f + qteSuccessCount * 0.2f));
            finalPosture = Mathf.RoundToInt(finalPosture * (1f + qteSuccessCount * 0.2f));
        }

        // ดึงรายชื่อศัตรูทั้งหมดที่ยังมีชีวิตอยู่ในฉากจาก CombatManager
        List<EnemyController> allEnemies = new List<EnemyController>(CombatManager.Instance.enemies);

        int hitCount = 0;
        foreach (var enemy in allEnemies)
        {
            if (enemy != null)
            {
                enemy.TakeDamage(finalDamage);
                enemy.TakePostureDamage(finalPosture);
                hitCount++;
            }
        }

        Debug.Log($"<color=orange> MIGHTY CLEAVE! Swept all {hitCount} enemies for {finalDamage} DMG each!</color>");

        // สั่นกล้องแรงสะใจเพราะเป็นการโจมตีวงกว้าง
        if (CameraShakeManager.Instance != null)
        {
            CameraShakeManager.Instance.Shake(1.5f);
        }
    }
}