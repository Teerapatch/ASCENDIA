using UnityEngine;

// คลาสแม่ของ UI Passive ประจำอาวุธ (Dagger/Gun/LongSword/Spell)
// จัดการ Fade เข้า/ออกตามอาวุธที่ถือ และ Pop Effect ให้ คลาสลูกแค่อัปเดตข้อความของตัวเอง
[RequireComponent(typeof(CanvasGroup))]
public abstract class WeaponPassiveUI : MonoBehaviour
{
    [Header("Animation Settings")]
    public float animSpeed = 12f;
    public float popScale = 1.4f;

    private CanvasGroup canvasGroup;

    protected abstract WeaponType TargetWeapon { get; }

    // ถูกเรียกทุกเฟรมขณะที่ผู้เล่นถืออาวุธประเภทนี้อยู่
    protected virtual void RefreshContent(PlayerCombatController player) { }

    // ถูกเรียกทุกเฟรมขณะที่ผู้เล่นถืออาวุธประเภทอื่น
    protected virtual void OnWeaponHidden() { }

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    protected void Pop()
    {
        transform.localScale = Vector3.one * popScale;
    }

    private void Update()
    {
        var player = PlayerCombatController.Instance;
        if (player == null || player.ActiveWeapon == null) return;

        bool isHolding = player.ActiveWeapon.weaponType == TargetWeapon;
        if (isHolding) RefreshContent(player);
        else OnWeaponHidden();

        AnimateTowards(isHolding ? 1f : 0f);
    }

    private void AnimateTowards(float targetAlpha)
    {
        // ทำ Fade เฉพาะตอนที่ค่ายังไม่ถึงเป้าหมาย ใกล้มากแล้วให้ล็อกค่าเป๊ะๆ แล้วหยุดคำนวณ
        if (Mathf.Abs(canvasGroup.alpha - targetAlpha) > 0.005f)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * animSpeed);
        }
        else if (canvasGroup.alpha != targetAlpha)
        {
            canvasGroup.alpha = targetAlpha;
        }

        // ทำ Pop Effect เฉพาะตอนที่มันยังใหญ่กว่าปกติ (1.0)
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
