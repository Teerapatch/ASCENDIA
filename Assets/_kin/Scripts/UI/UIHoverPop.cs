using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// ปุ่ม/ช่องของ UI อาร์ต: ชี้แล้วขยาย กดแล้วยุบ ปล่อยแล้วเด้ง พร้อมเรียก onClick (ใช้เวลาจริง ไม่ขึ้นกับ timeScale)
// กราฟิกที่คัดมาจากอาร์ตปิด Raycast ไว้ ต้องเปิด raycastTarget ให้กราฟิกที่ใช้รับคลิกเอง
public class UIHoverPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public Transform target;
    public float hoverScale = 1.06f;
    public float pressScale = 0.95f;
    public float selectedScale = 1.1f;
    public UnityEvent onClick = new UnityEvent();
    public bool interactable = true;

    private Vector3 baseScale;
    private bool hovered, pressed, selected;
    private float punch;

    public bool Selected
    {
        get => selected;
        set => selected = value;
    }

    public static UIHoverPop Add(GameObject host, UnityAction click, Transform scaleTarget = null)
    {
        UIHoverPop pop = host.GetComponent<UIHoverPop>();
        if (pop == null) pop = host.AddComponent<UIHoverPop>();
        pop.target = scaleTarget != null ? scaleTarget : host.transform;
        pop.baseScale = pop.target.localScale;
        pop.onClick.RemoveAllListeners();
        if (click != null) pop.onClick.AddListener(click);
        UnityEngine.UI.Graphic graphic = host.GetComponent<UnityEngine.UI.Graphic>();
        if (graphic != null) graphic.raycastTarget = true;
        return pop;
    }

    private void Awake()
    {
        if (target == null) target = transform;
        if (baseScale == Vector3.zero) baseScale = target.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => pressed = interactable;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable) return;
        punch = 1f;
        onClick.Invoke();
    }

    private void OnDisable()
    {
        hovered = pressed = false;
        if (target != null && baseScale != Vector3.zero) target.localScale = baseScale;
    }

    private void Update()
    {
        if (target == null) return;
        float goal = !interactable ? 1f : pressed ? pressScale : selected ? selectedScale : hovered ? hoverScale : 1f;
        punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 5f);
        goal += punch * 0.08f;
        float current = target.localScale.x / Mathf.Max(0.0001f, baseScale.x);
        float next = Mathf.Lerp(current, goal, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
        target.localScale = baseScale * next;
    }
}
