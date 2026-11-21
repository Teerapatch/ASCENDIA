using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// การ์ดเส้นทางหนึ่งใบใน NodePath_RE_UI (อาร์ต ReArt v2): ชื่อ/คำอธิบาย/ไอคอนตาม Node
// ชี้แล้วขยาย คลิกแล้วเลือก (เครื่องหมาย Selected) การยืนยันอยู่ที่ปุ่ม Confirm ของ NodeSelectionManager
public class NodeCardButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public float hoverScale = 1.06f;
    public float selectedScale = 1.1f;
    public float chosenScale = 1.2f;

    public PathNode Node { get; private set; }

    private NodeSelectionManager manager;
    private GameObject selectedMark;
    private CanvasGroup group;
    private Vector3 baseScale;
    private float targetScale = 1f;
    private bool hovered, selected, locked;

    public void Setup(PathNode node, NodeSelectionManager owner)
    {
        Node = node;
        manager = owner;
        baseScale = transform.localScale;
        group = gameObject.GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        Transform frame = transform.Find("CardFrame");
        if (frame == null) frame = transform;
        SetText(frame, "Topic", node.DisplayName);
        SetText(frame, "Desc", node.Description);
        Transform icon = frame.Find("Image (1)/Image");
        if (icon != null && node.nodeIcon != null) icon.GetComponent<Image>().sprite = node.nodeIcon;

        // กราฟิกที่คัดจากอาร์ตปิด Raycast ไว้ เปิดเฉพาะกรอบการ์ดให้คลิกได้
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        Image hit = frame.GetComponent<Image>();
        if (hit != null) hit.raycastTarget = true;

        Transform mark = transform.Find("Selected");
        selectedMark = mark != null ? mark.gameObject : null;
        SetSelected(false);
    }

    private static void SetText(Transform root, string child, string value)
    {
        Transform target = root.Find(child);
        TMP_Text text = target != null ? target.GetComponent<TMP_Text>() : null;
        if (text != null) text.text = value;
    }

    public void SetSelected(bool value)
    {
        selected = value;
        if (selectedMark != null) selectedMark.SetActive(value);
        UpdateTarget();
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; UpdateTarget(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; UpdateTarget(); }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (locked || manager == null) return;
        manager.SelectCard(Node);
    }

    private void UpdateTarget()
    {
        if (locked) return;
        targetScale = selected ? selectedScale : hovered ? hoverScale : 1f;
    }

    // ยืนยันแล้ว: ใบที่เลือกขยายเด่น ใบอื่นจางหาย
    public void ShowResult(bool chosen)
    {
        locked = true;
        targetScale = chosen ? chosenScale : 0.9f;
        StartCoroutine(Fade(chosen ? 1f : 0.15f));
    }

    private IEnumerator Fade(float alpha)
    {
        float from = group.alpha;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, alpha, t / 0.25f);
            yield return null;
        }
        group.alpha = alpha;
    }

    private void Update()
    {
        float current = transform.localScale.x / Mathf.Max(0.0001f, baseScale.x);
        float next = Mathf.Lerp(current, targetScale, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        transform.localScale = baseScale * next;
    }
}
