using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance;

    // ให้ UI ของอาร์ตเล่น Effect ตอนเมนูหลักขึ้น
    public event System.Action ActionMenuShown;

    [Header("Menu Panels")]
    public CanvasGroup actionMenuPanel;
    public CanvasGroup skillMenuPanel;

    [Header("Skill List Generation")]
    public GameObject skillButtonPrefab;
    public Transform skillListContainer;

    [Header("Weapon Switch Menu (ตกแต่งได้ใน Canvas)")]
    [Tooltip("หน้าเลือกอาวุธที่จะสลับไป (ต้องมี CanvasGroup)")]
    public CanvasGroup weaponMenuPanel;
    [Tooltip("ที่วางปุ่มอาวุธ (ใส่ Layout Group ได้)")]
    public Transform weaponListContainer;
    [Tooltip("ปุ่มต้นแบบ 1 อัน (ปิดไว้) ถูกก๊อปปี้ต่ออาวุธ 1 ชิ้น ลูกที่ชื่อ Icon(Image) / Name / Durability / Cost / Status (TMP) จะถูกเติมให้")]
    public GameObject weaponOptionTemplate;
    [Tooltip("ปุ่มย้อนกลับในหน้าเลือกอาวุธ (ไม่บังคับ)")]
    public Button weaponMenuBackButton;
    public Color activeWeaponTint = new Color(1f, 0.85f, 0.4f);
    public Color brokenWeaponTint = new Color(0.45f, 0.45f, 0.45f);

    private readonly List<GameObject> spawnedWeaponOptions = new List<GameObject>();

    [Header("Animation Settings")]
    public float animSpeed = 20f; // ปรับให้ไวขึ้นนิดนึงเพื่อความกระชับ

    // --- [เพิ่มใหม่] ระบบป้องกันแอนิเมชันตีกัน ---
    // เก็บประวัติว่า CanvasGroup ตัวไหน กำลังรัน Coroutine อะไรอยู่
    private Dictionary<CanvasGroup, Coroutine> activeCoroutines = new Dictionary<CanvasGroup, Coroutine>();

    private void Awake()
    {
        Instance = this;
        // ปิดเมนูทั้งหมดตอนเริ่มเกมแบบทันที
        PlayMenuAnimation(actionMenuPanel, false, instant: true);
        PlayMenuAnimation(skillMenuPanel, false, instant: true);
        PlayMenuAnimation(weaponMenuPanel, false, instant: true);

        if (weaponOptionTemplate != null) weaponOptionTemplate.SetActive(false);
        if (weaponMenuBackButton != null)
        {
            weaponMenuBackButton.onClick.AddListener(() =>
            {
                if (PlayerCombatController.Instance != null) PlayerCombatController.Instance.UI_CancelWeaponSwitch();
            });
        }
    }

    public void ShowActionMenu()
    {
        HideMenu(skillMenuPanel);
        HideMenu(weaponMenuPanel);
        PlayMenuAnimation(actionMenuPanel, true);
        ActionMenuShown?.Invoke();
    }

    // หน้าเลือกอาวุธ: ปุ่มละ 1 ชิ้นตามลำดับใน inventory ชิ้นที่ถืออยู่/พังแล้วกดไม่ได้
    public void ShowWeaponMenu(List<WeaponData> weapons, WeaponData activeWeapon, int apCost)
    {
        if (weaponMenuPanel == null || weaponOptionTemplate == null)
        {
            Debug.LogWarning("PlayerUIManager: ยังไม่ได้ใส่ weaponMenuPanel / weaponOptionTemplate");
            return;
        }

        HideMenu(actionMenuPanel);
        HideMenu(skillMenuPanel);

        foreach (GameObject old in spawnedWeaponOptions) if (old != null) Destroy(old);
        spawnedWeaponOptions.Clear();

        Transform container = weaponListContainer != null ? weaponListContainer : weaponOptionTemplate.transform.parent;
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponData weapon = weapons[i];
            int weaponIndex = i;
            bool isActive = weapon == activeWeapon;
            bool isBroken = weapon.currentDurability <= 0f;

            GameObject option = Instantiate(weaponOptionTemplate, container);
            option.name = $"Weapon_{weapon.weaponName}";
            option.SetActive(true);
            spawnedWeaponOptions.Add(option);

            SetText(option, "Name", weapon.weaponName);
            SetText(option, "Durability", $"DUR {Mathf.CeilToInt(Mathf.Max(0f, weapon.currentDurability))}/{Mathf.CeilToInt(weapon.maxDurability)}");
            SetText(option, "Cost", isActive || isBroken ? "" : $"{apCost} AP");
            SetText(option, "Status", isBroken ? "BROKEN" : isActive ? "EQUIPPED" : "");

            Image icon = FindChild<Image>(option, "Icon");
            if (icon != null)
            {
                icon.sprite = weapon.weaponIcon;
                icon.enabled = weapon.weaponIcon != null;
            }

            Button button = option.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = !isActive && !isBroken;
                button.onClick.AddListener(() =>
                {
                    if (PlayerCombatController.Instance != null) PlayerCombatController.Instance.UI_SelectWeapon(weaponIndex);
                });
            }

            Image background = option.GetComponent<Image>();
            if (background != null && (isActive || isBroken))
            {
                background.color *= isActive ? activeWeaponTint : brokenWeaponTint;
            }
        }

        PlayMenuAnimation(weaponMenuPanel, true);
    }

    private static T FindChild<T>(GameObject root, string childName) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component.gameObject.name == childName) return component;
        }
        return null;
    }

    private static void SetText(GameObject root, string childName, string value)
    {
        TextMeshProUGUI text = FindChild<TextMeshProUGUI>(root, childName);
        if (text != null) text.text = value;
    }

    public void ShowSkillMenu(WeaponData currentWeapon)
    {
        HideMenu(actionMenuPanel);

        // 1. ลบปุ่มสกิลเก่าทิ้งให้หมด
        foreach (Transform child in skillListContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. สร้างปุ่มสกิลใหม่ตามอาวุธที่ถือ
        for (int i = 0; i < currentWeapon.availableSkills.Count; i++)
        {
            WeaponSkill skill = currentWeapon.availableSkills[i];
            int skillIndex = i;

            GameObject btnObj = Instantiate(skillButtonPrefab, skillListContainer);
            btnObj.SetActive(true);

            // การ์ดของอาร์ตตั้งชื่อช่องไว้ ปุ่มแบบเดิมใช้ลำดับข้อความ
            TextMeshProUGUI nameText = FindChild<TextMeshProUGUI>(btnObj, "Name");
            if (nameText != null)
            {
                nameText.text = skill.skillName;
                SetText(btnObj, "Cost", skill.apCost.ToString());
                SetText(btnObj, "Description", skill.description);
            }
            else
            {
                TextMeshProUGUI[] texts = btnObj.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length > 0) texts[0].text = skill.skillName;
                if (texts.Length > 1) texts[1].text = $"{skill.apCost} AP";
                if (texts.Length > 2) texts[2].text = $"{skill.description}";
            }

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                PlayerCombatController.Instance.UI_SelectSkill(skillIndex);
            });
        }

        PlayMenuAnimation(skillMenuPanel, true);
    }

    public void HideAllMenus()
    {
        HideMenu(actionMenuPanel);
        HideMenu(skillMenuPanel);
        HideMenu(weaponMenuPanel);
    }

    private void HideMenu(CanvasGroup menu, bool instant = false)
    {
        PlayMenuAnimation(menu, false, instant);
    }

    // ==========================================
    // Core Animation System (ป้องกันการบั๊ก/ค้าง)
    // ==========================================
    private void PlayMenuAnimation(CanvasGroup menu, bool isShowing, bool instant = false)
    {
        if (menu == null) return;

        // 1. ถ้ามีแอนิเมชันเก่ารันอยู่ ให้ "หยุด" ทันที เพื่อไม่ให้มันแย่งกันดึงค่า Alpha
        if (activeCoroutines.ContainsKey(menu) && activeCoroutines[menu] != null)
        {
            StopCoroutine(activeCoroutines[menu]);
        }

        // 2. ปิด/เปิด แบบทันที (ไม่ต้องมีแอนิเมชัน)
        if (instant)
        {
            menu.alpha = isShowing ? 1f : 0f;
            menu.transform.localScale = isShowing ? Vector3.one : Vector3.one * 0.8f;
            menu.interactable = isShowing;
            menu.blocksRaycasts = isShowing;
            return;
        }

        // 3. เริ่มแอนิเมชันใหม่ และบันทึก Coroutine ไว้ใน Dictionary
        activeCoroutines[menu] = StartCoroutine(AnimateMenuRoutine(menu, isShowing));
    }

    private IEnumerator AnimateMenuRoutine(CanvasGroup menu, bool isShowing)
    {
        // เปิดให้กดได้ทันทีถ้ากำลังจะโชว์ หรือ บล็อกการกดทันทีถ้ากำลังจะซ่อน
        menu.interactable = isShowing;
        menu.blocksRaycasts = isShowing;

        float targetAlpha = isShowing ? 1f : 0f;
        Vector3 targetScale = isShowing ? Vector3.one : Vector3.one * 0.8f;

        // [จุดสำคัญ] ถ้าเป็นการเปิดเมนูจากสถานะ "ปิดสนิท" ค่อยให้เด้ง Pop up ขยายร่าง
        // (ป้องกันบั๊กเวลาสลับไปมาไวๆ แล้วสเกลมันเด้งแบบแปลกๆ)
        if (isShowing && menu.alpha < 0.1f)
        {
            menu.transform.localScale = Vector3.one * 1.1f;
        }

        // เล่นแอนิเมชันจนกว่าจะถึงเป้าหมาย
        while (Mathf.Abs(menu.alpha - targetAlpha) > 0.01f || Vector3.Distance(menu.transform.localScale, targetScale) > 0.01f)
        {
            menu.alpha = Mathf.Lerp(menu.alpha, targetAlpha, Time.deltaTime * animSpeed);
            menu.transform.localScale = Vector3.Lerp(menu.transform.localScale, targetScale, Time.deltaTime * animSpeed);
            yield return null;
        }

        // ล็อกค่าให้เป๊ะตอนจบ
        menu.alpha = targetAlpha;
        menu.transform.localScale = targetScale;
    }
}