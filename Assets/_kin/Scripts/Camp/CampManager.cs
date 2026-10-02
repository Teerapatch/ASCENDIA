using UnityEngine;
using UnityEngine.UI;
using TMPro; // สำหรับจัดการ Text UI

public class CampManager : MonoBehaviour
{
    [Header("Core References")]
    public PlayerData playerData; 
    // หมายเหตุ: ต้องมี InventoryManager อยู่ใน Scene ด้วย เพื่อเช็ควัตถุดิบ
    
    [Header("UI Panels (หน้าต่างต่างๆ)")]
    public GameObject mainMenuPanel;
    public GameObject loadoutPanel;
    public GameObject forgePanel;

    [Header("Resting System")]
    public TextMeshProUGUI systemMessageText; // ข้อความเด้งเตือนกลางจอ
    public ParticleSystem healEffect; // เอฟเฟกต์ฮีล (ถ้ามี)

    [Header("Forge System")]
    public WeaponData[] availableWeapons; // ลาก WeaponData ทั้ง 4 ชิ้นมาใส่ใน Inspector

    private void Start()
    {
        // เริ่มมาให้โชว์แค่หน้า Main Menu
        ShowPanel(mainMenuPanel);
        ShowSystemMessage("Welcome to Base Camp");
    }

    // ==========================================
    // 🛠️ ส่วนจัดการ UI (เปิด/ปิด หน้าต่าง)
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
    // 🏕️ ระบบ Resting (พักผ่อน)
    // ==========================================
    public void OnClickRest()
    {
        if (playerData == null) return;

        // 1. ฟื้นฟู Stamina จนเต็ม
        playerData.stamina = playerData.maxStamina;

        // 2. ถ้ามีตัวแปรความทนทานอาวุธ (DUR) ให้รีเซ็ตตรงนี้ (สมมติว่าเพิ่มไว้ในอนาคต)
        // playerData.currentWeaponDUR = playerData.maxWeaponDUR;

        // 3. แสดงข้อความ และเล่นเอฟเฟกต์
        ShowSystemMessage("Resting... Stamina Fully Restored!");
        if (healEffect != null) healEffect.Play();

        Debug.Log("💤 พักผ่อนเรียบร้อย หลอด Stamina เต็ม!");
    }

    // ==========================================
    // ⚔️ ระบบ Forge (สร้าง/อัปเกรดอาวุธ)
    // ==========================================
    // ฟังก์ชันนี้จะถูกเรียกเมื่อกดปุ่มคราฟต์อาวุธ (รับค่า Index จากปุ่ม)
    public void TryCraftWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= availableWeapons.Length) return;
        
        WeaponData targetWeapon = availableWeapons[weaponIndex];
        
        // 1. นับจำนวน Gem Stone และ Monster Part ในกระเป๋า
        int myGems = CountItemInInventory("Gem Stone");
        int myMonsterParts = CountItemInInventory("Monster Part");

        // 2. เช็คว่าของพอไหม
        if (myGems >= targetWeapon.gemStoneCost && myMonsterParts >= targetWeapon.monsterPartCost)
        {
            // ของพอ! หักไอเทมออกจากกระเป๋า (ต้องไปเพิ่มฟังก์ชัน RemoveItem ใน InventoryManager)
            // InventoryManager.Instance.RemoveItem("Gem Stone", targetWeapon.gemStoneCost);
            // InventoryManager.Instance.RemoveItem("Monster Part", targetWeapon.monsterPartCost);
            
            // เพิ่มอาวุธเข้าตัวผู้เล่น (เขียนระบบสวมใส่อาวุธทีหลัง)
            ShowSystemMessage($"Successfully Crafted: {targetWeapon.weaponName}!");
            Debug.Log($"🔨 คราฟต์ {targetWeapon.weaponName} สำเร็จ!");
        }
        else
        {
            // ของไม่พอ!
            ShowSystemMessage($"Not enough materials for {targetWeapon.weaponName}.");
            Debug.LogWarning($"⚠️ ของไม่พอ! ขาดวัตถุดิบในการคราฟต์ {targetWeapon.weaponName}");
        }
    }

    // ฟังก์ชันตัวช่วย: นับของในกระเป๋า (อ่านจาก InventoryManager)
    private int CountItemInInventory(string itemNameKeyword)
    {
        if (InventoryManager.Instance == null) return 0;

        int total = 0;
        foreach (var slot in InventoryManager.Instance.slots)
        {
            if (slot.item.itemName.Contains(itemNameKeyword))
            {
                total += slot.amount;
            }
        }
        return total;
    }

    // ==========================================
    // 💬 ระบบข้อความเตือน
    // ==========================================
    private void ShowSystemMessage(string msg)
    {
        if (systemMessageText != null)
        {
            systemMessageText.text = msg;
            // สั่ง Fade Out หรือให้หายไปเองได้ในอนาคต
        }
    }
}