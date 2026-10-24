using UnityEngine;
using TMPro;

public class DaggerComboUI : WeaponPassiveUI
{
    [Header("UI References")]
    public TextMeshProUGUI comboText;

    private int lastComboCount = -1;

    protected override WeaponType TargetWeapon => WeaponType.Dagger;

    protected override void RefreshContent(PlayerCombatController player)
    {
        // อัปเดต Text เฉพาะจังหวะที่แต้มเปลี่ยนเท่านั้น
        int currentCombo = player.currentComboStacks;
        if (currentCombo == lastComboCount) return;

        comboText.text = $"COMBO\n<size=150%>{currentCombo}</size>";
        Pop();
        lastComboCount = currentCombo;
    }
}
