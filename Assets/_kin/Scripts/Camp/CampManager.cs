using UnityEngine;
using System.Collections;
using TMPro;

public class CampManager : MonoBehaviour
{
    [Header("Core References")]
    public PlayerData playerData; 
    
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject loadoutPanel; 
    public GameObject forgePanel;

    [Header("Resting System")]
    public TextMeshProUGUI systemMessageText; 

    // 🌟 ตัวแปรใหม่สำหรับคุมอนิเมชัน
    private GameObject currentActivePanel;
    private bool isTransitioning = false; // ป้องกันผู้เล่นกดรัวๆ ตอนกำลังเล่นอนิเมชัน

    private void Start()
    {
        // 1. ซ่อนทุกหน้าต่างก่อนเริ่ม
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (loadoutPanel != null) loadoutPanel.SetActive(false);
        if (forgePanel != null) forgePanel.SetActive(false);
        if (systemMessageText != null) systemMessageText.gameObject.SetActive(false);

        // 2. เปิดหน้า Main Menu ทันทีตอนเริ่มฉาก
        currentActivePanel = mainMenuPanel;
        if (currentActivePanel != null)
        {
            currentActivePanel.SetActive(true);
            CanvasGroup cg = GetOrAddCanvasGroup(currentActivePanel);
            cg.alpha = 1f;
            currentActivePanel.transform.localScale = Vector3.one;
        }

        ShowSystemMessage("Welcome to Base Camp");
    }

    // ==========================================
    // 🛠️ ระบบจัดการหน้าต่าง UI เปิด/ปิด (พร้อมอนิเมชัน)
    // ==========================================
    public void ShowPanel(GameObject panelToShow)
    {
        // ถ้ากำลังเล่นอนิเมชันอยู่ หรือกดเปิดหน้าต่างเดิมซ้ำ ให้เพิกเฉยไปเลย
        if (isTransitioning || currentActivePanel == panelToShow) return;
        
        StartCoroutine(AnimatePanelTransition(currentActivePanel, panelToShow));
    }

    private IEnumerator AnimatePanelTransition(GameObject panelToHide, GameObject panelToShow)
    {
        isTransitioning = true;
        float duration = 0.2f; // ความเร็วในการเปิด/ปิดหน้าต่าง (0.2 วินาที)

        // 🌟 1. อนิเมชันยุบปิด (Fade Out + Shrink)
        if (panelToHide != null)
        {
            CanvasGroup hideCg = GetOrAddCanvasGroup(panelToHide);
            Vector3 startScale = Vector3.one;
            Vector3 endScale = Vector3.one * 0.8f; // ยุบขนาดลงเหลือ 80%

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = 1f - Mathf.Pow(1f - t, 3f); // สมูทตอนปลาย

                hideCg.alpha = Mathf.Lerp(1f, 0f, smoothT);
                panelToHide.transform.localScale = Vector3.Lerp(startScale, endScale, smoothT);
                yield return null;
            }
            
            panelToHide.SetActive(false);
        }

        currentActivePanel = panelToShow;

        // 🌟 2. อนิเมชันโผล่เปิด (Fade In + Pop Up)
        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
            CanvasGroup showCg = GetOrAddCanvasGroup(panelToShow);
            Vector3 startScale = Vector3.one * 0.8f; // เริ่มจากขนาด 80%
            Vector3 endScale = Vector3.one;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = 1f - Mathf.Pow(1f - t, 3f);

                showCg.alpha = Mathf.Lerp(0f, 1f, smoothT);
                panelToShow.transform.localScale = Vector3.Lerp(startScale, endScale, smoothT);
                yield return null;
            }
            
            // เซ็ตค่าสุดท้ายให้เป๊ะ
            showCg.alpha = 1f;
            panelToShow.transform.localScale = endScale;
        }

        isTransitioning = false; // ปลดล็อคให้กดปุ่มอื่นต่อได้
    }

    // ฟังก์ชันตัวช่วย: ถ้า GameObject ไม่มี CanvasGroup ให้มันสร้างใส่ให้เองเลย
    private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
    {
        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if (cg == null) cg = obj.AddComponent<CanvasGroup>();
        return cg;
    }

    public void CloseToMainMenu()
    {
        ShowPanel(mainMenuPanel);
    }

    // ==========================================
    // 🏕️ ระบบ Resting (พักผ่อนฟื้นพลัง)
    // ==========================================
    public void OnClickRest()
    {
        if (playerData == null) return;
        playerData.stamina = playerData.maxStamina;
        playerData.currentWeaponDUR = playerData.maxWeaponDUR; 
        ShowSystemMessage("Resting... Stamina and Durability Fully Restored!");
    }

    // ==========================================
    // 💬 ฟังก์ชันตัวช่วยแสดงข้อความเตือน
    // ==========================================
    public void ShowSystemMessage(string msg)
    {
        if (systemMessageText != null) 
        {
            systemMessageText.gameObject.SetActive(true);
            systemMessageText.text = msg;
        }
        Debug.Log(msg);
    }
}