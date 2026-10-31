using TMPro;
using UnityEngine;
using UnityEngine.UI;

// การ์ดสกิล 1 ใบในหน้าเลือกสกิล (ต้นแบบอยู่ในฉาก: LoadoutPanel > SkillSelectPanel > CardGrid > SkillCardTemplate)
// SkillLoadoutPanel ก๊อปต้นแบบนี้ 1 ใบต่อ 1 สกิล ปรับหน้าตาที่ต้นแบบในฉากได้เลย
public class SkillCardView : MonoBehaviour
{
    public Button button;
    public Image background;
    [Tooltip("กรอบที่เปิดเฉพาะตอนสกิลถูกเลือกพกอยู่")]
    public Outline equippedOutline;

    [Header("Texts")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI infoText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI stateText;
}
