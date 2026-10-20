using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale Settings")]
    public float normalScale = 1f;
    public float hoverScale = 1.1f;    // ขยายใหญ่ขึ้นตอนเอาเมาส์ชี้
    public float pressedScale = 0.92f; // ยุบตัวลงตอนกดคลิก
    public float animSpeed = 15f;      // ความเร็วและความเด้งในการแอนิเมชัน

    [Header("Color Tint Settings (Optional)")]
    public bool useColorTint = true;
    public Image targetImage;
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(1.1f, 1.1f, 1.1f, 1f); // สว่างขึ้นตอนชี้
    public Color pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); // คล้ำลงตอนกด

    private Vector3 targetScale;
    private Color targetColor;
    private Color currentColor;
    private bool isHovered = false;
    private bool isPressed = false;

    private void Awake()
    {
        if (targetImage == null) targetImage = GetComponent<Image>();

        targetScale = Vector3.one * normalScale;
        transform.localScale = targetScale;

        if (targetImage != null)
        {
            currentColor = normalColor;
            targetColor = normalColor;
            targetImage.color = normalColor;
        }
    }

    private void Update()
    {
        // 1. แอนิเมชันปรับขนาด (Scale Smoothing) โดยใช้ unscaledTime เพื่อให้ขยับได้แม้เกมสะดุด/Hitstop
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animSpeed);

        // 2. แอนิเมชันปรับเปลี่ยนสี (Color Tint Smoothing)
        if (useColorTint && targetImage != null)
        {
            currentColor = Color.Lerp(currentColor, targetColor, Time.unscaledDeltaTime * animSpeed);
            targetImage.color = currentColor;
        }
    }

    // เมื่อเมาส์เริ่มชี้เข้าปุ่ม (Hover)
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        UpdateTargetState();
    }

    // เมื่อเมาส์ออกนอกปุ่ม
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false; // ป้องกันค้างเวลากระชากเมาส์ออก
        UpdateTargetState();
    }

    // เมื่อกดปุ่มคลิกซ้ายค้างไว้ (Press)
    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        UpdateTargetState();
    }

    // เมื่อปล่อยเมาส์จากการคลิก
    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        UpdateTargetState();
    }

    // คำนวณสถานะเป้าหมายตาม Action ของผู้เล่น
    private void UpdateTargetState()
    {
        if (isPressed)
        {
            targetScale = Vector3.one * pressedScale;
            targetColor = pressedColor;
        }
        else if (isHovered)
        {
            targetScale = Vector3.one * hoverScale;
            targetColor = hoverColor;
        }
        else
        {
            targetScale = Vector3.one * normalScale;
            targetColor = normalColor;
        }
    }

    private void OnDisable()
    {
        // รีเซ็ตค่าสเกลกลับปกติทันทีเมื่อปุ่มถูกปิดใช้งาน (เช่น ปิดหน้าต่างเมนู)
        transform.localScale = Vector3.one * normalScale;
        targetScale = Vector3.one * normalScale;
        isHovered = false;
        isPressed = false;
    }
}