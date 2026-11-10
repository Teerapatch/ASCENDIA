using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ปุ่มเมนูหน้าแรกของ Camp: ตอบสนองตอนชี้ กด และคลิก ค่าที่ปรับได้อยู่บน CampArtUI
public class CampMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    private const float SpringStiffness = 380f;
    private const float SpringDamping = 18f;
    private const float FadeSpeed = 16f;
    private const float HoverKick = 1.5f;
    private const float ClickKick = 6f;
    private const float LabelHoverScale = 1.06f;
    private const float GlowBaseScale = 1.04f;
    private const float GlowPulseAmount = 0.02f;
    private const float GlowPulseSpeed = 6f;
    private const float BurstDuration = 0.4f;
    private const float BurstScale = 1.35f;

    private CampArtUI owner;
    private System.Action onClick;
    private Image frame;
    private Image label;
    private Image glow;
    private Color frameBaseColor;
    private Vector3 baseScale;
    private Vector3 labelBaseScale;
    private bool hovered;
    private bool pressed;
    private bool isClicking;
    private float scale = 1f;
    private float scaleVelocity;
    private float highlight;

    public void Setup(CampArtUI art, Vector4 raycastPadding, System.Action clickAction)
    {
        owner = art;
        onClick = clickAction;
        baseScale = transform.localScale;

        frame = GetComponent<Image>();
        frame.raycastTarget = true;
        frame.raycastPadding = raycastPadding;
        frameBaseColor = frame.color;

        label = transform.Find("Image (1)").GetComponent<Image>();
        labelBaseScale = label.transform.localScale;

        // แสงรอบตัวหนังสือคือรูปเดียวกันวางไว้ด้านหลัง
        glow = CloneLabel("LabelGlow");
        glow.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
    }

    private Image CloneLabel(string cloneName)
    {
        GameObject copy = Instantiate(label.gameObject, transform);
        copy.name = cloneName;
        Image image = copy.GetComponent<Image>();
        image.raycastTarget = false;
        Color hidden = owner.buttonGlowColor;
        hidden.a = 0f;
        image.color = hidden;
        return image;
    }

    private void Update()
    {
        bool active = hovered || isClicking;
        float targetScale = pressed ? owner.buttonPressScale : active ? owner.buttonHoverScale : 1f;

        // สปริงให้ขนาดเด้งเกินแล้วกลับ
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
        scaleVelocity += (targetScale - scale) * SpringStiffness * dt;
        scaleVelocity *= Mathf.Exp(-SpringDamping * dt);
        scale += scaleVelocity * dt;
        transform.localScale = baseScale * scale;

        highlight = Mathf.Lerp(highlight, active ? 1f : 0f, 1f - Mathf.Exp(-FadeSpeed * dt));

        Color frameColor = frameBaseColor * Mathf.Lerp(owner.buttonIdleBrightness, 1f, highlight);
        frameColor.a = frameBaseColor.a;
        frame.color = frameColor;

        label.color = Color.Lerp(owner.buttonLabelNormal, owner.buttonLabelHover, highlight);
        label.transform.localScale = labelBaseScale * Mathf.Lerp(1f, LabelHoverScale, highlight);

        Color glowColor = owner.buttonGlowColor;
        glowColor.a *= highlight;
        glow.color = glowColor;
        float pulse = GlowBaseScale + Mathf.Sin(Time.unscaledTime * GlowPulseSpeed) * GlowPulseAmount;
        glow.transform.localScale = label.transform.localScale * pulse;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        scaleVelocity += HoverKick;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || isClicking) return;
        StartCoroutine(ClickRoutine());
        StartCoroutine(Burst());
    }

    // หน่วงคำสั่งไว้นิดหนึ่ง ผู้เล่นจะได้เห็นปุ่มตอบสนองก่อนเปลี่ยนหน้า
    private IEnumerator ClickRoutine()
    {
        isClicking = true;
        scaleVelocity += ClickKick;
        yield return new WaitForSecondsRealtime(owner.buttonClickDelay);
        onClick?.Invoke();
        isClicking = false;
    }

    private IEnumerator Burst()
    {
        Image burst = CloneLabel("Burst");
        Vector3 startScale = label.transform.localScale;
        Color color = owner.buttonLabelHover;
        float elapsed = 0f;
        while (elapsed < BurstDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / BurstDuration);
            burst.transform.localScale = startScale * Mathf.Lerp(1f, BurstScale, 1f - (1f - t) * (1f - t));
            color.a = 1f - t;
            burst.color = color;
            yield return null;
        }
        Destroy(burst.gameObject);
    }
}
