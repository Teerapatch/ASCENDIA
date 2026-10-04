using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class WeaponCarousel : MonoBehaviour
{
    [Header("References")]
    public PlayerData playerData;
    public CampManager campManager;
    public List<WeaponData> allWeaponsInGame;

    [Header("UI Elements")]
    public Image leftImage;
    public Image centerImage;
    public Image rightImage;
    public TextMeshProUGUI weaponNameText;

    public Button equipButton; 
    public TextMeshProUGUI equipButtonText; 

    [Header("Equipped Status Indicators")]
    public GameObject leftEquippedGlow;
    public GameObject centerEquippedGlow;
    public GameObject rightEquippedGlow;

    [Header("Visual Effects")]
    public float sideImageScale = 0.7f;
    public Color unselectedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color selectedColor = Color.white;
    public float animationSpeed = 10f;
    
    public GameObject selectionAuraEffect;

    private int currentIndex = 0;
    private Vector3 centerTargetScale = Vector3.one;
    private Vector3 sideTargetScale;
    private Color centerTargetColor;
    private Color sideTargetColor;

    private void Start()
    {
        sideTargetScale = Vector3.one * sideImageScale;
        centerTargetColor = selectedColor;
        sideTargetColor = unselectedColor;
        
        currentIndex = 0;
        UpdateCarouselVisuals(true);
    }

    private void Update()
    {
        if (allWeaponsInGame.Count == 0) return;

        if (centerImage != null) centerImage.transform.localScale = Vector3.Lerp(centerImage.transform.localScale, centerTargetScale, Time.deltaTime * animationSpeed);
        if (leftImage != null) leftImage.transform.localScale = Vector3.Lerp(leftImage.transform.localScale, sideTargetScale, Time.deltaTime * animationSpeed);
        if (rightImage != null) rightImage.transform.localScale = Vector3.Lerp(rightImage.transform.localScale, sideTargetScale, Time.deltaTime * animationSpeed);

        if (centerEquippedGlow != null && centerImage != null) centerEquippedGlow.transform.localScale = centerImage.transform.localScale;
        if (leftEquippedGlow != null && leftImage != null) leftEquippedGlow.transform.localScale = leftImage.transform.localScale;
        if (rightEquippedGlow != null && rightImage != null) rightEquippedGlow.transform.localScale = rightImage.transform.localScale;

        if (centerImage != null) centerImage.color = Color.Lerp(centerImage.color, centerTargetColor, Time.deltaTime * animationSpeed);
        if (leftImage != null) leftImage.color = Color.Lerp(leftImage.color, sideTargetColor, Time.deltaTime * animationSpeed);
        if (rightImage != null) rightImage.color = Color.Lerp(rightImage.color, sideTargetColor, Time.deltaTime * animationSpeed);
    }

    public void SlideLeft()
    {
        if (allWeaponsInGame.Count == 0) return;
        currentIndex--;
        if (currentIndex < 0) currentIndex = allWeaponsInGame.Count - 1;
        UpdateCarouselVisuals(false);
    }

    public void SlideRight()
    {
        if (allWeaponsInGame.Count == 0) return;
        currentIndex++;
        if (currentIndex >= allWeaponsInGame.Count) currentIndex = 0;
        UpdateCarouselVisuals(false);
    }

    private void UpdateCarouselVisuals(bool instant = false)
    {
        if (allWeaponsInGame.Count == 0) return;

        int totalWeapons = allWeaponsInGame.Count;
        int leftIndex = (currentIndex - 1 + totalWeapons) % totalWeapons;
        int rightIndex = (currentIndex + 1) % totalWeapons;

        if (centerImage != null) centerImage.sprite = allWeaponsInGame[currentIndex].weaponIcon;
        if (leftImage != null)
        {
            leftImage.sprite = allWeaponsInGame[leftIndex].weaponIcon;
            leftImage.gameObject.SetActive(totalWeapons > 1);
        }
        if (rightImage != null)
        {
            rightImage.sprite = allWeaponsInGame[rightIndex].weaponIcon;
            rightImage.gameObject.SetActive(totalWeapons > 2);
        }

        if (weaponNameText != null) weaponNameText.text = allWeaponsInGame[currentIndex].weaponName;

        if (instant)
        {
            if (centerImage != null) { centerImage.transform.localScale = centerTargetScale; centerImage.color = centerTargetColor; }
            if (leftImage != null) { leftImage.transform.localScale = sideTargetScale; leftImage.color = sideTargetColor; }
            if (rightImage != null) { rightImage.transform.localScale = sideTargetScale; rightImage.color = sideTargetColor; }
        }
        else
        {
            if (centerImage != null) centerImage.transform.localScale = Vector3.one * 0.8f;
        }

        RefreshEquipButtonState(); 
        RefreshEquippedIndicators(); 
    }

    private void RefreshEquipButtonState()
    {
        if (playerData == null || equipButton == null || equipButtonText == null) return;

        WeaponData showingWeapon = allWeaponsInGame[currentIndex];

        if (playerData.activeLoadout.Contains(showingWeapon))
        {
            equipButton.interactable = true;
            equipButtonText.text = "Unequip"; 
            return;
        }

        if (playerData.activeLoadout.Count >= playerData.currentCampLevel)
        {
            equipButton.interactable = false;
            equipButtonText.text = "Slots Full"; 
            return;
        }

        equipButton.interactable = true;
        equipButtonText.text = "Equip"; 
    }

    // 🌟 ฟังก์ชันนี้จะจัดการทั้งกรอบแสง และ ออร่าตรงกลางแบบอัตโนมัติ
    private void RefreshEquippedIndicators()
    {
        if (playerData == null || allWeaponsInGame.Count == 0) return;

        int totalWeapons = allWeaponsInGame.Count;
        int leftIndex = (currentIndex - 1 + totalWeapons) % totalWeapons;
        int rightIndex = (currentIndex + 1) % totalWeapons;

        bool isLeftEquipped = playerData.activeLoadout.Contains(allWeaponsInGame[leftIndex]);
        bool isCenterEquipped = playerData.activeLoadout.Contains(allWeaponsInGame[currentIndex]);
        bool isRightEquipped = playerData.activeLoadout.Contains(allWeaponsInGame[rightIndex]);

        if (leftEquippedGlow != null)
        {
            leftEquippedGlow.SetActive(isLeftEquipped);
            if (isLeftEquipped) leftEquippedGlow.GetComponent<Image>().color = allWeaponsInGame[leftIndex].auraColor;
        }
            
        if (centerEquippedGlow != null)
        {
            centerEquippedGlow.SetActive(isCenterEquipped);
            if (isCenterEquipped) centerEquippedGlow.GetComponent<Image>().color = allWeaponsInGame[currentIndex].auraColor;
        }
            
        if (rightEquippedGlow != null)
        {
            rightEquippedGlow.SetActive(isRightEquipped);
            if (isRightEquipped) rightEquippedGlow.GetComponent<Image>().color = allWeaponsInGame[rightIndex].auraColor;
        }

        // 🌟 จัดการออร่าระเบิดให้เล่นค้างไว้ตลอดถ้าอาวุธถูกใส่อยู่
        if (selectionAuraEffect != null)
        {
            selectionAuraEffect.SetActive(isCenterEquipped);
            if (isCenterEquipped)
            {
                ParticleSystem ps = selectionAuraEffect.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.startColor = allWeaponsInGame[currentIndex].auraColor;
                }
                
                Image auraImg = selectionAuraEffect.GetComponent<Image>();
                if (auraImg != null) auraImg.color = allWeaponsInGame[currentIndex].auraColor;
            }
        }
    }

    public void EquipSelectedWeapon()
    {
        if (playerData == null || allWeaponsInGame.Count == 0) return;

        WeaponData selectedWeapon = allWeaponsInGame[currentIndex];

        if (playerData.activeLoadout.Contains(selectedWeapon))
        {
            playerData.activeLoadout.Remove(selectedWeapon);
            if (campManager != null) campManager.ShowSystemMessage($"Unequipped: {selectedWeapon.weaponName}");
        }
        else
        {
            if (playerData.activeLoadout.Count < playerData.currentCampLevel)
            {
                playerData.activeLoadout.Add(selectedWeapon);
                if (campManager != null) campManager.ShowSystemMessage($"Equipped: {selectedWeapon.weaponName}");
            }
        }

        RefreshEquipButtonState();
        RefreshEquippedIndicators(); // อัปเดตทุกอย่างทันทีที่กดใส่/ถอด
    }
}