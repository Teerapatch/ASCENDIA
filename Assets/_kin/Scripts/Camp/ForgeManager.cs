using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class ForgeManager : MonoBehaviour
{
    [Header("Core References")]
    public PlayerData playerData;
    public List<WeaponData> allWeaponsInGame;
    public CampManager campManager;

    [Header("Zone 1: Blueprint List (ซ้าย)")]
    public Transform gridContent;
    public GameObject blueprintPrefab;

    [Header("Zone 2: Crafting Console (กลาง)")]
    public Image centerWeaponIcon;
    public TextMeshProUGUI gemStoneCostText;
    public TextMeshProUGUI monsterPartCostText;
    
    // 🌟 เอาปุ่ม Path Selection ออกทั้งหมด เหลือแค่ปุ่ม Action หลักปุ่มเดียว
    public Button actionButton;       
    public TextMeshProUGUI actionButtonText;

    [Header("Zone 3: Details & Stats (ขวา)")]
    public TextMeshProUGUI weaponNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI statsText;

    [Header("Effects")]
    public ParticleSystem successEffect;

    private WeaponData currentWeapon;

    private void OnEnable()
    {
        InitializeBlueprintList();
    }

    private int GetUnlockedWeaponCount()
    {
        int count = 0;
        foreach (WeaponData w in allWeaponsInGame)
        {
            if (w.isUnlocked) count++;
        }
        return count;
    }

    private void InitializeBlueprintList()
    {
        foreach (Transform child in gridContent)
        {
            Destroy(child.gameObject);
        }

        int unlockedCount = GetUnlockedWeaponCount();
        bool canCraftNewWeapon = playerData.CanCraftNewWeapon(unlockedCount);

        foreach (WeaponData weapon in allWeaponsInGame)
        {
            GameObject btnObj = Instantiate(blueprintPrefab, gridContent);
            Image icon = btnObj.transform.Find("Icon").GetComponent<Image>();
            
            bool isAvailable = weapon.isUnlocked || canCraftNewWeapon;

            if (icon != null) 
            {
                icon.sprite = weapon.weaponIcon;
                icon.color = isAvailable ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f);
            }

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => OnWeaponSelected(weapon));
        }

        if (allWeaponsInGame.Count > 0)
        {
            OnWeaponSelected(allWeaponsInGame[0]);
        }
    }

    private void OnWeaponSelected(WeaponData weapon)
    {
        currentWeapon = weapon;
        RefreshForgeUI();
    }

    private void RefreshForgeUI()
    {
        if (currentWeapon == null) return;

        int unlockedCount = GetUnlockedWeaponCount();
        bool canCraftNewWeapon = playerData.CanCraftNewWeapon(unlockedCount);
        
        bool isAvailableToView = currentWeapon.isUnlocked || canCraftNewWeapon;

        centerWeaponIcon.sprite = currentWeapon.weaponIcon;
        centerWeaponIcon.color = isAvailableToView ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f);
        
        weaponNameText.text = currentWeapon.weaponName;

        // กรณีโควตาเต็ม
        if (!isAvailableToView)
        {
            gemStoneCostText.text = "- / -";
            monsterPartCostText.text = "- / -";
            gemStoneCostText.color = Color.gray;
            monsterPartCostText.color = Color.gray;
            actionButton.interactable = false;

            descriptionText.text = "You have already bound a weapon for this Camp. Progress further to craft another one.";
            statsText.text = $"<color=red>NO CRAFTING QUOTA LEFT (Requires Camp Lv.{unlockedCount + 1})</color>";
            actionButtonText.text = "Camp Limit Reached";
            return; 
        }

        descriptionText.text = currentWeapon.description;

        // ดึงราคา (ถ้ายังไม่คราฟต์ใช้ราคา Base, ถ้าคราฟต์แล้วใช้ราคา Next Level)
        int reqGem = currentWeapon.isUnlocked ? currentWeapon.GetNextLevelGemCost() : currentWeapon.gemStoneCost;
        int reqMonster = currentWeapon.isUnlocked ? currentWeapon.GetNextLevelMonsterPartCost() : currentWeapon.monsterPartCost;

        bool hasEnoughGem = playerData.gemStone >= reqGem;
        bool hasEnoughMonster = playerData.monsterPart >= reqMonster;

        gemStoneCostText.text = $"{playerData.gemStone} / {reqGem}";
        gemStoneCostText.color = hasEnoughGem ? Color.white : Color.red;

        monsterPartCostText.text = $"{playerData.monsterPart} / {reqMonster}";
        monsterPartCostText.color = hasEnoughMonster ? Color.white : Color.red;

        bool isMaxLevel = currentWeapon.currentLevel >= currentWeapon.maxLevel;

        // 🌟 แยกแสดงผลตามสถานะของอาวุธ
        if (!currentWeapon.isUnlocked)
        {
            // ยังไม่ได้คราฟต์
            statsText.text = $"<color=#FFD700>Lv.0 ➔ Lv.1 (CRAFT TO BIND)</color>\n" +
                             $"ATK: {currentWeapon.baseDamage}\n" +
                             $"AP (Speed): {currentWeapon.speed}";
                             
            actionButtonText.text = "Craft Weapon";
            actionButton.interactable = hasEnoughGem && hasEnoughMonster; // ต้องมีของพอถึงจะกดได้
        }
        else if (isMaxLevel)
        {
            // เลเวลตัน
            statsText.text = $"<color=#FFD700>MAX LEVEL (Lv.{currentWeapon.maxLevel})</color>\n" +
                             $"ATK: {currentWeapon.GetCurrentDamage()}\n" +
                             $"AP (Speed): {currentWeapon.GetCurrentSpeed()}";
                             
            actionButtonText.text = "Max Level Reached";
            actionButton.interactable = false; 
            gemStoneCostText.text = "- / -";
            monsterPartCostText.text = "- / -";
            gemStoneCostText.color = Color.white;
            monsterPartCostText.color = Color.white;
        }
        else
        {
            // กำลังอัปเกรด (โชว์พรีวิวสเตตัสแบบรวบตึง)
            float curDmg = currentWeapon.GetCurrentDamage();
            float nextDmg = currentWeapon.GetNextLevelDamage();
            float curSpeed = currentWeapon.GetCurrentSpeed();
            float nextSpeed = currentWeapon.GetNextLevelSpeed();

            statsText.text = $"<color=#FFD700>Lv.{currentWeapon.currentLevel} ➔ Lv.{currentWeapon.currentLevel + 1}</color>\n" +
                             $"ATK: {curDmg} ➔ <color=#00FF00>{nextDmg}</color>\n" +
                             $"AP (Speed): {curSpeed} ➔ <color=#00FF00>{nextSpeed}</color>";

            actionButtonText.text = "Upgrade";
            actionButton.interactable = hasEnoughGem && hasEnoughMonster;
        }
    }

    public void OnActionBtnClicked()
    {
        if (currentWeapon == null) return;
        if (currentWeapon.isUnlocked && currentWeapon.currentLevel >= currentWeapon.maxLevel) return;

        int reqGem = currentWeapon.isUnlocked ? currentWeapon.GetNextLevelGemCost() : currentWeapon.gemStoneCost;
        int reqMonster = currentWeapon.isUnlocked ? currentWeapon.GetNextLevelMonsterPartCost() : currentWeapon.monsterPartCost;

        playerData.gemStone -= reqGem;
        playerData.monsterPart -= reqMonster;

        if (!currentWeapon.isUnlocked)
        {
            // 🌟 1. จ่ายของเพื่อคราฟต์
            currentWeapon.isUnlocked = true;
            currentWeapon.currentLevel = 1;
            
            // นำเข้า Loadout อัตโนมัติ
            for (int i = 0; i < playerData.activeLoadout.Length; i++)
            {
                if (playerData.activeLoadout[i] == null)
                {
                    playerData.activeLoadout[i] = currentWeapon;
                    break;
                }
            }

            if (campManager != null) campManager.ShowSystemMessage($"Crafted & Bound: {currentWeapon.weaponName}!");
        }
        else
        {
            // 🌟 2. จ่ายของเพื่ออัปเกรด
            currentWeapon.currentLevel++;
            if (campManager != null) campManager.ShowSystemMessage($"{currentWeapon.weaponName} upgraded to Lv.{currentWeapon.currentLevel}!");
        }

        if (successEffect != null) successEffect.Play();
        
        InitializeBlueprintList();
        RefreshForgeUI();
    }
}