using System.Collections.Generic;
using UnityEngine;

// สกิลที่ดาเมจคูณตาม Combo Stack ของ Dagger (Eviscerate = เป้าเดี่ยว + รีเซ็ต Combo, Dancing Knife = ทุกตัว ไม่รีเซ็ต)
[CreateAssetMenu(fileName = "New Dagger Skill", menuName = "Ascendia/Skills/Dagger Combo Burst")]
public class DaggerComboSkill : WeaponSkill
{
    [Header("Combo Enhancements")]
    [Tooltip("ดาเมจพื้นฐานของสกิล (กรณีไม่มี Stack เลย)")]
    public float skillBaseDamage = 15f;

    [Tooltip("ดาเมจโบนัสที่จะบวกเพิ่มต่อ 1 Combo Stack")]
    public float bonusDamagePerStack = 8f;

    [Tooltip("หากกด QTE สำเร็จ จะคูณความเสียหายรวมกี่เท่า?")]
    public float qteSuccessMultiplier = 1.5f;

    [Header("Targeting & Stacks")]
    [Tooltip("โจมตีศัตรูทุกตัวในฉาก (Dancing Knife)")]
    public bool hitsAllEnemies = false;
    [Tooltip("ใช้แล้วรีเซ็ต Combo กลับเป็น 0 (Eviscerate)")]
    public bool consumeStacks = true;
    [Tooltip("ดาเมจ Posture = ดาเมจที่ทำ x ค่านี้")]
    public float postureRatio = 0.4f;

    // ฟังก์ชันนี้จะถูกเรียกจาก PlayerCombatController เมื่อถึงจังหวะปล่อยสกิล
    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccessCount)
    {
        // 1. ดึงแต้ม Combo (ConsumeComboStacks จะรีเซ็ตเป็น 0 ให้อัตโนมัติ)
        int currentStacks = consumeStacks ? player.ConsumeComboStacks() : player.currentComboStacks;

        // 2. คำนวณความเสียหายรวม: ดาเมจฐาน + (แต้มคอมโบ * โบนัส)
        float totalDamage = skillBaseDamage + (currentStacks * bonusDamagePerStack);

        // 3. คอนเฟิร์มผลลัพธ์จาก QTE (กดโดน 1 ครั้งขึ้นไป ถือว่าผ่าน)
        if (useQTE && qteSuccessCount > 0)
        {
            totalDamage *= qteSuccessMultiplier;
            Debug.Log($"<color=yellow>QTE Perfect! Damage multiplied by {qteSuccessMultiplier}</color>");
        }

        // 4. โจมตีเป้าหมาย
        List<EnemyController> targets = hitsAllEnemies
            ? new List<EnemyController>(CombatManager.Instance.enemies)
            : new List<EnemyController> { target };

        foreach (EnemyController enemy in targets)
        {
            player.DealDamage(enemy, totalDamage, totalDamage * postureRatio);
        }

        // --- [Visual & Game Feel Feedback] ---
        // สั่นกล้อง: ยิ่งสะสม Stack มาเยอะ กล้องยิ่งสั่นกระแทกแรง
        float shakeForce = 0.5f + (currentStacks * 0.3f); // เริ่มที่ 0.5 และบวก 0.3 ทุกๆ 1 Stack
        CameraShakeManager.TryShake(shakeForce);

        Debug.Log($"<color=magenta>[{skillName}] Executed! | Stacks: {currentStacks} (consumed: {consumeStacks}) | Targets: {targets.Count} | DMG each: {Mathf.RoundToInt(totalDamage)}</color>");
    }
}
