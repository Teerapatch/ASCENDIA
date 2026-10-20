using UnityEngine;
using TMPro;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasGroup))]
public class SpellUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI elementText; // ช่องแสดงผลธาตุ

    [Header("Animation Settings")]
    public float animSpeed = 12f;
    public float popScale = 1.3f; // ขนาดตอนเด้งรับธาตุใหม่

    private CanvasGroup canvasGroup;
    private float targetAlpha = 0f;
    private int lastElementCount = 0; // ตัวแปรเช็กจำนวนธาตุเพื่อสั่งเล่นอนิเมชัน Pop

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        var player = PlayerCombatController.Instance;
        if (player == null || player.ActiveWeapon == null) return;

        bool isHoldingSpell = player.ActiveWeapon.weaponType == WeaponType.Spell;
        targetAlpha = isHoldingSpell ? 1f : 0f;

        if (isHoldingSpell)
        {
            List<ElementType> slots = player.elementSlots;

            if (slots.Count > lastElementCount)
            {
                transform.localScale = Vector3.one * popScale;
            }
            lastElementCount = slots.Count;

            if (slots.Count == 0)
            {
                elementText.text = "Elemental Sigil\n<size=120%><color=#888888>[ Empty Slots ]</color></size>";
            }
            else
            {
                string displayStr = "Elemental Sigil\n<size=130%>";
                foreach (var elem in slots)
                {
                    string colorHex = GetElementColorHex(elem);
                    displayStr += $"<color={colorHex}>[{elem}]</color> ";
                }
                displayStr += "</size>";
                elementText.text = displayStr;
            }
        }
        else
        {
            lastElementCount = 0;
        }

        if (Mathf.Abs(canvasGroup.alpha - targetAlpha) > 0.005f)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * animSpeed);
        }
        else if (canvasGroup.alpha != targetAlpha)
        {
            canvasGroup.alpha = targetAlpha;
        }

        if (transform.localScale.x > 1.005f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.deltaTime * (animSpeed * 0.5f));
        }
        else if (transform.localScale.x != 1f)
        {
            transform.localScale = Vector3.one;
        }
    }

    private string GetElementColorHex(ElementType type)
    {
        switch (type)
        {
            case ElementType.Fire: return "#FF4444";   // แดง (ไฟ)
            case ElementType.Water: return "#4488FF";  // น้ำเงิน (น้ำ)
            case ElementType.Wind: return "#44FF44";   // เขียว (ลม)
            case ElementType.Earth: return "#FFDD44";  // เหลือง (ดิน)
            default: return "#FFFFFF";
        }
    }
}