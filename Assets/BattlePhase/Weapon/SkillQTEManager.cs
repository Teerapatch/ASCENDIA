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
    public RectTransform Bg_Circle;
    public RectTransform Bg_Diamond;
    public Transform indicatorTip;

    [Header("วงแหวนแบบส่วนโค้ง (ReArt v2: Bar เป็น Radial Fill)")]
    [Tooltip("ถ้าใส่ จะตัดสินด้วยมุมเข็มแทนการชนกล่อง และวาดส่วนโค้งนี้ให้ตรงกับช่วงที่กดผ่าน")]
    public UnityEngine.UI.Image successArc;
    [Tooltip("ความกว้างช่วงที่กดผ่าน (องศา) กึ่งกลางอยู่ด้านบน ที่เข็มหมุนครบรอบพอดี")]
    public float successArcDegrees = 60f;
    public Color successColor = new Color(0.55f, 1f, 0.75f, 1f);
    public Color failColor = new Color(1f, 0.35f, 0.35f, 1f);

    private Color arcBaseColor;
    private Coroutine flashRoutine;

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
        SetupSuccessArc();

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
            Bg_Circle.gameObject.SetActive(false);
            Bg_Diamond.gameObject.SetActive(false);
            hitZoneUI.gameObject.SetActive(false);
        }

        // 2. หน่วงเวลาตามที่ตั้งไว้ใน ScriptableObject (ซิงก์ Animation)
        yield return new WaitForSeconds(activeStep.delayBeforeStart);

        // 3. ครบเวลา โชว์เข็ม และเปิดระบบรับ Input
        if (indicatorUI != null) indicatorUI.gameObject.SetActive(true);
        if (hitZoneUI != null) hitZoneUI.gameObject.SetActive(true);
        if (Bg_Circle != null) Bg_Circle.gameObject.SetActive(true);
        if (Bg_Diamond != null) Bg_Diamond.gameObject.SetActive(true);
        isQTEActive = true;
    }

    private void Update()
    {
        if (!isQTEActive || Keyboard.current == null || GameExitMenu.IsOpen) return;

        currentAngleDistance -= activeStep.spinSpeed * Time.deltaTime;

        if (indicatorUI != null)
        {
            indicatorUI.localRotation = Quaternion.Euler(0, 0, currentAngleDistance);
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CheckHit();
        }

        if (currentAngleDistance < -Mathf.Max(30f, successArcDegrees * 0.5f))
        {
            FlashArc(failColor);
            SoundManager.Play(Sfx.QTEMiss);
            Debug.Log($"<color=red>QTE ฮิตที่ {currentStepIndex + 1} : FAILED! (ไม่ได้กด/ปล่อยเลยเป้า)</color>");
            ProceedToNextStep();
        }
    }

    // ส่วนโค้ง Radial เริ่มจากด้านบนหมุนตามเข็มนาฬิกา หมุนกลับครึ่งหนึ่งให้กึ่งกลางอยู่ด้านบน (ตรงกับมุม 0 ของเข็ม)
    private void SetupSuccessArc()
    {
        if (successArc == null) return;
        arcBaseColor = successArc.color;
        successArc.type = UnityEngine.UI.Image.Type.Filled;
        successArc.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        successArc.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
        successArc.fillClockwise = true;
        successArc.fillAmount = Mathf.Clamp01(successArcDegrees / 360f);
        successArc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, successArcDegrees * 0.5f);
    }

    private bool IsNeedleInArc()
    {
        // เข็มเริ่มที่ startingAngle (เช่น 360) หมุนลดลง ช่วงกดผ่านคือรอบ ๆ 0
        return Mathf.Abs(currentAngleDistance) <= successArcDegrees * 0.5f;
    }

    private void FlashArc(Color color)
    {
        if (successArc == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashArcRoutine(color));
    }

    private IEnumerator FlashArcRoutine(Color color)
    {
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            successArc.color = Color.Lerp(color, arcBaseColor, t / 0.25f);
            yield return null;
        }
        successArc.color = arcBaseColor;
        flashRoutine = null;
    }

    private void CheckHit()
    {
        bool isHit;
        if (successArc != null) isHit = IsNeedleInArc();
        else
        {
            Vector2 tipScreenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, indicatorTip.position);
            isHit = RectTransformUtility.RectangleContainsScreenPoint(hitZoneUI, tipScreenPos, uiCamera);
        }
        FlashArc(isHit ? successColor : failColor);
        SoundManager.Play(isHit ? Sfx.QTEHit : Sfx.QTEMiss);

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