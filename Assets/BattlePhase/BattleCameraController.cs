using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public enum CameraState
{
    Overview,
    SkillSelecting,
    TargetSelecting,
    ActionExecution,
    Custom
}

public class BattleCameraController : MonoBehaviour
{
    public static BattleCameraController Instance;

    [Header("Cinemachine Core")]
    [SerializeField] private CinemachineCamera activeVCam;

    [Header("Scene Scale")]
    [Tooltip("ตัวคูณระยะกล้องตอนซูมเลือกสกิล/เป้าหมาย ใช้เมื่อฉากขยายตัวละครทั้งชุด (เช่น Characters scale 2 = ใส่ 2)")]
    public float framingScale = 1f;

    [Header("UI Focus Anchor")]
    [Tooltip("ลาก GameObject ของ UI เมนูสกิลมาใส่ที่นี่")]
    public Transform skillMenuUIPos;

    [System.Serializable]
    public struct CameraPreset
    {
        public string presetName;
        public Vector3 positionOffset;
        public float lensSize;
        public float transitionDuration;
    }

    [Header("Presets (Expedition 33 Style)")]
    private CameraPreset overviewPreset;

    public CameraPreset skillSelectPreset = new CameraPreset
    {
        presetName = "SkillSelect",
        positionOffset = new Vector3(0f, 0.5f, -3.5f),
        lensSize = 3.5f,
        transitionDuration = 0.4f
    };

    public CameraPreset targetFocusPreset = new CameraPreset
    {
        presetName = "TargetFocus",
        positionOffset = new Vector3(0f, 1.0f, -5.5f), // ค่ามาตรฐานสำหรับศัตรูขนาดกลาง
        lensSize = 4f,
        transitionDuration = 0.4f
    };

    [Header("Action Framing (ตอนผู้เล่นลงมือ คูณ framingScale)")]
    public float meleeCameraDistance = 7f;
    public float meleeCameraHeight = 1.5f;
    [Tooltip("ความสูงจุดที่กล้องมอง (ระดับอกตัวละคร)")]
    public float actionLookHeight = 1.5f;
    public float rangedCameraBack = 4.5f;
    public float rangedCameraSide = 2f;
    public float rangedCameraHeight = 2.2f;
    public float actionTransition = 0.35f;

    [Header("Current Targets")]
    private Transform currentAttacker;
    private Transform currentTarget;
    public CameraState CurrentState { get; private set; } = CameraState.Overview;

    private Vector3 sceneDefaultPosition;
    private Quaternion sceneDefaultRotation;
    private float sceneDefaultLensSize;

    private Coroutine transitionCoroutine;
    private Coroutine punchCoroutine;
    private float sceneDefaultFov;

    private void Awake()
    {
        Instance = this;
        if (activeVCam == null) activeVCam = GetComponent<CinemachineCamera>();

        if (activeVCam != null)
        {
            sceneDefaultPosition = activeVCam.transform.position;
            sceneDefaultRotation = activeVCam.transform.rotation;
            sceneDefaultLensSize = activeVCam.Lens.OrthographicSize;
            sceneDefaultFov = activeVCam.Lens.FieldOfView;
        }

        overviewPreset = new CameraPreset
        {
            presetName = "Overview",
            positionOffset = Vector3.zero,
            lensSize = sceneDefaultLensSize,
            transitionDuration = 0.4f
        };

        SetState(CameraState.Overview);
    }

