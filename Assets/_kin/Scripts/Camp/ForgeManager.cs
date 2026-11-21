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
        // ข้อความสเตตัสยาวขึ้น (Durability, Posture, สกิลถัดไป) ย่อฟอนต์อัตโนมัติไม่ให้ล้นกรอบ
        if (statsText != null && !statsText.enableAutoSizing)
        {
            statsText.fontSizeMax = statsText.fontSize;
            statsText.fontSizeMin = Mathf.Min(14f, statsText.fontSize);
            statsText.enableAutoSizing = true;
        }
        InitializeBlueprintList();
    }

    private static string SkillNames(System.Collections.Generic.List<WeaponSkill> skills)
    {
        return string.Join(", ", skills.ConvertAll(s => s.skillName));
    }

    // บรรทัดสกิล: สกิลที่จะได้ตอนอัปเลเวลนี้ หรือสกิลถัดไปที่รออยู่
    private string SkillLine(WeaponData weapon)
    {
        WeaponSkill gained = weapon.SkillUnlockedAt(weapon.currentLevel + 1);
        if (gained != null) return $"\n<color=#7FD4FF>NEW SKILL: {gained.skillName}</color>";
        WeaponSkill next = weapon.NextSkillToUnlock(out int level);
        return next != null ? $"\n<color=#9AA0AA>Next skill at Lv.{level}: {next.skillName}</color>" : "";
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

        bool isMaxLevel = currentWeapon.IsMaxLevel;
        int unlockedSkills = currentWeapon.GetUnlockedSkills().Count;
        int totalSkills = currentWeapon.skillPool.FindAll(s => s != null).Count;

        // 🌟 แยกแสดงผลตามสถานะของอาวุธ
        if (!currentWeapon.isUnlocked)
        {
            // ยังไม่ได้คราฟต์
            statsText.text = $"<color=#FFD700>Lv.0 ➔ Lv.1 (CRAFT TO BIND)</color>\n" +
                             $"ATK: {currentWeapon.GetDamageAt(1)}\n" +
                             $"Speed: {currentWeapon.GetSpeedAt(1)}\n" +
                             $"DUR: {currentWeapon.GetDurabilityAt(1)}\n" +
                             $"<color=#7FD4FF>Skills: {SkillNames(currentWeapon.GetUnlockedSkills())}</color>";

            actionButtonText.text = "Craft Weapon";
            actionButton.interactable = hasEnoughGem && hasEnoughMonster; // ต้องมีของพอถึงจะกดได้
        }
        else if (isMaxLevel)
        {
            // เลเวลตัน
            int level = currentWeapon.currentLevel;
            statsText.text = $"<color=#FFD700>MAX LEVEL (Lv.{currentWeapon.MaxLevel})</color>\n" +
                             $"ATK: {currentWeapon.GetDamageAt(level)}\n" +
                             $"Speed: {currentWeapon.GetSpeedAt(level)}\n" +
                             $"DUR: {currentWeapon.GetDurabilityAt(level)}\n" +
                             $"Posture: x{currentWeapon.GetPostureAt(level):0.00}\n" +
                             $"<color=#7FD4FF>Skills: {unlockedSkills}/{totalSkills} unlocked</color>";

            actionButtonText.text = "Max Level Reached";
            actionButton.interactable = false;
            gemStoneCostText.text = "- / -";
            monsterPartCostText.text = "- / -";
            gemStoneCostText.color = Color.white;
            monsterPartCostText.color = Color.white;
        }
        else
        {
            // กำลังอัปเกรด (โชว์พรีวิวสเตตัสทุกตัว และสกิลที่จะได้)
            int level = currentWeapon.currentLevel;
            statsText.text = $"<color=#FFD700>Lv.{level} ➔ Lv.{level + 1}</color>   <color=#9AA0AA>Skills {unlockedSkills}/{totalSkills}</color>\n" +
                             $"ATK: {currentWeapon.GetDamageAt(level)} ➔ <color=#00FF00>{currentWeapon.GetDamageAt(level + 1)}</color>\n" +
                             $"Speed: {currentWeapon.GetSpeedAt(level)} ➔ <color=#00FF00>{currentWeapon.GetSpeedAt(level + 1)}</color>\n" +
                             $"DUR: {currentWeapon.GetDurabilityAt(level)} ➔ <color=#00FF00>{currentWeapon.GetDurabilityAt(level + 1)}</color>\n" +
                             $"Posture: x{currentWeapon.GetPostureAt(level):0.00} ➔ <color=#00FF00>x{currentWeapon.GetPostureAt(level + 1):0.00}</color>" +
                             SkillLine(currentWeapon);

            actionButtonText.text = "Upgrade";
            actionButton.interactable = hasEnoughGem && hasEnoughMonster;
        }
    }

    public void OnActionBtnClicked()
    {
        string message = ForgeWeapon(currentWeapon);
        if (message == null) return;
        if (campManager != null) campManager.ShowSystemMessage(message);
        if (successEffect != null) successEffect.Play();

        InitializeBlueprintList();
        RefreshForgeUI();
    }

    public int CraftedWeaponCount() => GetUnlockedWeaponCount();

    // ต้องใช้วัตถุดิบเท่าไรสำหรับการกดครั้งถัดไป (คราฟต์ = ราคาฐาน, อัปเกรด = ราคาเลเวลถัดไป)
    public static void Cost(WeaponData weapon, out int gem, out int monster)
    {
        gem = weapon.isUnlocked ? weapon.GetNextLevelGemCost() : weapon.gemStoneCost;
        monster = weapon.isUnlocked ? weapon.GetNextLevelMonsterPartCost() : weapon.monsterPartCost;
    }

    public bool CanForge(WeaponData weapon)
    {
        if (weapon == null || playerData == null) return false;
        if (weapon.isUnlocked && weapon.IsMaxLevel) return false;
        if (!weapon.isUnlocked && !playerData.CanCraftNewWeapon(GetUnlockedWeaponCount())) return false;
        Cost(weapon, out int gem, out int monster);
        return playerData.gemStone >= gem && playerData.monsterPart >= monster;
    }

    // คราฟต์หรืออัปเกรดหนึ่งครั้ง (ใช้ร่วมกับหน้า Forge ชุดใหม่ CampReArtUI) คืนข้อความผลลัพธ์ หรือ null ถ้าทำไม่ได้
    public string ForgeWeapon(WeaponData weapon)
    {
        if (!CanForge(weapon)) return null;

        Cost(weapon, out int reqGem, out int reqMonster);
        playerData.gemStone -= reqGem;
        playerData.monsterPart -= reqMonster;

        if (!weapon.isUnlocked)
        {
            // 🌟 1. จ่ายของเพื่อคราฟต์
            weapon.isUnlocked = true;
            weapon.currentLevel = 1;

            // นำเข้า Loadout อัตโนมัติ
            for (int i = 0; i < playerData.activeLoadout.Length; i++)
            {
                if (playerData.activeLoadout[i] == null && playerData.IsLoadoutSlotUnlocked(i))
                {
                    playerData.activeLoadout[i] = weapon;
                    break;
                }
            }
            return $"Crafted & Bound: {weapon.weaponName}!";
        }

        // 🌟 2. จ่ายของเพื่ออัปเกรด
        weapon.currentLevel++;
        WeaponSkill gained = weapon.SkillUnlockedAt(weapon.currentLevel);
        if (gained != null)
        {
            // ช่องสกิลยังไม่เต็ม: ใส่สกิลใหม่ให้เลย (ถอด/สลับได้ที่หน้า Skills)
            List<WeaponSkill> equipped = playerData.GetEquippedSkills(weapon);
            if (!equipped.Contains(gained) && equipped.Count < WeaponData.MaxEquippedSkills)
            {
                equipped.Add(gained);
                List<WeaponSkill> pool = weapon.skillPool;
                equipped.Sort((x, y) => pool.IndexOf(x).CompareTo(pool.IndexOf(y)));
                playerData.SetEquippedSkills(weapon, equipped);
            }
        }
        return gained != null
            ? $"{weapon.weaponName} upgraded to Lv.{weapon.currentLevel}! New skill: {gained.skillName}"
            : $"{weapon.weaponName} upgraded to Lv.{weapon.currentLevel}!";
    }
}