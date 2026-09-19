using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance;

    [Header("Menu Panels")]
    public CanvasGroup actionMenuPanel;
    public CanvasGroup skillMenuPanel;

    [Header("Skill List Generation")]
    public GameObject skillButtonPrefab;
    public Transform skillListContainer;

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
    }

    public void ShowActionMenu()
    {
        HideMenu(skillMenuPanel);
        PlayMenuAnimation(actionMenuPanel, true);
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

            TextMeshProUGUI[] texts = btnObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = skill.skillName;
            if (texts.Length > 1) texts[1].text = $"{skill.apCost} AP";

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