using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// หน้าเลือกสกิลของอาวุธ 1 ชิ้น (อยู่ในฉาก: CampScene > Canvas > LoadoutPanel > SkillSelectPanel)
// เปิดจากปุ่ม SKILLS ใต้ช่องอาวุธในหน้า Loadout (WeaponCarousel.OnClickSkills)
// อาวุธแต่ละชิ้นมี 6 สกิล (WeaponData.skillPool) เลือกพกเข้าฉากต่อสู้ได้สูงสุด 3 (WeaponData.MaxEquippedSkills)
// ผลการเลือกเก็บใน PlayerData.skillLoadouts แล้ว NodeSelectionManager ส่งเข้า BattleRequest.equippedSkills
[RequireComponent(typeof(CanvasGroup))]
public class SkillLoadoutPanel : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("ว่างได้ ถ้าว่างจะใช้ PlayerData ที่ส่งมาตอน Open() หรือของ GameManager")]
    public PlayerData playerData;

    [Header("Header")]
    public TextMeshProUGUI weaponNameText;
    public Image weaponIcon;
    public Button backButton;

    [Header("Cards")]
    public Transform cardContainer;
    [Tooltip("การ์ดต้นแบบ (ปิดไว้ในฉาก) จะถูกก๊อป 1 ใบต่อ 1 สกิล")]
    public SkillCardView cardTemplate;

    [Header("Footer")]
    public TextMeshProUGUI summaryText;
    public TextMeshProUGUI messageText;

    [Header("Hide While Open")]
    [Tooltip("ซ่อนระหว่างเปิดหน้านี้ (ช่องอาวุธ/ปุ่มปิดของหน้า Loadout)\nฟอนต์ LiberationSans SDF ใช้ Shader แบบ Overlay ตัวหนังสือด้านหลังจึงทะลุมาทับหน้านี้ถ้าไม่ซ่อน")]
    public GameObject[] hideWhileOpen;

    [Header("Card Style")]
    public Color cardColor = new Color(0.14f, 0.16f, 0.21f, 1f);
    public Color cardEquippedColor = new Color(0.18f, 0.3f, 0.2f, 1f);
    public Color accentColor = new Color(1f, 0.84f, 0.35f);
    public Color textColor = Color.white;
    public Color mutedColor = new Color(0.72f, 0.74f, 0.8f);

    [Header("Animation")]
    public float fadeDuration = 0.15f;
    public float messageDuration = 2.5f;

    private WeaponData currentWeapon;
    private CanvasGroup canvasGroup;
    private float messageTimer;
    private readonly List<SkillCardView> spawnedCards = new List<SkillCardView>();
    private readonly List<GameObject> hiddenObjects = new List<GameObject>();

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (backButton != null) backButton.onClick.AddListener(Close);
        if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
        SetMessageAlpha(0f);
    }

    private void Update()
    {
        if (messageTimer <= 0f) return;
        messageTimer -= Time.unscaledDeltaTime;
        SetMessageAlpha(Mathf.Clamp01(messageTimer));
    }

    // ==========================================
    // เปิด/ปิด
    // ==========================================
    public void Open(WeaponData weapon, PlayerData data = null)
    {
        if (weapon == null) return;

        if (data != null) playerData = data;
        if (playerData == null && GameManager.Instance != null) playerData = GameManager.Instance.playerData;
        if (playerData == null)
        {
            Debug.LogWarning("SkillLoadoutPanel: ไม่พบ PlayerData");
            return;
        }

        currentWeapon = weapon;
        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // ลอยทับทุกอย่างในหน้า Loadout
        HideBackground();

        Refresh();

        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    public void Close()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    // ปิดหน้านี้ด้วยวิธีไหนก็ตาม (ปุ่ม BACK หรือ Panel แม่ถูกปิด) ให้คืนของที่ซ่อนไว้เสมอ
    private void OnDisable()
    {
        foreach (GameObject obj in hiddenObjects)
        {
            if (obj != null) obj.SetActive(true);
        }
        hiddenObjects.Clear();
    }

    private void HideBackground()
    {
        if (hideWhileOpen == null) return;
        foreach (GameObject obj in hideWhileOpen)
        {
            if (obj == null || !obj.activeSelf) continue;
            obj.SetActive(false);
            hiddenObjects.Add(obj);
        }
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;

        float time = 0f;
        canvasGroup.alpha = 0f;
        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(time / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    // ==========================================
    // เลือก/ถอดสกิล
    // ==========================================
    private void ToggleSkill(WeaponSkill skill)
    {
        List<WeaponSkill> equipped = playerData.GetEquippedSkills(currentWeapon);

        if (equipped.Contains(skill))
        {
            equipped.Remove(skill);
        }
        else if (equipped.Count >= WeaponData.MaxEquippedSkills)
        {
            ShowMessage($"You can equip up to {WeaponData.MaxEquippedSkills} skills. Unequip one first.");
            return;
        }
        else
        {
            equipped.Add(skill);
        }

        // เรียงตามลำดับใน Pool เพื่อให้เมนูสกิลในฉากต่อสู้เรียงเหมือนเดิมเสมอ
        List<WeaponSkill> pool = currentWeapon.skillPool;
        equipped.Sort((a, b) => pool.IndexOf(a).CompareTo(pool.IndexOf(b)));

        playerData.SetEquippedSkills(currentWeapon, equipped);
        Refresh();
    }

    // ==========================================
    // อัปเดตหน้าจอ
    // ==========================================
    private void Refresh()
    {
        if (currentWeapon == null) return;

        if (weaponNameText != null) weaponNameText.text = currentWeapon.weaponName;
        if (weaponIcon != null)
        {
            weaponIcon.sprite = currentWeapon.weaponIcon;
            weaponIcon.enabled = currentWeapon.weaponIcon != null;
        }

        foreach (SkillCardView card in spawnedCards)
        {
            if (card != null) Destroy(card.gameObject);
        }
        spawnedCards.Clear();

        List<WeaponSkill> equipped = playerData.GetEquippedSkills(currentWeapon);
        foreach (WeaponSkill skill in currentWeapon.skillPool)
        {
            if (skill != null) spawnedCards.Add(CreateCard(skill, equipped.Contains(skill)));
        }

        if (summaryText != null)
        {
            List<string> names = equipped.ConvertAll(s => s.skillName);
            summaryText.text = $"Equipped {equipped.Count}/{WeaponData.MaxEquippedSkills}   |   " +
                               (names.Count > 0 ? string.Join(",  ", names) : "<color=#FF8888>No skills equipped</color>");
        }
    }

    private SkillCardView CreateCard(WeaponSkill skill, bool isEquipped)
    {
        SkillCardView card = Instantiate(cardTemplate, cardContainer);
        card.name = "Card_" + skill.skillName;
        card.gameObject.SetActive(true);

        if (card.background != null) card.background.color = isEquipped ? cardEquippedColor : cardColor;
        if (card.equippedOutline != null) card.equippedOutline.enabled = isEquipped;

        if (card.titleText != null)
        {
            card.titleText.text = skill.skillName;
            card.titleText.color = isEquipped ? accentColor : textColor;
        }

        if (card.infoText != null)
        {
            string posture = skill.postureRating == SkillPostureRating.None ? "-" : skill.postureRating.ToString();
            card.infoText.text = $"<color=#7FD4FF>AP {skill.apCost}</color>     Posture: {posture}" + (skill.useQTE ? "     QTE" : "");
        }

        if (card.descriptionText != null) card.descriptionText.text = skill.description;

        if (card.stateText != null)
        {
            card.stateText.text = isEquipped ? "EQUIPPED  (click to remove)" : "Click to equip";
            card.stateText.color = isEquipped ? accentColor : mutedColor;
            card.stateText.fontStyle = isEquipped ? FontStyles.Bold : FontStyles.Italic;
        }

        if (card.button != null)
        {
            card.button.onClick.RemoveAllListeners();
            card.button.onClick.AddListener(() => ToggleSkill(skill));
        }
        return card;
    }

    private void ShowMessage(string message)
    {
        if (messageText == null) return;
        messageText.text = message;
        messageTimer = messageDuration;
        SetMessageAlpha(1f);
    }

    private void SetMessageAlpha(float alpha)
    {
        if (messageText == null) return;
        Color c = messageText.color;
        c.a = alpha;
        messageText.color = c;
    }
}
