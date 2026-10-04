using UnityEngine;
using TMPro;

public class CampManager : MonoBehaviour
{
    [Header("Core References")]
    public PlayerData playerData; 
    
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject loadoutPanel; // 🌟 หน้าต่างนี้ข้างในจะใส่ WeaponCarouselUI ตามที่เราคุยกัน
    public GameObject forgePanel;

    [Header("Resting System")]
    public TextMeshProUGUI systemMessageText; 

    private void Start()
    {
        ShowPanel(mainMenuPanel);
        ShowSystemMessage("Welcome to Base Camp");
    }

    // ==========================================
    // 🛠️ ระบบจัดการหน้าต่าง UI เปิด/ปิด
    // ==========================================
    public void ShowPanel(GameObject panelToShow)
    {
        mainMenuPanel.SetActive(false);
        if(loadoutPanel != null) loadoutPanel.SetActive(false);
        if(forgePanel != null) forgePanel.SetActive(false);

        if(panelToShow != null) panelToShow.SetActive(true);
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
        playerData.currentWeaponDUR = playerData.maxWeaponDUR; // ฟื้นฟูความทนทานอาวุธ
        ShowSystemMessage("Resting... Stamina and Durability Fully Restored!");
    }

    // ==========================================
    // 💬 ฟังก์ชันตัวช่วยแสดงข้อความเตือน
    // ==========================================
    public void ShowSystemMessage(string msg)
    {
        if (systemMessageText != null) systemMessageText.text = msg;
        Debug.Log(msg);
    }
}