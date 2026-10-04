using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class LoadoutManager : MonoBehaviour
{
    [Header("Data")]
    public PlayerData playerData;
    public List<WeaponData> allWeaponsInGame; // อาวุธทั้งหมดที่มีในเกม

    [Header("Main Panel (ช่อง 4 ช่องตรงกลาง/ขวา)")]
    public Button[] slotButtons; // ลากปุ่ม Slot 1 ถึง 4 มาใส่
    public Image[] slotIcons;    // ลาก Image ที่ไว้โชว์รูปอาวุธในแต่ละ Slot มาใส่
    public GameObject[] lockIcons; // ลากรูปลูกกุญแจ (ถ้ามี) ไว้โชว์ตอนแคมป์เวลไม่ถึง

    [Header("Selection Panel (หน้าต่างเลือกด้านซ้าย)")]
    public GameObject selectionPanel; // ตัว Panel กรอบด้านซ้าย
    public Transform gridContent;     // ตัวที่มีคอมโพเนนต์ GridLayoutGroup
    public GameObject weaponIconPrefab; // Prefab ของปุ่มเลือกอาวุธ

    private int currentlySelectingSlotIndex = -1; // จำว่าตอนนี้กดเลือกช่องไหนอยู่

    private void OnEnable()
    {
        selectionPanel.SetActive(false); // ซ่อนหน้าต่างซ้ายไว้ก่อน
        RefreshMainSlots();
    }

    // ==========================================
    // 1. อัปเดตหน้าจอหลัก (ช่อง 4 ช่อง)
    // ==========================================
    public void RefreshMainSlots()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            // เช็คว่าแคมป์เลเวลถึงระดับที่ปลดล็อกช่องนี้หรือยัง (เช่น ช่อง 2 ต้องการ Camp Lv.2)
            bool isUnlocked = i < playerData.currentCampLevel;

            slotButtons[i].interactable = isUnlocked; // เปิด/ปิด การกดปุ่ม
            if (lockIcons[i] != null) lockIcons[i].SetActive(!isUnlocked); // โชว์/ซ่อน กุญแจ

            if (isUnlocked)
            {
                // ถ้ามีอาวุธใส่อยู่ ให้โชว์รูป
                if (playerData.activeLoadout[i] != null)
                {
                    slotIcons[i].sprite = playerData.activeLoadout[i].weaponIcon;
                    slotIcons[i].color = Color.white; // เปิดให้เห็นชัด
                }
                else
                {
                    slotIcons[i].sprite = null;
                    slotIcons[i].color = new Color(0, 0, 0, 0); // ซ่อนรูปถ้าช่องว่าง
                }
            }
            else
            {
                // ถ้ายังไม่ปลดล็อก
                slotIcons[i].sprite = null;
                slotIcons[i].color = new Color(0, 0, 0, 0.5f); // ทำสีทึบๆ
            }
        }
    }

    // ==========================================
    // 2. เมื่อผู้เล่นกดที่ช่อง 1 - 4 บนหน้าจอหลัก
    // ==========================================
    // ** ต้องไปตั้งค่า OnClick ของแต่ละปุ่มใน Unity ให้ส่งเลข 0, 1, 2, 3 เข้ามาด้วย **
    public void OnSlotClicked(int slotIndex)
    {
        currentlySelectingSlotIndex = slotIndex;
        OpenSelectionPanel();
    }

    // ==========================================
    // 3. เปิดหน้าต่างด้านซ้าย และสร้างปุ่มตารางอาวุธ
    // ==========================================
    private void OpenSelectionPanel()
    {
        selectionPanel.SetActive(true);

        // ลบปุ่มเก่าทิ้งให้หมดก่อน
        foreach (Transform child in gridContent)
        {
            Destroy(child.gameObject);
        }

        // สร้างปุ่มอาวุธใหม่ตามลิตส์ที่มี
        foreach (WeaponData weapon in allWeaponsInGame)
        {
            GameObject newBtnObj = Instantiate(weaponIconPrefab, gridContent);
            
            // เปลี่ยนรูปไอคอน
            Image iconImage = newBtnObj.transform.Find("Icon").GetComponent<Image>();
            iconImage.sprite = weapon.weaponIcon;

            // ผูกฟังก์ชันให้ปุ่ม เมื่อกดแล้วให้เลือกอาวุธชิ้นนี้
            Button btn = newBtnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => OnWeaponSelectedFromGrid(weapon));
        }
    }

    // ==========================================
    // 4. เมื่อผู้เล่นกดเลือกอาวุธจากหน้าต่างด้านซ้าย
    // ==========================================
    private void OnWeaponSelectedFromGrid(WeaponData chosenWeapon)
    {
        // 1. เช็คก่อนว่าอาวุธนี้ถูกใส่ไว้ในช่องอื่นแล้วหรือยัง (กันผู้เล่นใส่ดาบ 2 ช่อง)
        for (int i = 0; i < playerData.activeLoadout.Length; i++)
        {
            if (playerData.activeLoadout[i] == chosenWeapon)
            {
                playerData.activeLoadout[i] = null; // ถอดออกจากช่องเดิม
            }
        }

        // 2. ใส่อาวุธลงในช่องที่เรากดเปิดมา
        playerData.activeLoadout[currentlySelectingSlotIndex] = chosenWeapon;

        // 3. ปิดหน้าต่างซ้าย และรีเฟรชหน้าหลัก
        selectionPanel.SetActive(false);
        RefreshMainSlots();
    }
}