using TMPro;
using UnityEngine;
using UnityEngine.UI;

// กล่องคำอธิบายของป้ายสถานะ (StatusBadgeRow) แบบพู่กันเหมือน UI อื่นของเกม
// หน้าตาอยู่ที่ Assets/Resources/StatusTooltip.prefab (Tools/ASCENDIA/UI/Build Status Tooltip Prefab สร้างให้ แต่งต่อเองได้)
// ขนาดตามข้อความด้วย VerticalLayoutGroup + ContentSizeFitter บนราก ภาพพู่กัน (Brush / Accent) ไม่ร่วม Layout และยืดตามกรอบ
// ชิ้นไหนไม่มีก็ข้าม: Title / Kind / Detail / Duration (ข้อความ) Accent (ปื้นพู่กันสีของสถานะหลังชื่อ)
public class StatusTooltipView : MonoBehaviour
{
    public TextMeshProUGUI title;
    public TextMeshProUGUI kind;
    public TextMeshProUGUI detail;
    public TextMeshProUGUI duration;
    [Tooltip("ปื้นพู่กันหลังชื่อ ย้อมสีของสถานะ")]
    public Image accent;
    [Range(0f, 1f)] public float accentAlpha = 0.4f;
    public Color buffColor = new Color(0.95f, 0.8f, 0.4f, 1f);
    public Color debuffColor = new Color(0.92f, 0.38f, 0.42f, 1f);

    public void Show(StatusBadgeRow.Info info)
    {
        if (title != null)
        {
            title.text = info.title;
            title.color = Color.Lerp(info.color, Color.white, 0.45f);
        }
        if (kind != null)
        {
            kind.text = info.isBuff ? "BUFF" : "DEBUFF";
            kind.color = info.isBuff ? buffColor : debuffColor;
        }
        if (detail != null)
        {
            detail.text = info.detail;
            detail.gameObject.SetActive(!string.IsNullOrEmpty(info.detail));
        }
        if (duration != null)
        {
            duration.text = info.duration;
            duration.gameObject.SetActive(!string.IsNullOrEmpty(info.duration));
        }
        if (accent != null) accent.color = new Color(info.color.r, info.color.g, info.color.b, accentAlpha);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
    }
}
