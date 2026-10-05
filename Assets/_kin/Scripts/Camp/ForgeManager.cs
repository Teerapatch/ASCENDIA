using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class ForgeManager : MonoBehaviour
{
    public enum UpgradePath { None, Damage, AP }

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
    
    public GameObject pathSelectionGroup; 
    public Button pathDamageBtn;
    public Button pathAPBtn;
    public Image pathDamageHighlight; 
    public Image pathAPHighlight;     

    public Button actionButton;       
    public TextMeshProUGUI actionButtonText;

    [Header("Zone 3: Details & Stats (ขวา)")]
    public TextMeshProUGUI weaponNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI statsText;

    [Header("Effects")]
    public ParticleSystem successEffect;

    private WeaponData currentWeapon;
    private UpgradePath selectedPath = UpgradePath.None;

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
        bool canUnlockNewWeapon = unlockedCount < playerData.currentCampLevel;

        foreach (WeaponData weapon in allWeaponsInGame)
        {
            GameObject btnObj = Instantiate(blueprintPrefab, gridContent);
            Image icon = btnObj.transform.Find("Icon").GetComponent<Image>();
            
            bool isAvailable = weapon.isUnlocked || canUnlockNewWeapon;

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
        selectedPath = currentWeapon.isUnlocked ? UpgradePath.Damage : UpgradePath.None; 
        RefreshForgeUI();
    }

    public void SelectPathDamage()
    {
        selectedPath = UpgradePath.Damage;
        RefreshForgeUI();
    }

    public void SelectPathAP()
    {
        selectedPath = UpgradePath.AP;
        RefreshForgeUI();
    }

    private void RefreshForgeUI()
    {
        if (currentWeapon == null) return;

        int unlockedCount = GetUnlockedWeaponCount();
        bool canUnlockNewWeapon = unlockedCount < playerData.currentCampLevel;
        
        bool isAvailableToView = currentWeapon.isUnlocked || canUnlockNewWeapon;

        centerWeaponIcon.sprite = currentWeapon.weaponIcon;
        centerWeaponIcon.color = isAvailableToView ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f);
        
        weaponNameText.text = currentWeapon.weaponName;

        // ==========================================
        // 🌟 กรณีที่โควตาหมด (ล็อคการปลดชิ้นใหม่)
        // ==========================================
        if (!isAvailableToView)
        {
            if (pathSelectionGroup != null) pathSelectionGroup.SetActive(false);
            
            gemStoneCostText.text = "- / -";
            monsterPartCostText.text = "- / -";
            gemStoneCostText.color = Color.gray;
            monsterPartCostText.color = Color.gray;
            actionButton.interactable = false;

            descriptionText.text = "You have reached the unlock limit for this Camp. Progress further to unlock more weapons.";
            statsText.text = $"<color=red>REQUIRES CAMP LEVEL {unlockedCount + 1}</color>";
            actionButtonText.text = "Camp Limit Reached";
            return; 
        }

        // ==========================================
        // กรณีปกติ: รับฟรีได้ หรือ อัปเกรดชิ้นเดิมได้
        // ==========================================
        descriptionText.text = currentWeapon.description;

        if (pathSelectionGroup != null) pathSelectionGroup.SetActive(currentWeapon.isUnlocked);
        
        if (pathDamageHighlight != null) pathDamageHighlight.enabled = (selectedPath == UpgradePath.Damage);
        if (pathAPHighlight != null) pathAPHighlight.enabled = (selectedPath == UpgradePath.AP);

        bool isMaxLevel = currentWeapon.currentLevel >= currentWeapon.maxLevel;

        // 🌟 ถ้ายืนยันจะปลดล็อคชิ้นใหม่ -> ให้ขึ้นว่า FREE
        if (!currentWeapon.isUnlocked)
        {
            gemStoneCostText.text = "FREE";
            gemStoneCostText.color = Color.green;
            monsterPartCostText.text = "FREE";
            monsterPartCostText.color = Color.green;

            statsText.text = $"<color=#FFD700>Lv.0 ➔ Lv.1 (FREE UNLOCK)</color>\n" +
                             $"ATK: {currentWeapon.attackDamage}\n" +
                             $"AP (Speed): {currentWeapon.speed}";
                             
            actionButtonText.text = "Unlock Blueprint";
            actionButton.interactable = true; // โควตามีแล้ว กดรับฟรีได้เลย
        }
        else if (isMaxLevel)
        {
            // 🌟 กรณีเลเวลตัน
            gemStoneCostText.text = "- / -";
            monsterPartCostText.text = "- / -";
            gemStoneCostText.color = Color.white;
            monsterPartCostText.color = Color.white;

            statsText.text = $"<color=#FFD700>MAX LEVEL (Lv.{currentWeapon.maxLevel})</color>\n" +
                             $"ATK: {currentWeapon.GetCurrentDamage()}\n" +
                             $"AP (Speed): {currentWeapon.GetCurrentSpeed()}";
                             
            actionButtonText.text = "Max Level Reached";
            actionButton.interactable = false; 
        }
        else
        {
            // 🌟 กรณีอัปเกรด (ต้องใช้แร่)
            int reqGem = currentWeapon.GetNextLevelGemCost();
            int reqMonster = currentWeapon.GetNextLevelMonsterPartCost();

            bool hasEnoughGem = playerData.gemStone >= reqGem;
            bool hasEnoughMonster = playerData.monsterPart >= reqMonster;

            gemStoneCostText.text = $"{playerData.gemStone} / {reqGem}";
            gemStoneCostText.color = hasEnoughGem ? Color.white : Color.red;

            monsterPartCostText.text = $"{playerData.monsterPart} / {reqMonster}";
            monsterPartCostText.color = hasEnoughMonster ? Color.white : Color.red;

            string previewStats = $"<color=#FFD700>Lv.{currentWeapon.currentLevel} ➔ Lv.{currentWeapon.currentLevel + 1}</color>\n";
            float curDmg = currentWeapon.GetCurrentDamage();
            float curSpeed = currentWeapon.GetCurrentSpeed();

            if (selectedPath == UpgradePath.Damage)
            {
                previewStats += $"ATK: {curDmg} ➔ <color=#00FF00>{curDmg + 5}</color>\n";
                previewStats += $"AP (Speed): {curSpeed}"; 
            }
            else if (selectedPath == UpgradePath.AP)
            {
                previewStats += $"ATK: {curDmg}\n"; 
                previewStats += $"AP (Speed): {curSpeed} ➔ <color=#00FF00>{curSpeed - 2}</color>";
            }

            statsText.text = previewStats;
            actionButtonText.text = "Upgrade";
            actionButton.interactable = hasEnoughGem && hasEnoughMonster;
        }
    }

    public void OnActionBtnClicked()
    {
        if (currentWeapon == null) return;
        if (currentWeapon.isUnlocked && currentWeapon.currentLevel >= currentWeapon.maxLevel) return;

        if (!currentWeapon.isUnlocked)
        {
            // 🌟 1. ปลดล็อคอาวุธใหม่ (ฟรี ไม่หักแร่)
            currentWeapon.isUnlocked = true;
            if (campManager != null) campManager.ShowSystemMessage($"Unlocked: {currentWeapon.weaponName}! Go to Loadout to equip it.");
        }
        else
        {
            // 🌟 2. อัปเกรดอาวุธเดิม (หักแร่ตามปกติ)
            int reqGem = currentWeapon.GetNextLevelGemCost();
            int reqMonster = currentWeapon.GetNextLevelMonsterPartCost();

            playerData.gemStone -= reqGem;
            playerData.monsterPart -= reqMonster;

            currentWeapon.currentLevel++;
            if (selectedPath == UpgradePath.Damage) currentWeapon.damageUpgradeCount++;
            else if (selectedPath == UpgradePath.AP) currentWeapon.apUpgradeCount++;

            if (campManager != null) campManager.ShowSystemMessage($"{currentWeapon.weaponName} upgraded to Lv.{currentWeapon.currentLevel}!");
        }

        if (successEffect != null) successEffect.Play();

        selectedPath = currentWeapon.isUnlocked ? UpgradePath.Damage : UpgradePath.None;
        
        InitializeBlueprintList();
        RefreshForgeUI();
    }
}