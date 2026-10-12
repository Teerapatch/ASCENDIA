using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Cinemachine; // 🌟 ต้อง Using ตัวนี้เพิ่ม
using System.Collections;

public class CampMenuEffects : MonoBehaviour
{
    [Header("Camera Sway Settings")]
    public CinemachineCamera cineCam;  // 🌟 เปลี่ยนจาก Main Camera เป็น CinemachineCamera
    public float swayAmount = 2f;      
    public float swaySpeed = 5f;       

    [Header("Button Hover Settings")]
    public float hoverScaleMultiplier = 1.1f; 
    public float scaleSpeed = 15f;            

    private Quaternion originalCamRotation;

    private void Start()
    {
        // เก็บมุมเดิมของ Cinemachine ไว้
        if (cineCam != null)
        {
            originalCamRotation = cineCam.transform.rotation;
        }

        Button[] allButtons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (Button btn in allButtons)
        {
            AddHoverEffect(btn.gameObject);
        }
    }

    private void Update()
    {
        // สั่งหมุนกล้อง Cinemachine แทน!
        if (cineCam != null)
        {
            float mouseX = (Input.mousePosition.x / Screen.width) - 0.5f;
            float mouseY = (Input.mousePosition.y / Screen.height) - 0.5f;

            Quaternion targetRotation = originalCamRotation * Quaternion.Euler(-mouseY * swayAmount, mouseX * swayAmount, 0f);

            cineCam.transform.rotation = Quaternion.Lerp(cineCam.transform.rotation, targetRotation, Time.deltaTime * swaySpeed);
        }
    }

    // ==========================================
    // ระบบยัด Event อนิเมชันขยายปุ่มอัตโนมัติ (เหมือนเดิมเป๊ะ)
    // ==========================================
    private void AddHoverEffect(GameObject buttonObj)
    {
        EventTrigger trigger = buttonObj.GetComponent<EventTrigger>();
        if (trigger == null) trigger = buttonObj.AddComponent<EventTrigger>();

        Vector3 originalScale = buttonObj.transform.localScale;

        EventTrigger.Entry entryHover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryHover.callback.AddListener((data) => { StopAllCoroutines(); StartCoroutine(ScaleButton(buttonObj.transform, originalScale * hoverScaleMultiplier)); });
        trigger.triggers.Add(entryHover);

        EventTrigger.Entry exitHover = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitHover.callback.AddListener((data) => { StopAllCoroutines(); StartCoroutine(ScaleButton(buttonObj.transform, originalScale)); });
        trigger.triggers.Add(exitHover);
    }

    private IEnumerator ScaleButton(Transform btnTransform, Vector3 targetScale)
    {
        while (Vector3.Distance(btnTransform.localScale, targetScale) > 0.01f)
        {
            btnTransform.localScale = Vector3.Lerp(btnTransform.localScale, targetScale, Time.deltaTime * scaleSpeed);
            yield return null;
        }
        btnTransform.localScale = targetScale;
    }
}