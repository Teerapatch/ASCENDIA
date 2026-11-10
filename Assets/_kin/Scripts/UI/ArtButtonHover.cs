using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ปุ่มจาก UI ของอาร์ต: ตอนเมาส์ชี้จะขยายนิดหน่อย ถ้าระบุตัวหนังสือไว้ ตัวหนังสือจะสว่างขึ้น ขยาย และมีแสงรอบ
public class ArtButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float hoverMultiplier = 1.07f;
    public float labelHoverMultiplier = 1.12f;
    public float speed = 16f;
    public Color labelNormal = new Color(0.72f, 0.78f, 0.9f, 1f);
    public Color labelHover = Color.white;
    public Color glowColor = new Color(0.55f, 0.8f, 1f, 0.8f);

    private Vector3 baseScale;
    private Vector3 labelBaseScale;
    private Image label;
    private Image glow;
    private bool hovered;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void Blend(float t)
    {
        transform.localScale = Vector3.Lerp(transform.localScale, hovered ? baseScale * hoverMultiplier : baseScale, t);
        if (label == null) return;

        label.color = Color.Lerp(label.color, hovered ? labelHover : labelNormal, t);
        label.transform.localScale = Vector3.Lerp(label.transform.localScale, hovered ? labelBaseScale * labelHoverMultiplier : labelBaseScale, t);
        Color target = glowColor;
        target.a = hovered ? glowColor.a : 0f;
        glow.color = Color.Lerp(glow.color, target, t);
        glow.transform.localScale = label.transform.localScale * 1.04f;
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; }

    private void OnDisable()
    {
        hovered = false;
        Blend(1f);
    }

    private void Update()
    {
        Blend(1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
    }

    // ทำให้รูปขนาดใหญ่ที่มีขอบโปร่งใสรับคลิกเฉพาะตรงตัวหนังสือ
    public static Button Make(Transform target, Vector4 raycastPadding, UnityEngine.Events.UnityAction onClick, Image labelImage = null)
    {
        Image image = target.GetComponent<Image>();
        image.raycastTarget = true;
        image.raycastPadding = raycastPadding;
        Button button = target.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        ArtButtonHover hover = target.gameObject.AddComponent<ArtButtonHover>();
        if (labelImage != null) hover.AttachLabel(labelImage);
        return button;
    }

    private void AttachLabel(Image labelImage)
    {
        label = labelImage;
        labelBaseScale = label.transform.localScale;
        label.color = labelNormal;

        // แสงรอบตัวหนังสือคือรูปเดียวกันวางไว้ด้านหลัง
        GameObject copy = Instantiate(label.gameObject, label.transform.parent);
        copy.name = "LabelGlow";
        copy.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
        glow = copy.GetComponent<Image>();
        glow.raycastTarget = false;
        Color hidden = glowColor;
        hidden.a = 0f;
        glow.color = hidden;
    }
}