    public void SetState(CameraState newState, Transform attacker = null, Transform target = null)
    {
        CurrentState = newState;
        if (attacker != null) currentAttacker = attacker;
        if (target != null) currentTarget = target;

        switch (newState)
        {
            case CameraState.Overview:
                // ใช้ ApplyCustomTransition เหมือนโหมดอื่น เพื่อหยุด Transition เก่าก่อน (เดิมวิ่งซ้อนกันได้)
                ApplyCustomTransition(sceneDefaultPosition, sceneDefaultRotation, sceneDefaultLensSize, overviewPreset.transitionDuration);
                break;

            case CameraState.SkillSelecting:
                Vector3 focusPoint;
                if (skillMenuUIPos != null)
                {
                    focusPoint = skillMenuUIPos.position;
                }
                else if (currentAttacker != null)
                {
                    focusPoint = currentAttacker.position + Vector3.up * (1.2f * framingScale);
                }
                else
                {
                    focusPoint = Vector3.zero;
                }

                Vector3 lookDirUI = (focusPoint - sceneDefaultPosition).normalized;
                if (lookDirUI == Vector3.zero) lookDirUI = Vector3.forward;

                Vector3 skillCamPos = focusPoint - (lookDirUI * Mathf.Abs(skillSelectPreset.positionOffset.z) * framingScale) + Vector3.up * (skillSelectPreset.positionOffset.y * framingScale);
                Quaternion skillCamRot = Quaternion.LookRotation(focusPoint - skillCamPos);

                ApplyCustomTransition(skillCamPos, skillCamRot, skillSelectPreset.lensSize, skillSelectPreset.transitionDuration);
                break;

            case CameraState.TargetSelecting:
                if (currentTarget != null)
                {
                    // --- [ระบบใหม่] กำหนดค่า Offset เริ่มต้นตาม Preset ปกติ (Medium Enemy) ---
                    float targetZOffset = targetFocusPreset.positionOffset.z; // ค่าติดลบ เช่น -5.5f (ระยะห่าง)
                    float targetYOffset = targetFocusPreset.positionOffset.y; // เช่น 1.0f (ความสูงกล้อง)
                    float targetLensSize = targetFocusPreset.lensSize;          // เช่น 4f (ขนาดเลนส์)
                    float enemyCenterHeight = 1.0f;                             // จุดศูนย์กลางความสูงของศัตรู

                    // --- เช็ก Tag ของศัตรูเพื่อปรับแต่ง Offset ตามขนาด (Size Scaling) ---
                    if (currentTarget.CompareTag("Boss") || currentTarget.CompareTag("Large"))
                    {
                        // ศัตรูตัวใหญ่ / บอส: ถอยกล้องออกห่าง, ยกกล้องสูงขึ้น, และขยาย Ortho Size
                        targetZOffset = -8.5f; // ถอยไกลขึ้น
                        targetYOffset = 1.8f;  // ยกสูงขึ้นเพื่อมองภาพรวม
                        targetLensSize = 5.5f; // ขยายมุมมองให้กว้างขึ้น
                        enemyCenterHeight = 1.5f; // เล็งตรงกลางอก/หัวบอส
                    }
                    else if (currentTarget.CompareTag("Small"))
                    {
                        // ศัตรูตัวเล็ก: ซูมกล้องเข้าไปใกล้ขึ้น
                        targetZOffset = -4.0f; // ขยับเข้าใกล้
                        targetYOffset = 0.8f;  // ลดระดับความสูงลงนิดหน่อย
                        targetLensSize = 3.2f; // บีบมุมมองให้แคบลง (ซูม)
                        enemyCenterHeight = 0.7f; // จุดศูนย์กลางต่ำลงตามตัว
                    }

                    // คำนวณตำแหน่งพิกัดเป้าหมาย
                    // ขยายระยะทั้งหมดตามสเกลของฉาก
                    targetZOffset *= framingScale;
                    targetYOffset *= framingScale;
                    enemyCenterHeight *= framingScale;

                    Vector3 enemyCenter = currentTarget.position + Vector3.up * enemyCenterHeight;
                    Vector3 lookDirEnemy = (enemyCenter - sceneDefaultPosition).normalized;
                    if (lookDirEnemy == Vector3.zero) lookDirEnemy = Vector3.forward;

                    Vector3 targetCamPos = enemyCenter - (lookDirEnemy * Mathf.Abs(targetZOffset)) + Vector3.up * targetYOffset;
                    Quaternion targetCamRot = Quaternion.LookRotation(enemyCenter - targetCamPos);

                    ApplyCustomTransition(targetCamPos, targetCamRot, targetLensSize, targetFocusPreset.transitionDuration);
                }
                break;
        }
    }

