using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// หน้าจบการต่อสู้ชุดใหม่ (ภาพพู่กันของอาร์ต) วางใน TurnBase_Phase เป็น BattleResult_UI (Tools/ASCENDIA/UI/Build Battle Result Screen)
// แต่งใน Hierarchy ได้ โค้ดหาชิ้นจากชื่อ: Dim, Banner/Stroke, Banner/Title, Banner/Subtitle, Rewards/Header, Rewards/List,
// Rewards/RewardTemplate (Frame, Icon, Name, Amount), Btn_Continue/Label  ถ้ามีในฉาก TempGameFlowUI จะใช้ตัวนี้แทน Victory/GameOver Panel เดิม
public class BattleResultScreen : MonoBehaviour
{
    public static BattleResultScreen Instance { get; private set; }

    [Header("ข้อความ")]
    public string victoryTitle = "VICTORY";
    public string defeatTitle = "DEFEAT";

    [Header("สี")]
    public Color victoryTitleColor = new Color(0.92f, 0.95f, 1f);
    public Color defeatTitleColor = new Color(1f, 0.42f, 0.42f);
    public Color victoryStrokeTint = new Color(0.45f, 0.6f, 1f, 0.45f);
    public Color defeatStrokeTint = new Color(0.85f, 0.2f, 0.25f, 0.45f);
    public Color penaltyColor = new Color(1f, 0.5f, 0.5f);

    [Header("จังหวะ (วินาที เวลาจริง)")]
    public float dimTime = 0.35f;
    public float strokeTime = 0.45f;
    public float titleTime = 0.4f;
    public float rewardStagger = 0.12f;

    private CanvasGroup group, dim, subtitleGroup, buttonGroup, rewardsGroup;
    private Image stroke;
    private TMP_Text title, subtitle, header, continueLabel;
    private RectTransform titleRect, list, buttonRect;
    private GameObject rewardTemplate;
    private Button continueButton;
    private System.Action onContinue;
    private readonly List<GameObject> spawned = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
        group = GetOrAdd<CanvasGroup>(gameObject);
        dim = Child<CanvasGroup>("Dim", true);
        stroke = Child<Image>("Banner/Stroke");
        title = Child<TMP_Text>("Banner/Title");
        titleRect = title != null ? title.rectTransform : null;
        subtitle = Child<TMP_Text>("Banner/Subtitle");
        subtitleGroup = subtitle != null ? GetOrAdd<CanvasGroup>(subtitle.gameObject) : null;
        rewardsGroup = Child<CanvasGroup>("Rewards", true);
        header = Child<TMP_Text>("Rewards/Header");
        list = transform.Find("Rewards/List") as RectTransform;
        Transform template = transform.Find("Rewards/RewardTemplate");
        rewardTemplate = template != null ? template.gameObject : null;
        if (rewardTemplate != null) rewardTemplate.SetActive(false);
        continueButton = Child<Button>("Btn_Continue");
        buttonRect = continueButton != null ? (RectTransform)continueButton.transform : null;
        buttonGroup = continueButton != null ? GetOrAdd<CanvasGroup>(continueButton.gameObject) : null;
        continueLabel = Child<TMP_Text>("Btn_Continue/Label");
        if (continueButton != null) continueButton.onClick.AddListener(() => onContinue?.Invoke());

        if (stroke != null) { stroke.type = Image.Type.Filled; stroke.fillMethod = Image.FillMethod.Horizontal; stroke.fillOrigin = (int)Image.OriginHorizontal.Left; }
        Hide();
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private T Child<T>(string path, bool add = false) where T : Component
    {
        Transform t = transform.Find(path);
        if (t == null) return null;
        T c = t.GetComponent<T>();
        if (c == null && add) c = t.gameObject.AddComponent<T>();
        return c;
    }

    private void Hide()
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    // คืนข้อความบนปุ่ม (TempGameFlowUI ใช้นับถอยหลังกลับฉากเดิม)
    public TMP_Text Show(BattleSummary summary, string penaltyText, string continueText, System.Action continueAction)
    {
        onContinue = continueAction;
        bool win = summary.outcome == BattleOutcome.Victory;

        if (title != null) { title.text = win ? victoryTitle : defeatTitle; title.color = win ? victoryTitleColor : defeatTitleColor; }
        if (stroke != null) stroke.color = win ? victoryStrokeTint : defeatStrokeTint;
        if (subtitle != null)
            subtitle.text = $"ENEMIES {summary.enemiesDefeated}   |   WAVES {summary.wavesCleared}/{summary.totalWaves}   |   TURNS {summary.turnsTaken}   |   BROKEN {summary.weaponsBroken}/{summary.totalWeapons}";
        if (continueLabel != null) continueLabel.text = continueText;

        foreach (GameObject old in spawned) if (old != null) Destroy(old);
        spawned.Clear();
        if (win)
        {
            if (header != null) { header.text = summary.rewards.Count > 0 ? "REWARDS" : "NO REWARDS"; header.color = Color.white; }
            foreach (ItemReward reward in summary.rewards) AddReward(reward.item != null ? reward.item.icon : null, reward.item != null ? reward.item.itemName : "?", "x" + reward.amount, Color.white);
        }
        else
        {
            if (header != null) { header.text = "PENALTY"; header.color = penaltyColor; }
            if (!string.IsNullOrEmpty(penaltyText)) AddReward(null, penaltyText, "", penaltyColor);
        }

        group.blocksRaycasts = true;
        group.interactable = true;
        StopAllCoroutines();
        StartCoroutine(Reveal());
        return continueLabel;
    }

