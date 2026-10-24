using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class SpellUI : WeaponPassiveUI
{
    [Header("UI References")]
    public TextMeshProUGUI elementText; // ช่องแสดงผลธาตุ

    private int lastElementCount = 0; // ตัวแปรเช็กจำนวนธาตุเพื่อสั่งเล่นอนิเมชัน Pop

    protected override WeaponType TargetWeapon => WeaponType.Spell;

    protected override void RefreshContent(PlayerCombatController player)
    {
        List<ElementType> slots = player.elementSlots;

        if (slots.Count > lastElementCount) Pop();
        lastElementCount = slots.Count;

        if (slots.Count == 0)
        {
            elementText.text = "Elemental Sigil\n<size=120%><color=#888888>[ Empty Slots ]</color></size>";
            return;
        }

        string displayStr = "Elemental Sigil\n<size=130%>";
        foreach (var elem in slots)
        {
            displayStr += $"<color={ElementTypes.GetColorHex(elem)}>[{elem}]</color> ";
        }
        displayStr += "</size>";
        elementText.text = displayStr;
    }

    protected override void OnWeaponHidden()
    {
        lastElementCount = 0;
    }
}
