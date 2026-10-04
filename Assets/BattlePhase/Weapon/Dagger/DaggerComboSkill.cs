using UnityEngine;

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

    // ฟังก์ชันนี้จะถูกเรียกจาก PlayerCombatController เมื่อถึงจังหวะปล่อยสกิล
    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccessCount)
    {
        // 1. สูบแต้ม Combo ออกมาใช้ (ฟังก์ชัน ConsumeComboStacks จะรีเซ็ตค่ากลับเป็น 0 ให้อัตโนมัติ)
        int currentStacks = player.ConsumeComboStacks();

        // 2. คำนวณความเสียหายรวม: ดาเมจฐาน + (แต้มคอมโบ * โบนัส)
        float totalDamage = skillBaseDamage + (currentStacks * bonusDamagePerStack);

        // 3. คอนเฟิร์มผลลัพธ์จาก QTE (สมมติว่าถ้ากดโดน 1 ครั้งขึ้นไป ถือว่าผ่าน)
        bool isCritical = false;
        if (useQTE && qteSuccessCount > 0)
        {
            totalDamage *= qteSuccessMultiplier;
            isCritical = true;
            Debug.Log($"<color=yellow>QTE Perfect! Damage multiplied by {qteSuccessMultiplier}</color>");
        }

        // 4. โจมตีเป้าหมาย
        int finalDamage = Mathf.RoundToInt(totalDamage);
        target.TakeDamage(finalDamage);

        // 5. โบนัสความเสียหายต่อ Posture (ยิ่ง Stack เยอะ ยิ่งลดเกจมึนได้เยอะ)
        int postureDamage = Mathf.RoundToInt(finalDamage * 0.4f); // 40% ของดาเมจจริง
        target.TakePostureDamage(postureDamage);

        // --- [Visual & Game Feel Feedback] ---

        // สั่นกล้อง: ยิ่งสะสม Stack มาเยอะ กล้องยิ่งสั่นกระแทกแรง
        if (CameraShakeManager.Instance != null)
        {
            float shakeForce = 0.5f + (currentStacks * 0.3f); // เริ่มที่ 0.5 และบวก 0.3 ทุกๆ 1 Stack
            CameraShakeManager.Instance.Shake(shakeForce);
        }

        // แจ้งเตือนบน Console (ช่วยคุณเช็กบั๊กตอนเทสต์)
        Debug.Log($"<color=magenta>[{skillName}] Executed! | Stacks Used: {currentStacks} | Total DMG: {finalDamage}</color>");
    }
}