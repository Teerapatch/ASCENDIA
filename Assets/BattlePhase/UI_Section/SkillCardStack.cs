using UnityEngine;

// วางการ์ดสกิล (ReArt v2) เรียงเป็นกองเฉียง ใบละ step พิกเซล (BattleUI/Skills_List)
// การ์ดของอาร์ตมี Rect ใหญ่กว่าภาพจริง จึงจัดตำแหน่งเองแทน LayoutGroup
//
// ปรับเองได้ที่คอมโพเนนต์นี้ทั้งหมด (ตำแหน่งกอง = RectTransform ของ Skills_List เอง):
//  - step / columns / columnStep: ระยะระหว่างการ์ด และจำนวนคอลัมน์
//  - cardScale: ขนาดการ์ด (BattleArtUI ใช้ค่านี้ย่อแม่แบบ Skills_Ex แทน skillCardScale)
//  - tiltPerCard / firstTilt: เอียงการ์ด (องศา) ใบแรกเอียง firstTilt ใบถัดไปเพิ่มทีละ tiltPerCard
// พรีวิว: ตอนไม่ได้กด Play จะสร้างการ์ดตัวอย่าง [Preview] previewCount ใบจากแม่แบบ Skills_Ex ให้เห็นกองจริงในหน้า Scene/Game
// การ์ดพรีวิวไม่ถูกบันทึกลงฉาก (HideFlags.DontSave) และถูกลบเองเมื่อกด Play หรือปิด preview
[ExecuteAlways]
public class SkillCardStack : MonoBehaviour
{
    [Header("การจัดวาง")]
    public Vector2 step = new Vector2(40f, -210f);
    [Tooltip("จำนวนคอลัมน์ (1 = กองเดียว) การ์ดเรียงซ้ายไปขวาก่อนแล้วค่อยขึ้นแถวใหม่")]
    public int columns = 1;
    [Tooltip("ระยะระหว่างคอลัมน์")]
    public Vector2 columnStep = new Vector2(430f, 0f);
    [Tooltip("ขนาดการ์ด (สเกลของแม่แบบ Skills_Ex)")]
    public float cardScale = 0.42f;
    [Tooltip("มุมเอียงของการ์ดใบแรก (องศา)")]
    public float firstTilt = 0f;
    [Tooltip("เอียงเพิ่มทีละใบ (องศา)")]
    public float tiltPerCard = 0f;

    [Header("พรีวิวตอนแก้ฉาก (ไม่บันทึก)")]
    public bool preview = true;
    [Range(1, 6)] public int previewCount = 3;
    [Tooltip("แม่แบบการ์ดสำหรับพรีวิว ว่าง = หา Skills_Ex ที่อยู่ข้าง ๆ")]
    public RectTransform previewTemplate;

    private const string PreviewPrefix = "[Preview] ";

    public Vector2 PositionOf(int index)
    {
        int cols = Mathf.Max(1, columns);
        return step * (index / cols) + columnStep * (index % cols);
    }

    public float TiltOf(int index) => firstTilt + tiltPerCard * index;

    private void LateUpdate()
    {
        if (Application.isPlaying) ClearPreview();
        else UpdatePreview();

        int index = 0;
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf) continue;
            RectTransform rect = (RectTransform)child;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = PositionOf(index);
            rect.localRotation = Quaternion.Euler(0f, 0f, TiltOf(index));
            // ตอนเล่นจริงไม่แตะสเกล: ArtButtonHover ขยายการ์ดตอนชี้เมาส์ (สเกลตั้งไว้ที่แม่แบบแล้ว)
            if (!Application.isPlaying) rect.localScale = Vector3.one * cardScale;
            index++;
        }
    }

    private void OnDisable() => ClearPreview();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
#endif

    private RectTransform Template()
    {
        if (previewTemplate != null) return previewTemplate;
        return transform.parent != null ? transform.parent.Find("Skills_Ex") as RectTransform : null;
    }

    private void UpdatePreview()
    {
        // มีการ์ดจริงอยู่แล้ว (ไม่ใช่พรีวิว) = ไม่ต้องพรีวิว
        bool hasReal = false;
        int previews = 0;
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith(PreviewPrefix)) previews++;
            else hasReal = true;
        }
        RectTransform template = Template();
        int wanted = preview && !hasReal && template != null ? previewCount : 0;
        if (previews == wanted) return;

        ClearPreview();
        for (int i = 0; i < wanted; i++)
        {
            GameObject card = Instantiate(template.gameObject, transform, false);
            card.name = PreviewPrefix + "Skill " + (i + 1);
            card.SetActive(true);
            SetHideFlags(card.transform);
        }
    }

    private static void SetHideFlags(Transform root)
    {
        root.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        foreach (Transform child in root) SetHideFlags(child);
    }

    private void ClearPreview()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith(PreviewPrefix)) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
