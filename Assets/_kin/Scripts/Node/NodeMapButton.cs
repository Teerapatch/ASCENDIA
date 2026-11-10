using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ปุ่ม Node บนแผนที่: NodeSelectionManager เพิ่มให้ตอนสร้างแผนที่ ตอบสนองตอนชี้ กด และเลือก
public class NodeMapButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    private enum Result { Waiting, Chosen, Rejected }

    private const float SpringStiffness = 420f;
    private const float SpringDamping = 20f;
    private const float FadeSpeed = 16f;
    private const float ChosenKick = 7f;
    private const float GlowScale = 1.25f;
    private const float BurstDuration = 0.4f;
    private const float BurstScale = 2.4f;
    // รูปกรอบมีขอบโปร่งใสกว้าง จึงรับคลิกเฉพาะส่วนในของรูป
    private const float HitPadding = 30f;

    public PathNode Node { get; private set; }

    private NodeSelectionManager manager;
    private Graphic[] graphics;
    private Color[] baseColors;
    private Image icon;
    private Image glow;
    private Vector3 baseScale;
    private Result result = Result.Waiting;
    private bool hovered;
    private bool pressed;
    private float scale = 1f;
    private float scaleVelocity;
    private float brightness;
    private float glowAmount;

    public void Setup(PathNode pathNode, NodeSelectionManager owner)
    {
        Node = pathNode;
        manager = owner;
        baseScale = transform.localScale;
        brightness = manager.nodeIdleBrightness;

        Image hitArea = GetComponent<Image>();
        if (hitArea == null)
        {
            hitArea = gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
        }
        hitArea.raycastTarget = true;
        hitArea.raycastPadding = Vector4.one * HitPadding;

        // รูปอาร์ตไม่รับคลิก ส่วนพื้นที่กดไม่ต้องเปลี่ยนสี
        var tinted = new System.Collections.Generic.List<Graphic>();
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == hitArea) continue;
            graphic.raycastTarget = false;
            tinted.Add(graphic);
        }
        graphics = tinted.ToArray();
        baseColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++) baseColors[i] = graphics[i].color;

        Transform iconTransform = transform.Find("Icon");
        if (iconTransform == null) return;

        icon = iconTransform.GetComponent<Image>();
        glow = CloneIcon("Glow");
        glow.transform.SetSiblingIndex(icon.transform.GetSiblingIndex());
        glow.transform.localScale = icon.transform.localScale * GlowScale;
    }

    private Image CloneIcon(string cloneName)
    {
        GameObject copy = Instantiate(icon.gameObject, transform);
        copy.name = cloneName;
        Image image = copy.GetComponent<Image>();
        Color hidden = manager.nodeGlowColor;
        hidden.a = 0f;
        image.color = hidden;
        return image;
    }

    public void ShowResult(bool chosen)
    {
        result = chosen ? Result.Chosen : Result.Rejected;
        hovered = false;
        pressed = false;
        if (!chosen) return;

        scaleVelocity += ChosenKick;
        if (icon != null) StartCoroutine(Burst());
    }

    private IEnumerator Burst()
    {
        Image burst = CloneIcon("Burst");
        Vector3 startScale = icon.transform.localScale;
        Color color = manager.nodeGlowColor;
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

    private void Update()
    {
        float targetScale = 1f;
        float targetBrightness = manager.nodeIdleBrightness;
        float targetGlow = 0f;

        if (result == Result.Chosen)
        {
            targetScale = manager.nodeChosenScale;
            targetBrightness = 1f;
            targetGlow = 1f;
        }
        else if (result == Result.Rejected)
        {
            targetScale = manager.nodeRejectedScale;
            targetBrightness = manager.nodeRejectedBrightness;
        }
        else if (hovered)
        {
            targetScale = pressed ? manager.nodePressScale : manager.nodeHoverScale;
            targetBrightness = 1f;
            targetGlow = 1f;
        }

        // สปริงให้ขนาดเด้งเกินแล้วกลับ
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
        scaleVelocity += (targetScale - scale) * SpringStiffness * dt;
        scaleVelocity *= Mathf.Exp(-SpringDamping * dt);
        scale += scaleVelocity * dt;
        transform.localScale = baseScale * scale;

        float blend = 1f - Mathf.Exp(-FadeSpeed * dt);
        brightness = Mathf.Lerp(brightness, targetBrightness, blend);
        glowAmount = Mathf.Lerp(glowAmount, targetGlow, blend);

        for (int i = 0; i < graphics.Length; i++)
        {
            Color color = baseColors[i] * brightness;
            color.a = baseColors[i].a;
            graphics[i].color = color;
        }

        if (glow != null)
        {
            Color color = manager.nodeGlowColor;
            color.a *= glowAmount;
            glow.color = color;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!manager.IsSelecting) return;
        hovered = true;
        manager.SetHoveredNode(Node);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        if (manager.IsSelecting) manager.ClearHoveredNode(Node);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (manager.IsSelecting && eventData.button == PointerEventData.InputButton.Left) pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) manager.ChooseNode(Node);
    }
}
