using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// หน้าสรุปผลการต่อสู้: ของรางวัลที่ได้ (ไอคอน / ชื่อ / จำนวน) + สถิติการต่อสู้
// วางไว้บน Victory/GameOver Panel เองเพื่อปรับค่าใน Inspector ได้ ถ้าไม่ได้วาง TempGameFlowUI จะ AddComponent ให้
// Layout ถูกสร้างจากโค้ดตอน Show() จึงไม่ต้องแก้ฉาก
public class BattleSummaryUI : MonoBehaviour
{
    [Header("Layout (วัดจากกึ่งกลางของ Panel)")]
    public Vector2 anchoredPosition = new Vector2(0f, 185f);
    public Vector2 size = new Vector2(640f, 230f);
    public float rowSpacing = 6f;
    public float iconSize = 32f;

    [Header("Style")]
    [Tooltip("ว่าง = ใช้ฟอนต์ของข้อความตัวเล็กสุดใน Panel (ฟอนต์ของปุ่ม) ฟอนต์ปัจจุบันไม่มีภาษาไทย")]
    public TMP_FontAsset font;
    public float headerFontSize = 30f;
    public float rowFontSize = 26f;
    public float statsFontSize = 20f;
    public Color headerColor = new Color(1f, 0.84f, 0.35f);
    public Color textColor = Color.white;
    public Color amountColor = new Color(0.55f, 1f, 0.55f);
    public Color mutedColor = new Color(0.75f, 0.75f, 0.75f);
    public Color penaltyColor = new Color(1f, 0.45f, 0.45f);

    [Header("Animation")]
    public float rowFadeDuration = 0.2f;
    public float rowRevealInterval = 0.12f;

    private RectTransform root;
    private readonly List<CanvasGroup> rows = new List<CanvasGroup>();

    public void Show(BattleSummary summary, string defeatPenaltyText)
    {
        EnsureRoot();
        Clear();

        if (summary.outcome == BattleOutcome.Victory)
        {
            AddTextRow("REWARDS", headerFontSize, headerColor, FontStyles.Bold);
            if (summary.rewards.Count == 0) AddTextRow("No rewards", rowFontSize, mutedColor);
            foreach (ItemReward reward in summary.rewards) AddRewardRow(reward);
        }
        else
        {
            AddTextRow("NO REWARDS", headerFontSize, mutedColor, FontStyles.Bold);
            if (!string.IsNullOrEmpty(defeatPenaltyText)) AddTextRow(defeatPenaltyText, rowFontSize, penaltyColor, FontStyles.Bold);
        }

        AddTextRow(BuildStatsLine(summary), statsFontSize, mutedColor);

        StopAllCoroutines();
        StartCoroutine(RevealRows());
    }

    private static string BuildStatsLine(BattleSummary s)
    {
        // ใช้ | คั่นแทนอักขระพิเศษ เผื่อฟอนต์ไม่มี Glyph
        return $"Enemies {s.enemiesDefeated}  |  Waves {s.wavesCleared}/{s.totalWaves}  |  Turns {s.turnsTaken}  |  Weapons broken {s.weaponsBroken}/{s.totalWeapons}";
    }

    // ==========================================
    // สร้าง Layout
    // ==========================================
    private void EnsureRoot()
    {
        if (root != null) return;
        if (font == null) font = FindPanelFont();

        GameObject go = new GameObject("BattleSummary", typeof(RectTransform), typeof(VerticalLayoutGroup));
        root = (RectTransform)go.transform;
        root.SetParent(transform, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = anchoredPosition;
        root.sizeDelta = size;

        VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = rowSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private TMP_FontAsset FindPanelFont()
    {
        TMP_Text smallest = null;
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (smallest == null || text.fontSize < smallest.fontSize) smallest = text;
        }
        return smallest != null ? smallest.font : null;
    }

    private void Clear()
    {
        foreach (Transform child in root) Destroy(child.gameObject);
        rows.Clear();
    }

    private CanvasGroup CreateRow(string rowName)
    {
        GameObject go = new GameObject(rowName, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(root, false);

        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        rows.Add(group);
        return group;
    }

    private void AddTextRow(string text, float fontSize, Color color, FontStyles style = FontStyles.Normal)
    {
        CanvasGroup row = CreateRow("Row_Text");
        SetupText(row.gameObject.AddComponent<TextMeshProUGUI>(), text, fontSize, color, style);
    }

    // [ไอคอน] ชื่อไอเทม  xจำนวน
    private void AddRewardRow(ItemReward reward)
    {
        CanvasGroup row = CreateRow("Row_" + reward.item.itemName);

        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (reward.item.icon != null)
        {
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconObj.transform.SetParent(row.transform, false);

            Image icon = iconObj.GetComponent<Image>();
            icon.sprite = reward.item.icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            LayoutElement element = iconObj.GetComponent<LayoutElement>();
            element.preferredWidth = iconSize;
            element.preferredHeight = iconSize;
        }

        SetupText(CreateChildText(row.transform, "Name"), reward.item.itemName, rowFontSize, textColor, FontStyles.Normal);
        SetupText(CreateChildText(row.transform, "Amount"), $"x{reward.amount}", rowFontSize, amountColor, FontStyles.Bold);
    }

    private static TextMeshProUGUI CreateChildText(Transform parent, string objectName)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<TextMeshProUGUI>();
    }

    private void SetupText(TextMeshProUGUI tmp, string text, float fontSize, Color color, FontStyles style)
    {
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    // ค่อยๆ โชว์ทีละแถว (ใช้เวลาจริง เพราะ Hitstop อาจหยุด timeScale ไว้ตอนตีตัวสุดท้าย)
    private IEnumerator RevealRows()
    {
        foreach (CanvasGroup row in rows)
        {
            float time = 0f;
            while (time < rowFadeDuration)
            {
                time += Time.unscaledDeltaTime;
                if (row != null) row.alpha = Mathf.Clamp01(time / rowFadeDuration);
                yield return null;
            }
            if (row != null) row.alpha = 1f;

            yield return new WaitForSecondsRealtime(rowRevealInterval);
        }
    }
}
