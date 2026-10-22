using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class InventoryUISlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image icon;
    public TextMeshProUGUI amountText; 
    
    private ItemData myItem;
    private ClimbUIManager uiManager;

    // 🌟 ตัวแปรอนิเมชันขยายรูป
    private Coroutine scaleCoroutine;
    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void Setup(InventorySlot slotData, ClimbUIManager manager)
    {
        uiManager = manager;

        if (slotData != null && slotData.item != null)
        {
            myItem = slotData.item;
            icon.sprite = myItem.icon; 
            icon.color = Color.white;
            amountText.text = slotData.amount > 1 ? slotData.amount.ToString() : "";
        }
        else
        {
            myItem = null;
            icon.color = new Color(0, 0, 0, 0); 
            amountText.text = "";
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (myItem != null && uiManager != null)
        {
            uiManager.ShowTooltip(myItem, transform.position);
            
            // ขยายกรอบ 1.1 เท่า
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(AnimateScale(originalScale * 1.1f));
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (uiManager != null)
        {
            uiManager.HideTooltip();
            
            // หดกรอบกลับขนาดเดิม
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(AnimateScale(originalScale));
        }
    }

    private IEnumerator AnimateScale(Vector3 targetScale)
    {
        float duration = 0.1f; // เด้งไวๆ
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        transform.localScale = targetScale;
    }
}