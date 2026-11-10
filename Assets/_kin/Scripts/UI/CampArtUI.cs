using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ผูก UI ของอาร์ต (CampUI_New คัดมาจาก ENVI_Camp3) เข้ากับเมนูหลักเดิม ปุ่มใหม่ส่งต่อไปยังปุ่มเดิมที่ซ่อนไว้
[DefaultExecutionOrder(-500)]
public class CampArtUI : MonoBehaviour
{
    [Header("Button Feedback")]
    public float buttonHoverScale = 1.1f;
    public float buttonPressScale = 0.94f;
    [Tooltip("เวลาที่ปุ่มแสดงผลก่อนทำคำสั่งจริง (วินาที)")]
    public float buttonClickDelay = 0.18f;
    [Range(0f, 1f), Tooltip("ความสว่างของกรอบปุ่มตอนยังไม่ได้ชี้ (ตอนชี้ = 1)")]
    public float buttonIdleBrightness = 0.8f;
    public Color buttonLabelNormal = new Color(0.72f, 0.78f, 0.9f, 1f);
    public Color buttonLabelHover = Color.white;
    public Color buttonGlowColor = new Color(0.55f, 0.8f, 1f, 0.85f);
    [Tooltip("เวลาเฟดปุ่มเมื่อเปิด/ปิดหน้าอื่น (วินาที)")]
    public float buttonFadeDuration = 0.12f;

    private readonly List<CanvasGroup> buttonGroups = new List<CanvasGroup>();
    private GameObject oldMenu;

    private void Awake()
    {
        CampManager manager = FindAnyObjectByType<CampManager>(FindObjectsInactive.Include);
        if (manager == null || manager.mainMenuPanel == null) return;

        oldMenu = manager.mainMenuPanel;
        Image panel = oldMenu.GetComponent<Image>();
        if (panel != null) panel.enabled = false;

        Bind("Rest", "Btn_Rest", new Vector4(209f, 436f, 126f, 378f));
        Bind("Loadout", "Btn_Loadout", new Vector4(234f, 416f, 166f, 411f));
        Bind("Forge", "Btn_Forge", new Vector4(387f, 480f, 193f, 400f));

        // เมนูเดิมต้องเปิดอยู่ เพราะ CampManager ใช้เปิด/ปิดเป็นสถานะ จึงซ่อนเฉพาะของข้างใน
        foreach (Transform child in oldMenu.transform) child.gameObject.SetActive(false);
    }

    private void Bind(string artName, string oldName, Vector4 padding)
    {
        Transform art = transform.Find(artName);
        Button oldButton = oldMenu.transform.Find(oldName).GetComponent<Button>();
        art.gameObject.AddComponent<CampMenuButton>().Setup(this, padding, () => oldButton.onClick.Invoke());
        buttonGroups.Add(art.gameObject.AddComponent<CanvasGroup>());
    }

    // เมนูอื่น (Loadout/Forge) เปิดเมื่อ MainMenuPanel ถูกปิด: ซ่อนเฉพาะปุ่ม ส่วนพื้นหลังอยู่ต่อใต้หน้านั้น
    private void LateUpdate()
    {
        bool visible = oldMenu != null && oldMenu.activeInHierarchy;
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, buttonFadeDuration);
        foreach (CanvasGroup group in buttonGroups)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, step);
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
