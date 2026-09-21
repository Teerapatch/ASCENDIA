using System;
using System.Collections; // ต้องมีอันนี้สำหรับ Coroutine
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SkillQTEManager : MonoBehaviour
{
    public static SkillQTEManager Instance;

    [Header("UI References")]
    public Canvas mainCanvas;
    public GameObject qteCanvasPanel;

    [Header("Hit Detection UI")]
    public RectTransform hitZoneUI;
    public RectTransform indicatorUI;
    public Transform indicatorTip;

    private List<QTEStep> currentSteps;
    private int currentStepIndex = 0;
    private int successCount = 0;

    private bool isQTEActive = false;
    private float currentAngleDistance;
    private QTEStep activeStep;
    private Action<int> onSequenceComplete;
    private Camera uiCamera;

    private void Awake()
    {
        Instance = this;
        if (qteCanvasPanel != null) qteCanvasPanel.SetActive(false);
    }

    private void Start()
    {
        if (mainCanvas != null && mainCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = mainCanvas.worldCamera;
        else
            uiCamera = null;
    }

    public void StartQTE(List<QTEStep> steps, Action<int> onComplete)
    {
        if (steps == null || steps.Count == 0)
        {
            onComplete?.Invoke(0);
            return;
        }

        currentSteps = steps;
        onSequenceComplete = onComplete;
        currentStepIndex = 0;
        successCount = 0;

        if (qteCanvasPanel != null) qteCanvasPanel.SetActive(true);

        // โหลดรอบแรก
        LoadNextStep();
    }

    private void LoadNextStep()
    {
        if (currentStepIndex >= currentSteps.Count)
        {
            EndQTE();
            return;
        }

        // เปลี่ยนมาเรียก Coroutine แทนการรันทันที
        StartCoroutine(WaitAndStartStepRoutine());
    }

    private IEnumerator WaitAndStartStepRoutine()
    {
        activeStep = currentSteps[currentStepIndex];
        currentAngleDistance = activeStep.startingAngle;

        // 1. นำเข็มไปรอที่จุดเริ่มต้น และ ซ่อนเข็ม ไว้ก่อน
        if (indicatorUI != null)
        {
            indicatorUI.localRotation = Quaternion.Euler(0, 0, currentAngleDistance);
            indicatorUI.gameObject.SetActive(false);
            hitZoneUI.gameObject.SetActive(false);
        }

        // 2. หน่วงเวลาตามที่ตั้งไว้ใน ScriptableObject (ซิงก์ Animation)
        yield return new WaitForSeconds(activeStep.delayBeforeStart);

        // 3. ครบเวลา โชว์เข็ม และเปิดระบบรับ Input
        if (indicatorUI != null) indicatorUI.gameObject.SetActive(true);
        if (hitZoneUI != null) hitZoneUI.gameObject.SetActive(true);
        isQTEActive = true;
    }

    private void Update()
    {
        if (!isQTEActive || Keyboard.current == null) return;

        currentAngleDistance -= activeStep.spinSpeed * Time.deltaTime;

        if (indicatorUI != null)
        {
            indicatorUI.localRotation = Quaternion.Euler(0, 0, currentAngleDistance);
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CheckHit();
        }

        if (currentAngleDistance < -30f)
        {
            Debug.Log($"<color=red>QTE ฮิตที่ {currentStepIndex + 1} : FAILED! (ไม่ได้กด/ปล่อยเลยเป้า)</color>");
            ProceedToNextStep();
        }
    }

    private void CheckHit()
    {
        Vector2 tipScreenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, indicatorTip.position);
        bool isHit = RectTransformUtility.RectangleContainsScreenPoint(hitZoneUI, tipScreenPos, uiCamera);

        if (isHit)
        {
            Debug.Log($"<color=green>QTE ฮิตที่ {currentStepIndex + 1} : SUCCESS!</color>");
            successCount++;
        }
        else
        {
            Debug.Log($"<color=red>QTE ฮิตที่ {currentStepIndex + 1} : FAILED! (กดเร็วไป/ช้าไป)</color>");
        }

        ProceedToNextStep();
    }

    private void ProceedToNextStep()
    {
        isQTEActive = false; // ปิดรับ Input ทันทีเพื่อป้องกันการกดเบิ้ล
        currentStepIndex++;
        LoadNextStep(); // จะวนกลับไปเรียก Coroutine เพื่อดีเลย์รอบถัดไป
    }

    private void EndQTE()
    {
        isQTEActive = false;
        if (qteCanvasPanel != null) qteCanvasPanel.SetActive(false);
        onSequenceComplete?.Invoke(successCount);
    }
}