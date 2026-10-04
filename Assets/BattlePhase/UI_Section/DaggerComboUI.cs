using UnityEngine;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class DaggerComboUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI comboText;

    [Header("Animation Settings")]
    public float animSpeed = 12f;
    public float popScale = 1.4f;

    private CanvasGroup canvasGroup;
    private float targetAlpha = 0f;
    private int lastComboCount = -1;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        // แคชตัวแปรไว้ใช้เพื่อลดภาระการเรียก Instance ลึกๆ หลายรอบในเฟรมเดียว
        var player = PlayerCombatController.Instance;
        if (player == null || player.ActiveWeapon == null) return;

        // 1. เช็กสถานะ
        bool isHoldingDagger = player.ActiveWeapon.weaponType == WeaponType.Dagger;
        targetAlpha = isHoldingDagger ? 1f : 0f;

        // 2. อัปเดต Text (ทำงานแค่จังหวะที่แต้มเปลี่ยนเท่านั้น ไม่เปลือง CPU)
        if (isHoldingDagger)
        {
            int currentCombo = player.currentComboStacks;
            if (currentCombo != lastComboCount)
            {
                comboText.text = $"COMBO\n<size=150%>{currentCombo}</size>";
                transform.localScale = Vector3.one * popScale;
                lastComboCount = currentCombo;
            }
        }

        // ==========================================
        // 🚀 OPTIMIZATION: บล็อกการคำนวณ Lerp ที่ไม่จำเป็น
        // ==========================================

        // 3. ทำ Fade (Lerp Alpha) เฉพาะตอนที่ค่ายังไม่ถึงเป้าหมาย (ห่างกันเกิน 0.005)
        if (Mathf.Abs(canvasGroup.alpha - targetAlpha) > 0.005f)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * animSpeed);
        }
        else if (canvasGroup.alpha != targetAlpha)
        {
            // ถ้าค่าใกล้เป้าหมายมากแล้ว ให้ล็อกค่าเป๊ะๆ ไปเลย แล้วหยุดทำงาน
            canvasGroup.alpha = targetAlpha;
        }

        // 4. ทำ Pop Effect (Lerp Scale) เฉพาะตอนที่มันยังใหญ่กว่าปกติ (1.0)
        if (transform.localScale.x > 1.005f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.deltaTime * (animSpeed * 0.5f));
        }
        else if (transform.localScale.x != 1f) // ถ้าค่าเหลือ 1.001 หรือใกล้เคียง
        {
            // ล็อกค่าให้กลับมาเป็น 1.0 เป๊ะๆ แล้วหยุดคำนวณ
            transform.localScale = Vector3.one;
        }
    }
}