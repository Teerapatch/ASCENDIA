using UnityEngine;

// วางปุ่มเปลี่ยนอาวุธเป็นวงรอบตัวผู้เล่นบนจอ (ตามตำแหน่งโมเดลที่มองเห็นทุกเฟรม กล้องเคลื่อนก็ตามไป)
// ใบแรกอยู่ด้านบน แล้วเวียนตามเข็มนาฬิกา ระยะห่างเท่ากันตามจำนวนอาวุธ ตอนหน้านี้ค่อย ๆ โผล่ วงจะกางออกจากตัวผู้เล่น
[RequireComponent(typeof(RectTransform))]
public class WeaponRingLayout : MonoBehaviour
{
    [Tooltip("รัศมีวง (อ้างอิง 1920x1080)")]
    public float radius = 320f;
    [Tooltip("รัศมีตอนเริ่มโผล่ (สัดส่วนของรัศมีเต็ม)")]
    [Range(0f, 1f)] public float openFrom = 0.45f;
    [Tooltip("มุมของใบแรก (90 = ด้านบนตัวผู้เล่น)")]
    public float startAngle = 90f;
    [Tooltip("ใส่แล้ววงล้อมวัตถุนี้แทนตัวผู้เล่น (ว่าง = โมเดลผู้เล่นที่มองเห็น)")]
    public Transform followTarget;
    [Tooltip("จุดกลางวงสูงจากเท้าผู้เล่นกี่หน่วยโลก")]
    public float playerCenterHeight = 3.2f;
    [Tooltip("เลื่อนจุดกลางวงบนจอเพิ่ม (อ้างอิง 1920x1080)")]
    public Vector2 screenOffset = new Vector2(30f, 0f);
    [Tooltip("วงรี: ยืด/บีบแนวตั้ง (1 = วงกลม)")]
    public float verticalScale = 0.9f;

    private RectTransform rect;
    private CanvasGroup group;
    private Canvas canvas;

    private void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
    }

    private void LateUpdate()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();

        Vector2 center = Vector2.zero;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 world = followTarget != null ? followTarget.position
                : PlayerBattleAnimator.Instance != null ? PlayerBattleAnimator.Instance.ModelPosition
                : PlayerCombatController.Instance != null ? PlayerCombatController.Instance.transform.position : Vector3.zero;
            world += Vector3.up * playerCenterHeight;
            Vector3 screen = cam.WorldToScreenPoint(world);
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (screen.z > 0f) RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, uiCamera, out center);
        }
        center += screenOffset;

        int count = 0;
        foreach (Transform child in transform) if (child.gameObject.activeSelf) count++;
        if (count == 0) return;

        // เปิดหน้า (alpha 0 -> 1) = วงกางออก
        float open = group != null ? group.alpha : 1f;
        float r = radius * Mathf.Lerp(openFrom, 1f, 1f - (1f - open) * (1f - open));
        int index = 0;
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf) continue;
            float angle = (startAngle - 360f * index / count) * Mathf.Deg2Rad;
            RectTransform item = (RectTransform)child;
            item.anchorMin = item.anchorMax = new Vector2(0.5f, 0.5f);
            item.anchoredPosition = count == 1
                ? center + Vector2.up * r
                : center + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * verticalScale);
            index++;
        }
    }
}
