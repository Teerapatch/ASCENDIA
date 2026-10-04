using UnityEngine;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class GunUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI comboText;

    [Header("Animation Settings")]
    public float animSpeed = 12f;
    public float popScale = 1.4f;

    private CanvasGroup canvasGroup;
    private float targetAlpha = 0f;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        var player = PlayerCombatController.Instance;
        if (player == null || player.ActiveWeapon == null) return;

        // CheckState
        bool isHoldingGun = player.ActiveWeapon.weaponType == WeaponType.Gun;
        targetAlpha = isHoldingGun ? 1f : 0f;

        if (isHoldingGun)
        {
            int totalCounter = player.totalGunCounterAttacks;
            comboText.text = $"totalCounter\n<size=150%>{totalCounter}</size>";
        }

        // ==========================================
        // OPTIMIZATION
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