    private void AddReward(Sprite iconSprite, string name, string amount, Color color)
    {
        if (rewardTemplate == null || list == null) return;
        GameObject item = Instantiate(rewardTemplate, list);
        item.SetActive(true);
        spawned.Add(item);
        Transform icon = item.transform.Find("Icon");
        if (icon != null)
        {
            Image image = icon.GetComponent<Image>();
            image.sprite = iconSprite;
            icon.gameObject.SetActive(iconSprite != null);
        }
        TMP_Text nameText = item.transform.Find("Name") != null ? item.transform.Find("Name").GetComponent<TMP_Text>() : null;
        if (nameText != null) { nameText.text = name; nameText.color = color; }
        TMP_Text amountText = item.transform.Find("Amount") != null ? item.transform.Find("Amount").GetComponent<TMP_Text>() : null;
        if (amountText != null) amountText.text = amount;
        GetOrAdd<CanvasGroup>(item).alpha = 0f;
    }

    private static float Ease(float x) { x = Mathf.Clamp01(x); return 1f - Mathf.Pow(1f - x, 3f); }
    private static float Back(float x) { x = Mathf.Clamp01(x); const float c1 = 1.70158f, c3 = c1 + 1f; float k = x - 1f; return 1f + c3 * k * k * k + c1 * k * k; }

    // ม่านมืดลง -> พู่กันปาดออก -> หัวข้อกระแทกเข้า -> สถิติ -> ของรางวัลทีละชิ้น -> ปุ่ม (เวลาจริง เผื่อ Hitstop ค้าง timeScale)
    private IEnumerator Reveal()
    {
        group.alpha = 1f;
        if (dim != null) dim.alpha = 0f;
        if (stroke != null) stroke.fillAmount = 0f;
        if (title != null) title.alpha = 0f;
        if (subtitleGroup != null) subtitleGroup.alpha = 0f;
        if (rewardsGroup != null) rewardsGroup.alpha = 0f;
        if (buttonGroup != null) buttonGroup.alpha = 0f;
        Vector2 buttonPos = buttonRect != null ? buttonRect.anchoredPosition : Vector2.zero;

        float rewardsStart = dimTime + strokeTime * 0.6f + titleTime;
        float end = rewardsStart + rewardStagger * spawned.Count + 0.8f;
        for (float t = 0f; t < end; t += Time.unscaledDeltaTime)
        {
            if (dim != null) dim.alpha = Ease(t / dimTime);
            if (stroke != null) stroke.fillAmount = Ease((t - dimTime * 0.5f) / strokeTime);

            float tt = (t - dimTime - strokeTime * 0.3f) / titleTime;
            if (title != null)
            {
                title.alpha = Ease(tt);
                titleRect.localScale = Vector3.one * Mathf.LerpUnclamped(1.35f, 1f, Back(tt));
                title.characterSpacing = Mathf.Lerp(40f, 12f, Ease(tt));
            }
            if (subtitleGroup != null) subtitleGroup.alpha = Ease((t - dimTime - strokeTime * 0.6f - titleTime * 0.5f) / 0.4f);
            if (rewardsGroup != null) rewardsGroup.alpha = Ease((t - rewardsStart) / 0.3f);
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] == null) continue;
                float k = (t - rewardsStart - i * rewardStagger) / 0.35f;
                spawned[i].GetComponent<CanvasGroup>().alpha = Ease(k);
                spawned[i].transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, Back(k));
            }
            float bk = Ease((t - rewardsStart - rewardStagger * spawned.Count - 0.1f) / 0.4f);
            if (buttonGroup != null) buttonGroup.alpha = bk;
            if (buttonRect != null) buttonRect.anchoredPosition = buttonPos + Vector2.down * 30f * (1f - bk);
            yield return null;
        }
        if (title != null) { title.alpha = 1f; titleRect.localScale = Vector3.one; }
        if (buttonRect != null) buttonRect.anchoredPosition = buttonPos;
    }
}
