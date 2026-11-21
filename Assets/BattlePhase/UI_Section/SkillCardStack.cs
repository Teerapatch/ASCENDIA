using UnityEngine;

// วางการ์ดสกิล (ReArt v2) เรียงเป็นกองเฉียง ใบละ step พิกเซล
// การ์ดของอาร์ตมี Rect ใหญ่กว่าภาพจริง จึงจัดตำแหน่งเองแทน LayoutGroup
public class SkillCardStack : MonoBehaviour
{
    public Vector2 step = new Vector2(40f, -210f);

    private void LateUpdate()
    {
        int index = 0;
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf) continue;
            RectTransform rect = (RectTransform)child;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = step * index;
            index++;
        }
    }
}
