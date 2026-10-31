using UnityEngine;

// คืน AP ตามจำนวน QTE ที่กดสำเร็จ (Tactical Reload)
[CreateAssetMenu(fileName = "New Restore AP Skill", menuName = "Ascendia/Skills/Restore AP")]
public class RestoreAPSkill : WeaponSkill
{
    public int baseAP = 0;
    public int apPerQTESuccess = 1;
    [Tooltip("คืนได้สูงสุดกี่หน่วยต่อการใช้ 1 ครั้ง")]
    public int maxRestore = 3;

    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccesses)
    {
        int amount = Mathf.Clamp(baseAP + apPerQTESuccess * qteSuccesses, 0, maxRestore);

        player.RestoreAP(amount);
        player.ShowPopup($"+{amount} AP", Color.cyan);
        Debug.Log($"<color=cyan>[{skillName}] คืน AP {amount} หน่วย (QTE สำเร็จ {qteSuccesses} ครั้ง)</color>");
    }
}
