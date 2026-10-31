using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ClimbUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject inventoryPanel;
    public Transform gridContent;
    public GameObject uiSlotPrefab;
    public int totalVisibleSlots = 15;

    [Header("Cameras")]
    public GameObject climbCamera;
    public GameObject inventoryCamera;

    [Header("Tooltip")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipNameText;
    public TextMeshProUGUI tooltipDescText;
    public RectTransform tooltipRect;

    private bool isInventoryOpen = false;

    // 🌟 ตัวแปรสำหรับคุมอนิเมชัน
    private Vector2 inventoryOriginalPos;
    private CanvasGroup invCanvasGroup;
    private Coroutine invCoroutine;

    private CanvasGroup tooltipCanvasGroup;
    private Coroutine tooltipCoroutine;

    private void Start()
    {
        // ตั้งค่า CanvasGroup สำหรับทำ Fade กระเป๋า
        invCanvasGroup = inventoryPanel.GetComponent<CanvasGroup>();
        if (invCanvasGroup == null) invCanvasGroup = inventoryPanel.AddComponent<CanvasGroup>();

        // เก็บตำแหน่งเดิมของกระเป๋าไว้
        RectTransform invRect = inventoryPanel.GetComponent<RectTransform>();
        inventoryOriginalPos = invRect.anchoredPosition;

        // ตั้งค่า CanvasGroup สำหรับทำ Fade Tooltip
        tooltipCanvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
        if (tooltipCanvasGroup == null) tooltipCanvasGroup = tooltipPanel.AddComponent<CanvasGroup>();

        inventoryPanel.SetActive(false);
        tooltipPanel.SetActive(false);
        SwitchCamera(false);

        if (InventoryManager.Instance != null) InventoryManager.Instance.OnInventoryChanged += RefreshInventoryUI;
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null) InventoryManager.Instance.OnInventoryChanged -= RefreshInventoryUI;
    }

    private void Update()
    {
        // 🌟 วิธีที่ชัวร์ขั้นสุด: ไม่สน State ผู้เล่นแล้ว เช็คจาก UI "หน้าเลือก Node" ตรงๆ เลย!
        bool isSelectingNode = false;

        if (NodeSelectionManager.Instance != null && NodeSelectionManager.Instance.mapUIPanel != null)
        {
            // ถ้าหน้าต่างแผนที่เปิดอยู่บนจอ แปลว่ากำลังเลือก Node ชัวร์ 100%
            if (NodeSelectionManager.Instance.mapUIPanel.activeInHierarchy)
            {
                isSelectingNode = true;
            }
        }

        // ⛔ ถ้ากำลังเลือก Node อยู่... ให้เตะออกไปเลย!
        if (isSelectingNode)
        {
            // เผื่อผู้เล่นแอบเปิดกระเป๋าทิ้งไว้ก่อนถึงหน้าเลือกโหนด ให้บังคับเก็บกระเป๋าให้เรียบร้อย
            if (isInventoryOpen)
            {
                ForceCloseInventory();
            }
            return; // สั่งหยุดทำงานบรรทัดนี้ ปุ่ม Tab ด้านล่างจะไม่มีวันถูกกดติด!
        }

        // ========================================================
        // โค้ดปุ่ม Tab ปกติ (จะทำงานเมื่อหน้าเลือก Node ปิดอยู่เท่านั้น)
        // ========================================================
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isInventoryOpen = !isInventoryOpen;
            SwitchCamera(isInventoryOpen);

            if (isInventoryOpen)
            {
                inventoryPanel.SetActive(true);
                RefreshInventoryUI();

                if (invCoroutine != null) StopCoroutine(invCoroutine);
                invCoroutine = StartCoroutine(AnimateInventoryPanel(true));

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                HideTooltip();

                if (invCoroutine != null) StopCoroutine(invCoroutine);
                invCoroutine = StartCoroutine(AnimateInventoryPanel(false));

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    // (ถ้ามีฟังก์ชันนี้อยู่แล้ว ให้คงไว้ ถ้าไม่มีให้ลบวงเล็บนี้ออกครับ)
    private void ForceCloseInventory()
    {
        isInventoryOpen = false;
        HideTooltip();

        if (inventoryCamera != null) inventoryCamera.SetActive(false);

        if (invCoroutine != null) StopCoroutine(invCoroutine);
        invCoroutine = StartCoroutine(AnimateInventoryPanel(false));
    }
    private void SwitchCamera(bool showInventoryCam)
    {
        if (inventoryCamera != null) inventoryCamera.SetActive(showInventoryCam);
        var controller = FindFirstObjectByType<PlayerCameraController>();
        if (controller != null)
        {
            if (showInventoryCam)
            {
                if (controller.walkCam) controller.walkCam.SetActive(false);
                if (controller.climbCam) controller.climbCam.SetActive(false);
            }
            else controller.RestoreGameplayCamera();
        }
        else if (climbCamera != null) climbCamera.SetActive(!showInventoryCam);
    }

    public void RefreshInventoryUI()
    {
        if (!isInventoryOpen || InventoryManager.Instance == null) return;

        foreach (Transform child in gridContent) Destroy(child.gameObject);

        List<InventorySlot> playerSlots = InventoryManager.Instance.slots;

        for (int i = 0; i < totalVisibleSlots; i++)
        {
            GameObject slotObj = Instantiate(uiSlotPrefab, gridContent);
            InventoryUISlot uiSlot = slotObj.GetComponent<InventoryUISlot>();

            if (i < playerSlots.Count) uiSlot.Setup(playerSlots[i], this);
            else uiSlot.Setup(null, this);
        }
    }

    // ==========================================
    // อนิเมชันกระเป๋า (สไลด์ + เฟด)
    // ==========================================
    private IEnumerator AnimateInventoryPanel(bool isOpen)
    {
        RectTransform rect = inventoryPanel.GetComponent<RectTransform>();
        float duration = 0.25f; // ความเร็วอนิเมชัน
        float elapsed = 0f;

        Vector2 visiblePos = inventoryOriginalPos;
        Vector2 hiddenPos = inventoryOriginalPos - new Vector2(300f, 0f); // ซ่อนไปทางซ้าย 300px

        Vector2 startPos = isOpen ? hiddenPos : visiblePos;
        Vector2 endPos = isOpen ? visiblePos : hiddenPos;

        float startAlpha = isOpen ? 0f : 1f;
        float endAlpha = isOpen ? 1f : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); // Ease-Out ให้นุ่มๆ ตรงปลาย

            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
            invCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, smoothT);
            yield return null;
        }

        rect.anchoredPosition = endPos;
        invCanvasGroup.alpha = endAlpha;

        if (!isOpen) inventoryPanel.SetActive(false);
    }

    // ==========================================
    // ระบบ Tooltip พร้อมอนิเมชันป๊อปอัป
    // ==========================================
    public void ShowTooltip(ItemData item, Vector3 slotPosition)
    {
        tooltipPanel.SetActive(true);
        tooltipNameText.text = item.itemName;
        tooltipDescText.text = item.description;

        if (tooltipCoroutine != null) StopCoroutine(tooltipCoroutine);
        tooltipCoroutine = StartCoroutine(AnimateTooltip(true));
    }

    public void HideTooltip()
    {
        if (tooltipPanel.activeSelf)
        {
            if (tooltipCoroutine != null) StopCoroutine(tooltipCoroutine);
            tooltipCoroutine = StartCoroutine(AnimateTooltip(false));
        }
    }

    private IEnumerator AnimateTooltip(bool isOpen)
    {
        float duration = 0.15f; // Tooltip ต้องเด้งไวๆ
        float elapsed = 0f;

        float startScale = isOpen ? 0.8f : 1f;
        float endScale = isOpen ? 1f : 0.8f;

        float startAlpha = isOpen ? 0f : 1f;
        float endAlpha = isOpen ? 1f : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            tooltipRect.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, smoothT);
            tooltipCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, smoothT);
            yield return null;
        }

        tooltipRect.localScale = Vector3.one * endScale;
        tooltipCanvasGroup.alpha = endAlpha;

        if (!isOpen) tooltipPanel.SetActive(false);
    }

    private void LateUpdate()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            Canvas canvas = tooltipPanel.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                Input.mousePosition,
                uiCamera,
                out Vector2 localPoint
            );

            tooltipRect.localPosition = new Vector3(localPoint.x + 30f, localPoint.y - 30f, 0f);
        }
    }
}