using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class WeaponCarousel : MonoBehaviour
{
    [Header("References")]
    public PlayerData playerData;
    public List<WeaponData> allWeaponsInGame;
    public CampManager campManager;

    [Header("Main Loadout Slots")]
    public Button[] slotButtons;
    public Image[] slotIcons;
    public GameObject[] lockIcons;
    
    public TextMeshProUGUI[] slotNameTexts; 
    public GameObject[] slotAuraEffects;    

    [Header("Default Visuals")]
    public Sprite emptySlotSprite; 

    [Header("Selection Grid UI")]
    public GameObject selectionPanel;
    public Transform gridContent;
    public GameObject weaponGridPrefab;

    private int currentSelectingSlot = -1;
    private Vector2[] iconOriginalPositions;

    // 🌟 ตัวแปรใหม่สำหรับคุมอนิเมชันหน้าต่างซ้าย
    private Vector2 panelOriginalPos;
    private CanvasGroup panelCanvasGroup;
    private Coroutine panelCoroutine;

    private void Start()
    {
        // เก็บตำแหน่งดั้งเดิมของหน้าต่างตารางเอาไว้
        RectTransform panelRect = selectionPanel.GetComponent<RectTransform>();
        panelOriginalPos = panelRect.anchoredPosition;

        // เช็คว่ามี CanvasGroup ไหม ถ้าไม่มีให้โค้ดแอบใส่ให้เอง (เอาไว้คุมการ Fade โปร่งใส)
        panelCanvasGroup = selectionPanel.GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null) panelCanvasGroup = selectionPanel.AddComponent<CanvasGroup>();

        selectionPanel.SetActive(false);

        iconOriginalPositions = new Vector2[slotIcons.Length];
        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (slotIcons[i] != null)
            {
                iconOriginalPositions[i] = slotIcons[i].rectTransform.anchoredPosition;
            }
        }

        RefreshMainSlots();
    }

    // ==========================================
    // 1. อัปเดตช่องหลัก
    // ==========================================
    public void RefreshMainSlots()
    {
        if (playerData == null) return;

        for (int i = 0; i < slotButtons.Length; i++)
        {
            bool isUnlocked = i < playerData.currentCampLevel;
            slotButtons[i].interactable = true; 
            
            if (lockIcons.Length > i && lockIcons[i] != null) 
            {
                lockIcons[i].SetActive(false); 
                lockIcons[i].transform.localScale = Vector3.one; 
            }

            if (isUnlocked)
            {
                if (playerData.activeLoadout.Length > i && playerData.activeLoadout[i] != null)
                {
                    WeaponData w = playerData.activeLoadout[i];
                    slotIcons[i].sprite = w.weaponIcon;
                    slotIcons[i].color = Color.white;
                    
                    if (slotNameTexts.Length > i && slotNameTexts[i] != null) slotNameTexts[i].text = w.weaponName;

                    if (slotAuraEffects.Length > i && slotAuraEffects[i] != null)
                    {
                        slotAuraEffects[i].SetActive(true);
                        SetAuraColor(slotAuraEffects[i], w.auraColor);
                    }
                }
                else
                {
                    slotIcons[i].sprite = emptySlotSprite; 
                    slotIcons[i].color = Color.white; 
                    
                    if (slotNameTexts.Length > i && slotNameTexts[i] != null) slotNameTexts[i].text = "";
                    if (slotAuraEffects.Length > i && slotAuraEffects[i] != null) slotAuraEffects[i].SetActive(false);
                }
            }
            else
            {
                slotIcons[i].sprite = emptySlotSprite;
                slotIcons[i].color = new Color(1, 1, 1, 0.2f); 
                
                if (slotNameTexts.Length > i && slotNameTexts[i] != null) slotNameTexts[i].text = "Locked";
                if (slotAuraEffects.Length > i && slotAuraEffects[i] != null) slotAuraEffects[i].SetActive(false);
            }
        }
    }

    private void SetAuraColor(GameObject auraObj, Color c)
    {
        ParticleSystem ps = auraObj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.startColor = c;
        }
        Image img = auraObj.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    // ==========================================
    // 2. เมื่อกดที่ช่อง 1-4
    // ==========================================
    public void OnClickSlot(int slotIndex)
    {
        if (slotIndex < playerData.currentCampLevel)
        {
            currentSelectingSlot = slotIndex;
            OpenSelectionGrid();
        }
        else
        {
            if (campManager != null) campManager.ShowSystemMessage($"Requires Camp Level {slotIndex + 1} to unlock!");

            if (lockIcons.Length > slotIndex && lockIcons[slotIndex] != null)
            {
                StartCoroutine(AnimateLockIcon(lockIcons[slotIndex]));
            }
        }
    }

    private IEnumerator AnimateLockIcon(GameObject lockObj)
    {
        Button btn = lockObj.transform.parent.GetComponent<Button>();
        if (btn != null) btn.interactable = false;

        lockObj.SetActive(true); 

        Vector3 originalPos = lockObj.transform.localPosition;
        Vector3 originalScale = lockObj.transform.localScale;

        float duration = 0.4f;
        float elapsed = 0f;
        float shakeMagnitude = 8f; 

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float xOffset = Mathf.Sin(elapsed * 60f) * (shakeMagnitude * (1f - progress));
            lockObj.transform.localPosition = originalPos + new Vector3(xOffset, 0, 0);
            
            float scaleMultiplier = 1f - (Mathf.Sin(progress * Mathf.PI) * 0.2f);
            lockObj.transform.localScale = originalScale * scaleMultiplier;
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        lockObj.transform.localPosition = originalPos;
        lockObj.transform.localScale = originalScale;
        lockObj.SetActive(false); 
        
        if (btn != null) btn.interactable = true;
    }

    // ==========================================
    // เปิด/ปิด หน้าต่าง เลือกอาวุธ
    // ==========================================
    private void OpenSelectionGrid()
    {
        bool wasClosed = !selectionPanel.activeSelf; // เช็คว่าก่อนหน้านี้ปิดอยู่ไหม

        // ถ้าหน้าต่างปิดอยู่ ให้จัดตำแหน่งไปหลบทางซ้ายก่อนเปิด เพื่อเตรียมสไลด์เข้า
        if (wasClosed)
        {
            RectTransform rect = selectionPanel.GetComponent<RectTransform>();
            rect.anchoredPosition = panelOriginalPos - new Vector2(300f, 0f);
            panelCanvasGroup.alpha = 0f;
            selectionPanel.SetActive(true);
        }

        foreach (Transform child in gridContent)
        {
            Destroy(child.gameObject);
        }

        foreach (WeaponData weapon in allWeaponsInGame)
        {
            GameObject newBtnObj = Instantiate(weaponGridPrefab, gridContent);
            
            Image iconImage = newBtnObj.transform.Find("Icon").GetComponent<Image>();
            if (iconImage != null) iconImage.sprite = weapon.weaponIcon;

            Transform glow = newBtnObj.transform.Find("EquippedGlow");
            Transform slotNumTextObj = newBtnObj.transform.Find("SlotNumText"); 
            
            int equippedSlotIndex = System.Array.IndexOf(playerData.activeLoadout, weapon);
            bool isEquipped = (equippedSlotIndex >= 0);

            if (glow != null)
            {
                glow.gameObject.SetActive(isEquipped);
                if (isEquipped) SetAuraColor(glow.gameObject, weapon.auraColor);
            }

            if (slotNumTextObj != null)
            {
                TextMeshProUGUI numText = slotNumTextObj.GetComponent<TextMeshProUGUI>();
                if (isEquipped)
                {
                    numText.gameObject.SetActive(true);
                    numText.text = "P" + (equippedSlotIndex + 1).ToString(); 
                    numText.color = weapon.auraColor; 
                }
                else
                {
                    numText.gameObject.SetActive(false);
                }
            }

            Button btn = newBtnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => OnWeaponSelectedFromGrid(weapon));
        }

        // 🌟 เริ่มเล่นอนิเมชันเปิดหน้าต่าง
        if (wasClosed)
        {
            if (panelCoroutine != null) StopCoroutine(panelCoroutine);
            panelCoroutine = StartCoroutine(AnimatePanel(true));
        }
    }

    // ฟังก์ชันสั่งปิดเมื่อกดปุ่มกากบาท
    public void CloseSelectionGrid()
    {
        if (selectionPanel.activeSelf)
        {
            if (panelCoroutine != null) StopCoroutine(panelCoroutine);
            panelCoroutine = StartCoroutine(AnimatePanel(false));
        }
    }

    // 🌟 6. อนิเมชัน เฟด+สไลด์ ของหน้าต่างซ้าย
    private IEnumerator AnimatePanel(bool isOpen)
    {
        RectTransform rect = selectionPanel.GetComponent<RectTransform>();
        
        float duration = 0.25f; // ความเร็วในการสไลด์ (0.25 วินาที)
        float elapsed = 0f;

        Vector2 visiblePos = panelOriginalPos;
        Vector2 hiddenPos = panelOriginalPos - new Vector2(300f, 0f); // ระยะที่ซ่อนไปทางซ้าย 300 พิกเซล

        Vector2 startPos = isOpen ? hiddenPos : visiblePos;
        Vector2 endPos = isOpen ? visiblePos : hiddenPos;
        
        float startAlpha = isOpen ? 0f : 1f;
        float endAlpha = isOpen ? 1f : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); // สมูทตอนใกล้จบ (Ease Out)

            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, smoothT);

            yield return null;
        }

        rect.anchoredPosition = endPos;
        panelCanvasGroup.alpha = endAlpha;

        if (!isOpen)
        {
            selectionPanel.SetActive(false);
        }
    }

    private void OnWeaponSelectedFromGrid(WeaponData chosenWeapon)
    {
        int existingSlotIndex = System.Array.IndexOf(playerData.activeLoadout, chosenWeapon);
        
        bool isEquippingNewWeapon = false;
        bool isUnequipping = false;
        Sprite oldWeaponSprite = null; 

        if (existingSlotIndex == currentSelectingSlot)
        {
            oldWeaponSprite = playerData.activeLoadout[currentSelectingSlot].weaponIcon;
            playerData.activeLoadout[currentSelectingSlot] = null; 
            isUnequipping = true;
        }
        else
        {
            if (existingSlotIndex >= 0) playerData.activeLoadout[existingSlotIndex] = null;
            playerData.activeLoadout[currentSelectingSlot] = chosenWeapon; 
            isEquippingNewWeapon = true;
        }
        
        RefreshMainSlots(); 
        OpenSelectionGrid(); 

        if (isEquippingNewWeapon && slotIcons.Length > currentSelectingSlot && slotIcons[currentSelectingSlot] != null)
        {
            StartCoroutine(AnimateWeaponEquip(currentSelectingSlot));
        }
        else if (isUnequipping && slotIcons.Length > currentSelectingSlot && slotIcons[currentSelectingSlot] != null)
        {
            StartCoroutine(AnimateWeaponUnequip(currentSelectingSlot, oldWeaponSprite));
        }
    }

    private IEnumerator AnimateWeaponEquip(int slotIndex)
    {
        Image weaponImage = slotIcons[slotIndex];
        RectTransform rect = weaponImage.rectTransform;
        
        Vector2 endPos = iconOriginalPositions[slotIndex]; 
        Vector2 startPos = endPos + new Vector2(0, 50f);   

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); 

            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
            
            Color c = weaponImage.color;
            c.a = Mathf.Lerp(0f, 1f, smoothT);
            weaponImage.color = c;

            yield return null;
        }

        rect.anchoredPosition = endPos;
        Color finalColor = weaponImage.color;
        finalColor.a = 1f;
        weaponImage.color = finalColor;
    }

    private IEnumerator AnimateWeaponUnequip(int slotIndex, Sprite oldSprite)
    {
        Image weaponImage = slotIcons[slotIndex];
        RectTransform rect = weaponImage.rectTransform;
        
        Vector2 startPos = iconOriginalPositions[slotIndex];
        Vector2 endPos = startPos - new Vector2(0, 50f); 

        weaponImage.sprite = oldSprite;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); 

            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
            
            Color c = weaponImage.color;
            c.a = Mathf.Lerp(1f, 0f, smoothT); 
            weaponImage.color = c;

            yield return null;
        }

        rect.anchoredPosition = iconOriginalPositions[slotIndex];
        weaponImage.sprite = emptySlotSprite;
        
        Color finalColor = Color.white; 
        weaponImage.color = finalColor;
    }
}