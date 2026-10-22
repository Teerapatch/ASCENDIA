using UnityEngine;
using UnityEngine.UI;

public class StatusBarUI : MonoBehaviour
{
    [Header("UI Sliders")]
    public Slider staminaBar; // หลอดสีเขียว (อยู่ข้างหน้าสุด)
    public Slider weightBar;  // หลอดสีส้ม (อยู่ข้างหลัง)

    [Header("Juice Settings")]
    public RectTransform mainRect; // ลากตัวแม่ที่คลุมหลอด 2 อันมาใส่ เพื่อทำเด้งดึ๋ง
    public float fillSpeed = 10f;
    public float bounceScale = 1.3f;
    public float bounceRecoverSpeed = 15f;

    private float displayStamina;
    private float displayWeight;
    private float lastStamina;
    private float lastWeight;

    private void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.playerData != null)
        {
            displayStamina = GameManager.Instance.playerData.stamina;
            displayWeight = GameManager.Instance.playerData.weight;
            lastStamina = displayStamina;
            lastWeight = displayWeight;
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerData == null) return;
        PlayerData data = GameManager.Instance.playerData;

        // 🌟 1. ดักจับและทำเด้งดึ๋งเมื่อมีการเปลี่ยนแปลง
        if (data.stamina > lastStamina || data.weight > lastWeight)
        {
            if (mainRect != null) mainRect.localScale = Vector3.one * bounceScale;
        }
        lastStamina = data.stamina;
        lastWeight = data.weight;

        // 🌟 2. ค่อยๆ ปรับตัวเลขแบบสมูท (Lerp)
        displayStamina = Mathf.Lerp(displayStamina, data.stamina, Time.deltaTime * fillSpeed);
        displayWeight = Mathf.Lerp(displayWeight, data.weight, Time.deltaTime * fillSpeed);

        // 🌟 3. อัปเดตหลอดสีเขียว (Stamina ปกติ)
        if (staminaBar != null)
        {
            staminaBar.maxValue = data.maxStamina;
            staminaBar.value = displayStamina;
        }

        // 🌟 4. อัปเดตหลอดสีส้ม (Stamina + น้ำหนักกระเป๋า)
        if (weightBar != null)
        {
            weightBar.maxValue = data.maxStamina; // ใช้ Max เดียวกับ Stamina 
            // หลอดส้มจะยาวเท่ากับเลือดปัจจุบัน + ค่าน้ำหนัก!
            weightBar.value = displayStamina + displayWeight; 
        }

        // 🌟 5. หดขนาด UI กลับเป็นปกติ
        if (mainRect != null)
        {
            mainRect.localScale = Vector3.Lerp(mainRect.localScale, Vector3.one, Time.deltaTime * bounceRecoverSpeed);
        }
    }
}