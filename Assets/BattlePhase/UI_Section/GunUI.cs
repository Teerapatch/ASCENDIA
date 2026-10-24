using UnityEngine;
using TMPro;

public class GunUI : WeaponPassiveUI
{
    [Header("UI References")]
    public TextMeshProUGUI comboText;

    private int lastCounterCount = -1;

    protected override WeaponType TargetWeapon => WeaponType.Gun;

    protected override void RefreshContent(PlayerCombatController player)
    {
        int totalCounter = player.totalGunCounterAttacks;
        if (totalCounter == lastCounterCount) return;

        comboText.text = $"totalCounter\n<size=150%>{totalCounter}</size>";
        lastCounterCount = totalCounter;
    }
}
