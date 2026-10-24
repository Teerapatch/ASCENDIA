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

    [Header("Current Targets")]
    private Transform currentAttacker;
    private Transform currentTarget;
    public CameraState CurrentState { get; private set; } = CameraState.Overview;

    private Vector3 sceneDefaultPosition;
    private Quaternion sceneDefaultRotation;
    private float sceneDefaultLensSize;

    private Coroutine transitionCoroutine;

    private void Awake()
    {
        Instance = this;
        if (activeVCam == null) activeVCam = GetComponent<CinemachineCamera>();

        if (activeVCam != null)
        {
            sceneDefaultPosition = activeVCam.transform.position;
            sceneDefaultRotation = activeVCam.transform.rotation;
            sceneDefaultLensSize = activeVCam.Lens.OrthographicSize;
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
                    focusPoint = currentAttacker.position + Vector3.up * 1.2f;
                }
                else
                {
                    focusPoint = Vector3.zero;
                }

                Vector3 lookDirUI = (focusPoint - sceneDefaultPosition).normalized;
                if (lookDirUI == Vector3.zero) lookDirUI = Vector3.forward;

                Vector3 skillCamPos = focusPoint - (lookDirUI * Mathf.Abs(skillSelectPreset.positionOffset.z)) + Vector3.up * skillSelectPreset.positionOffset.y;
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

        var lens = activeVCam.Lens;
        float startLens = lens.OrthographicSize;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / duration);

            activeVCam.transform.position = Vector3.Lerp(startPos, targetPos, t);
            activeVCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            lens.OrthographicSize = Mathf.Lerp(startLens, targetLens, t);
            activeVCam.Lens = lens;

            yield return null;
        }

        activeVCam.transform.position = targetPos;
        activeVCam.transform.rotation = targetRot;
        lens.OrthographicSize = targetLens;
        activeVCam.Lens = lens;
    }
}