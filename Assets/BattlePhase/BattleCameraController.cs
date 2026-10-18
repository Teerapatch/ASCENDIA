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
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [System.Serializable]
    public struct CameraPreset
    {
        public string presetName;
        public Vector3 positionOffset; // Offset สัมพัทธ์ทิศทางกล้อง
        public float lensSize;
        public float transitionDuration;
    }

    [Header("Presets (Expedition 33 Style)")]
    private CameraPreset overviewPreset;

    // --- [ปรับปรุง] ตั้งค่า Offset ใหม่สำหรับการเลือกสกิลและเลือกเป้าหมาย ---
    public CameraPreset skillSelectPreset = new CameraPreset
    {
        presetName = "SkillSelect",
        positionOffset = new Vector3(-1.5f, 1.2f, -4f), // ยืนเยื้องซ้ายข้ามไหล่ตัวละคร
        lensSize = 4.5f,
        transitionDuration = 0.4f
    };

    public CameraPreset targetFocusPreset = new CameraPreset
    {
        presetName = "TargetFocus",
        positionOffset = new Vector3(0f, 1.0f, -5.5f), // มองตรงไปที่ศัตรูจากระยะกำลังดี
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
        if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void Start()
    {
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
                StartCoroutine(SmoothCameraTransition(sceneDefaultPosition, sceneDefaultRotation, sceneDefaultLensSize, overviewPreset.transitionDuration));
                break;

            case CameraState.SkillSelecting:
                if (currentAttacker != null)
                {
                    // เอามุมการหันหน้าของผู้เล่นมาคิดเป็นทิศทางกล้องข้ามไหล่
                    Vector3 focusPoint = currentAttacker.position + Vector3.up * 1.2f;
                    Vector3 camPos = focusPoint + (currentAttacker.rotation * skillSelectPreset.positionOffset);
                    Quaternion camRot = Quaternion.LookRotation(focusPoint - camPos);

                    ApplyCustomTransition(camPos, camRot, skillSelectPreset.lensSize, skillSelectPreset.transitionDuration);
                }
                break;

            case CameraState.TargetSelecting:
                if (currentTarget != null)
                {
                    // --- [แก้ไขสำคัญ] ให้กล้องหันไปหาจุดศูนย์กลางของศัตรูตรงๆ ---
                    Vector3 enemyCenter = currentTarget.position + Vector3.up * 1.0f; // ยกขึ้นมาระดับอก/หัวศัตรู

                    // คำนวณพิกัดกล้องโดยเอา Vector ทิศทางจาก Overview หรือจากจุดศูนย์กลางฉากมาช่วยปรับ
                    Vector3 lookDir = (enemyCenter - sceneDefaultPosition).normalized;
                    if (lookDir == Vector3.zero) lookDir = Vector3.forward;

                    // ถอยกล้องออกมาตามระยะที่ตั้งไว้ใน TargetFocusPreset
                    Vector3 targetCamPos = enemyCenter - (lookDir * Mathf.Abs(targetFocusPreset.positionOffset.z)) + Vector3.up * targetFocusPreset.positionOffset.y;
                    Quaternion targetCamRot = Quaternion.LookRotation(enemyCenter - targetCamPos);

                    ApplyCustomTransition(targetCamPos, targetCamRot, targetFocusPreset.lensSize, targetFocusPreset.transitionDuration);
                }
                break;
        }
    }

    public void TriggerImpulse(float force = 1.0f)
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(force);
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