    // กล้องตอนลงมือ: ตีประชิดมองจากด้านข้างให้เห็นทั้งสองฝ่าย ยิง/ร่ายมองข้ามไหล่ไปหาเป้า
    public void FrameAction(Vector3 attackerPosition, Vector3 targetPosition, bool melee)
    {
        CurrentState = CameraState.ActionExecution;
        Vector3 look = Vector3.up * (actionLookHeight * framingScale);
        Vector3 toTarget = targetPosition - attackerPosition;
        toTarget.y = 0f;
        toTarget = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector3.forward;

        Vector3 focus;
        Vector3 camPos;
        if (melee)
        {
            focus = Vector3.Lerp(attackerPosition, targetPosition, 0.5f) + look;
            // อยู่ฝั่งเดียวกับกล้องภาพรวมเสมอ จะได้ไม่มุดเข้าหน้าผา
            Vector3 side = Vector3.Cross(Vector3.up, toTarget);
            if (Vector3.Dot(side, sceneDefaultPosition - focus) < 0f) side = -side;
            camPos = focus + (side - toTarget * 0.35f).normalized * (meleeCameraDistance * framingScale) + Vector3.up * ((meleeCameraHeight - actionLookHeight) * framingScale);
        }
        else
        {
            Vector3 side = Vector3.Cross(Vector3.up, toTarget);
            if (Vector3.Dot(side, sceneDefaultPosition - attackerPosition) < 0f) side = -side;
            focus = Vector3.Lerp(attackerPosition, targetPosition, 0.65f) + look;
            camPos = attackerPosition - toTarget * (rangedCameraBack * framingScale) + side * (rangedCameraSide * framingScale) + Vector3.up * (rangedCameraHeight * framingScale);
        }

        ApplyCustomTransition(camPos, Quaternion.LookRotation(focus - camPos), sceneDefaultLensSize, actionTransition);
    }

    private void ApplyCustomTransition(Vector3 targetPos, Quaternion targetRot, float lensSize, float duration)
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(SmoothCameraTransition(targetPos, targetRot, lensSize, duration));
    }

    private IEnumerator SmoothCameraTransition(Vector3 targetPos, Quaternion targetRot, float targetLens, float duration)
    {
        Vector3 startPos = activeVCam.transform.position;
        Quaternion startRot = activeVCam.transform.rotation;

        float startLens = activeVCam.Lens.OrthographicSize;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / duration);

            activeVCam.transform.position = Vector3.Lerp(startPos, targetPos, t);
            activeVCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            SetLensSize(Mathf.Lerp(startLens, targetLens, t));

            yield return null;
        }

        activeVCam.transform.position = targetPos;
        activeVCam.transform.rotation = targetRot;
        SetLensSize(targetLens);
    }

    // อ่านเลนส์ใหม่ทุกครั้ง เพื่อไม่ทับค่า FOV ที่ Punch กำลังขยับอยู่
    private void SetLensSize(float size)
    {
        var lens = activeVCam.Lens;
        lens.OrthographicSize = size;
        activeVCam.Lens = lens;
    }

    // เลนส์กระตุกเข้าแล้วคลายออกตอนโดน ใช้เวลาจริงเพื่อให้เห็นระหว่าง Hitstop
    public void Punch(float fovKick)
    {
        if (activeVCam == null || fovKick <= 0f) return;
        if (punchCoroutine != null) StopCoroutine(punchCoroutine);
        punchCoroutine = StartCoroutine(PunchRoutine(fovKick));
    }

    private IEnumerator PunchRoutine(float fovKick)
    {
        const float attack = 0.04f, release = 0.22f;
        for (float t = 0f; t < attack + release; t += Time.unscaledDeltaTime)
        {
            float amount = t < attack ? t / attack : 1f - Mathf.SmoothStep(0f, 1f, (t - attack) / release);
            SetFov(sceneDefaultFov - fovKick * amount);
            yield return null;
        }
        SetFov(sceneDefaultFov);
        punchCoroutine = null;
    }

    private void SetFov(float fov)
    {
        var lens = activeVCam.Lens;
        lens.FieldOfView = fov;
        activeVCam.Lens = lens;
    }
}