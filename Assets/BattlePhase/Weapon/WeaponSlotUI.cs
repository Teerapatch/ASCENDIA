using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponSlotUI : MonoBehaviour
{
    public Image weaponImage;
    public TextMeshProUGUI durabilityText;

    [Header("Durability Bar")]
    public Image durabilityFillImage; // [เพิ่มใหม่] ภาพหลอดความทนทาน

    [Header("Colors (Persona 5 Style)")]
    public Color normalColor = Color.white;
    public Color brokenColor = new Color(1f, 0.2f, 0.2f);
    public Color inactiveColor = new Color(0.7f, 0.7f, 0.7f);

    public Color gaugeColor = new Color(1f, 0.8f, 0.2f);

    [Header("Animation")]
    public float animSpeed = 12f;
    public float activeScale = 1.15f;
    public float activeYOffset = 15f;

    private RectTransform rect;
    private float targetScale = 1f;
    private float targetY = 0f;

    private float targetFill = 1f; // [เพิ่มใหม่] เป้าหมายความยาวของหลอด

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void Setup(WeaponData data, bool isActive)
    {
        if (data == null) return;

        weaponImage.sprite = data.weaponIcon;

        float currentDura = Mathf.Max(0, data.currentDurability);
        durabilityText.text = $"{currentDura}/{data.maxDurability}";

        if (data.maxDurability > 0)
            targetFill = currentDura / data.maxDurability;
        else
            targetFill = 0;

        bool isBroken = currentDura <= 0;

        // 1. ตั้งค่าสี (เปลี่ยนสีหลอดด้วย)
        if (isBroken)
        {
            weaponImage.color = brokenColor;
            durabilityText.color = brokenColor;
            if (durabilityFillImage != null) durabilityFillImage.color = brokenColor;
        }
        else
        {
            weaponImage.color = isActive ? normalColor : inactiveColor;
            durabilityText.color = normalColor;
            if (durabilityFillImage != null) durabilityFillImage.color = isActive ? gaugeColor : inactiveColor;
        }

        // 2. ตั้งค่าเป้าหมายแอนิเมชันขยายตัว
        if (isActive && !isBroken)
        {
            targetScale = activeScale;
            targetY = activeYOffset;
        }
        else
        {
            targetScale = 1f;
            targetY = 0f;
        }
    }

    private void Update()
    {
        float newScale = Mathf.Lerp(transform.localScale.x, targetScale, Time.deltaTime * animSpeed);
        transform.localScale = Vector3.one * newScale;

        Vector2 pos = rect.anchoredPosition;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * animSpeed);
        rect.anchoredPosition = pos;

        if (durabilityFillImage != null)
        {
            durabilityFillImage.fillAmount = Mathf.Lerp(durabilityFillImage.fillAmount, targetFill, Time.deltaTime * animSpeed);
        }
    }
}