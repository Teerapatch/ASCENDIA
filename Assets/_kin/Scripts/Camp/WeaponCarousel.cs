using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class WeaponCarousel : MonoBehaviour
{
    [Header("References")]
    public PlayerData playerData;
    public CampManager campManager; // 🌟 ไว้สั่งปิดหน้าต่างกลับไปหน้าหลัก
    public WeaponType carouselWeaponType; 
    public List<WeaponData> availableWeaponsForThisType; 

    [Header("UI Elements (ใส่ใน Scene)")]
    public Image leftImage;   
    public Image centerImage; 
    public Image rightImage;  
    public TextMeshProUGUI weaponNameText; 

    [Header("Visual Effects")]
    public float sideImageScale = 0.7f; 
    public Color unselectedColor = new Color(0.3f, 0.3f, 0.3f, 1f); 
    public Color selectedColor = Color.white; 
    
    // 🌟 ตัวแปรควบคุมความสมูท (Juiciness)
    public float animationSpeed = 10f; 
    public GameObject selectionAuraEffect; // 🌟 ลากออร่า (เช่น Particle หรือ Image เรืองแสง) มาใส่ตรงนี้

    private int currentIndex = 0; 

    // ตัวแปรเก็บค่าเป้าหมาย เพื่อให้ Lerp วิ่งไปหา
    private Vector3 centerTargetScale = Vector3.one;
    private Vector3 sideTargetScale;
    private Color centerTargetColor;
    private Color sideTargetColor;

    private void Start()
    {
        sideTargetScale = Vector3.one * sideImageScale;
        centerTargetColor = selectedColor;
        sideTargetColor = unselectedColor;

        if (selectionAuraEffect != null) selectionAuraEffect.SetActive(false); // ปิดออร่าไว้ก่อน

        InitializeCurrentIndex();
        UpdateCarouselVisuals(true); // true = อัปเดตแบบข้ามแอนิเมชันตอนเปิดครั้งแรก
    }

    private void InitializeCurrentIndex()
    {
        if (playerData == null || availableWeaponsForThisType.Count == 0) return;

        WeaponData currentlyEquipped = null;
        switch (carouselWeaponType)
        {
            case WeaponType.Dagger: currentlyEquipped = playerData.equippedDagger; break;
            case WeaponType.LongSword: currentlyEquipped = playerData.equippedSword; break;
            case WeaponType.Bow: currentlyEquipped = playerData.equippedBow; break;
            case WeaponType.Spell: currentlyEquipped = playerData.equippedSpell; break;
        }

        if (currentlyEquipped != null)
        {
            currentIndex = availableWeaponsForThisType.IndexOf(currentlyEquipped);
            if(currentIndex == -1) currentIndex = 0; 
        }
    }

    // ==========================================
    // 🌟 แอนิเมชัน Lerp (ทำงานทุกเฟรม)
    // ==========================================
    private void Update()
    {
        if (availableWeaponsForThisType.Count == 0) return;

        // ค่อยๆ ปรับขนาด (Scale)
        if (centerImage != null)
            centerImage.transform.localScale = Vector3.Lerp(centerImage.transform.localScale, centerTargetScale, Time.deltaTime * animationSpeed);
        if (leftImage != null)
            leftImage.transform.localScale = Vector3.Lerp(leftImage.transform.localScale, sideTargetScale, Time.deltaTime * animationSpeed);
        if (rightImage != null)
            rightImage.transform.localScale = Vector3.Lerp(rightImage.transform.localScale, sideTargetScale, Time.deltaTime * animationSpeed);

        // ค่อยๆ ปรับสี (Color / Fade)
        if (centerImage != null)
            centerImage.color = Color.Lerp(centerImage.color, centerTargetColor, Time.deltaTime * animationSpeed);
        if (leftImage != null)
            leftImage.color = Color.Lerp(leftImage.color, sideTargetColor, Time.deltaTime * animationSpeed);
        if (rightImage != null)
            rightImage.color = Color.Lerp(rightImage.color, sideTargetColor, Time.deltaTime * animationSpeed);
    }

    // ==========================================
    // ควบคุมการเลื่อน ซ้าย/ขวา
    // ==========================================
    public void SlideLeft()
    {
        if (availableWeaponsForThisType.Count == 0) return;
        currentIndex--;
        if (currentIndex < 0) currentIndex = availableWeaponsForThisType.Count - 1; 
        UpdateCarouselVisuals(false);
    }

    public void SlideRight()
    {
        if (availableWeaponsForThisType.Count == 0) return;
        currentIndex++;
        if (currentIndex >= availableWeaponsForThisType.Count) currentIndex = 0; 
        UpdateCarouselVisuals(false);
    }

    // ==========================================
    // อัปเดตข้อมูลภาพ (แต่ปล่อยให้ Update ทำแอนิเมชัน)
    // ==========================================
    private void UpdateCarouselVisuals(bool instant = false)
    {
        if (availableWeaponsForThisType.Count == 0) return;

        int totalWeapons = availableWeaponsForThisType.Count;
        int leftIndex = (currentIndex - 1 + totalWeapons) % totalWeapons;
        int rightIndex = (currentIndex + 1) % totalWeapons;

        // สลับรูปภาพ
        if (centerImage != null) centerImage.sprite = availableWeaponsForThisType[currentIndex].weaponIcon;
        if (leftImage != null) 
        {
            leftImage.sprite = availableWeaponsForThisType[leftIndex].weaponIcon;
            leftImage.gameObject.SetActive(totalWeapons > 1); 
        }
        if (rightImage != null)
        {
            rightImage.sprite = availableWeaponsForThisType[rightIndex].weaponIcon;
            rightImage.gameObject.SetActive(totalWeapons > 2); 
        }

        if (weaponNameText != null)
        {
            weaponNameText.text = availableWeaponsForThisType[currentIndex].weaponName;
        }

        // ถ้าให้เปลี่ยนทันที (เช่น ตอนเปิดหน้าต่าง) ให้เซ็ตค่าตรงๆ เลย
        if (instant)
        {
            if (centerImage != null) { centerImage.transform.localScale = centerTargetScale; centerImage.color = centerTargetColor; }
            if (leftImage != null) { leftImage.transform.localScale = sideTargetScale; leftImage.color = sideTargetColor; }
            if (rightImage != null) { rightImage.transform.localScale = sideTargetScale; rightImage.color = sideTargetColor; }
        }
        else
        {
            // ถ้าไม่ใส่ instant เราจะทำ "Bounce" แกล้งให้ตรงกลางมันเล็กลงนิดนึง แล้วค่อยให้มัน Lerp เด้งกลับมาที่ขนาดเดิม
            if (centerImage != null) centerImage.transform.localScale = Vector3.one * 0.8f; 
        }
    }

    // ==========================================
    // ปุ่มยืนยัน "สวมใส่" (Equip)
    // ==========================================
    public void EquipSelectedWeapon()
    {
        if (playerData == null || availableWeaponsForThisType.Count == 0) return;

        WeaponData selectedWeapon = availableWeaponsForThisType[currentIndex];

        switch (carouselWeaponType)
        {
            case WeaponType.Dagger: playerData.equippedDagger = selectedWeapon; break;
            case WeaponType.LongSword: playerData.equippedSword = selectedWeapon; break;
            case WeaponType.Bow: playerData.equippedBow = selectedWeapon; break;
            case WeaponType.Spell: playerData.equippedSpell = selectedWeapon; break;
        }

        // 🌟 1. เปิดเอฟเฟกต์ออร่า
        if (selectionAuraEffect != null)
        {
            selectionAuraEffect.SetActive(true);
        }

        // 🌟 2. แสดงข้อความ
        if (campManager != null)
        {
            campManager.ShowSystemMessage($"Equipped: {selectedWeapon.weaponName}");
        }

        // 🌟 3. สั่งปิดหน้าต่างกลับไปหน้าเมนูหลัก หลังจากดีเลย์นิดหน่อยให้เห็นออร่าก่อน
        Invoke("CloseWindow", 0.5f); // รอ 0.5 วินาที
    }

    private void CloseWindow()
    {
        if (campManager != null)
        {
            // ปิดออร่าเตรียมไว้ใช้รอบหน้า
            if (selectionAuraEffect != null) selectionAuraEffect.SetActive(false);
            
            // สั่ง Manager ให้กลับหน้าหลัก
            campManager.CloseToMainMenu(); 
        }
    }
}