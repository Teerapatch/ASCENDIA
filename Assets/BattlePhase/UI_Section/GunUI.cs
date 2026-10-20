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

        bool isHoldingGun = player.ActiveWeapon.weaponType == WeaponType.Gun;
        targetAlpha = isHoldingGun ? 1f : 0f;

        if (isHoldingGun)
        {
            int totalCounter = player.totalGunCounterAttacks;
            comboText.text = $"totalCounter\n<size=150%>{totalCounter}</size>";
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
}