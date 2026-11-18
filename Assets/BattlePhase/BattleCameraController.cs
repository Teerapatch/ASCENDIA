using UnityEngine;
using Unity.Cinemachine;

public enum CameraState
{
    Overview,
    SkillSelecting,
    TargetSelecting,
    ActionExecution,
    Custom,
    WeaponSelecting,
    EnemyTurn
}

// กล้องฉากต่อสู้แบบ Expedition 33: ภาพรวมถ่ายผ่านหลังไหล่ผู้เล่นไปหาศัตรู กล้องมีชีวิต (มือถือสั่นเบา ๆ โคจรช้า ๆ หายใจ)
// ทุกมุมคือ "เป้าหมาย" ที่กล้องไหลไปหาด้วยสปริง แล้วจึงบวกการขยับเล็ก ๆ ทับ ไม่ตัดภาพแข็ง ๆ
// ทำงานก่อน CinemachineBrain (ลำดับ -200) Brain จึงอ่านตำแหน่งของเฟรมนี้
[DefaultExecutionOrder(-200)]
public class BattleCameraController : MonoBehaviour
{
    public static BattleCameraController Instance;

    [Header("Cinemachine Core")]
    [SerializeField] private CinemachineCamera activeVCam;

    [Header("Scene Scale")]
    [Tooltip("ตัวคูณระยะกล้องทั้งหมด ใช้เมื่อฉากขยายตัวละครทั้งชุด (TurnBase_Phase = 2)")]
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

    [Header("Presets (ใช้เมื่อไม่มีจุดกล้องในฉาก)")]
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
        positionOffset = new Vector3(0f, 1.0f, -5.5f),
        lensSize = 4f,
        transitionDuration = 0.4f
    };

    [Header("จุดกล้อง (ลากใน Scene ได้ที่ BattleCameraPoints)")]
    public BattleCameraPoint skillPoint;
    public BattleCameraPoint switchWeaponPoint;
    [Tooltip("จุดมองศัตรู 1 จุดต่อตำแหน่งเกิด ระบบเลือกจุดที่ใกล้ศัตรูที่สุด")]
    public BattleCameraPoint[] focusPoints;
    [Tooltip("ถอยกล้องออกจากจุด Focus กี่เท่าสำหรับบอส/ศัตรูตัวใหญ่")]
    public float largeTargetPullback = 1.4f;

    [Header("Free Aim")]
    [Tooltip("องศา FOV ที่เปลี่ยนตอนเล็ง (ติดลบ = ซูมเข้า)")]
    public float aimFovOffset = -7f;
    public float aimZoomSpeed = 9f;

    [Header("Action Framing (ตอนผู้เล่นลงมือ คูณ framingScale)")]
    public float meleeCameraDistance = 7f;
    public float meleeCameraHeight = 1.5f;
    [Tooltip("ความสูงจุดที่กล้องมอง (ระดับอกตัวละคร)")]
    public float actionLookHeight = 1.5f;
    public float rangedCameraBack = 4.5f;
    public float rangedCameraSide = 2f;
    public float rangedCameraHeight = 2.2f;
    public float actionTransition = 0.35f;
    [Range(0f, .4f), Tooltip("ระหว่างลงมือ กล้องค่อย ๆ ดันเข้าหาจุดปะทะกี่ส่วนของระยะ")]
    public float actionPushIn = .12f;
    public float actionPushTime = 1.4f;

    [Header("มุมหลังตัวละคร (ตีประชิด, ยิง, เทิร์นศัตรู, ภาพรวมผ่านไหล่)")]
    [Tooltip("เปิด = กล้องอยู่หลังขวา ตัวละครเยื้องไปทางซ้ายของจอ / ปิด = ฝั่งเดียวกับกล้องในฉาก")]
    public bool cameraOnRightShoulder = true;
    [Tooltip("เลื่อนจุดมองไปทางขวาอีกกี่หน่วย (คูณ framingScale) ยิ่งมาก ตัวละครยิ่งชิดซ้าย")]
    public float frameLookShift = 1.2f;

    [Tooltip("ทุกการโจมตี (ผู้เล่นลงมือ/เทิร์นศัตรู): หันกล้องให้ผู้โจมตีกับเป้าหมายอยู่กลางจอพอดี ไม่เอียงซ้ายหรือขวา")]
    public bool centerAttackFraming = true;

    [Header("ตีประชิด: กล้องตามหลังตัวละครตอนพุ่งเข้าตี (คูณ framingScale)")]
    public float meleeBehindBack = 5.5f;
    public float meleeBehindSide = 2f;
    public float meleeBehindHeight = 2.4f;
    [Range(0f, 1f), Tooltip("จุดมองอยู่ระหว่างตัวละคร (0) กับศัตรู (1)")]
    public float meleeBehindLookAlong = .55f;
    [Tooltip("ความหน่วงตอนกล้องวิ่งตามตัวละคร (น้อย = ติดตัวมากขึ้น)")]
    public float meleeFollowSmoothTime = .12f;

    [Header("ภาพรวม (ค่าเริ่มต้น = มุมกล้องที่วางไว้ในฉาก)")]
    [Tooltip("เปิด = ภาพรวมถ่ายผ่านหลังไหล่ผู้เล่น ปิด = ใช้มุมกล้องในฉาก (มุมของอาร์ต)")]
    public bool overviewOverShoulder = false;
    public float shoulderBack = 6f;
    public float shoulderSide = 2.6f;
    public float shoulderHeight = 2.2f;
    public float shoulderLookHeight = 1.6f;
    [Range(0f, 1f), Tooltip("จุดมองอยู่ระหว่างผู้เล่น (0) กับกลุ่มศัตรู (1)")]
    public float shoulderLookAlong = .6f;

    [Header("เทิร์นศัตรู: ผ่านไหล่ผู้เล่นไปหาตัวที่โจมตี (คูณ framingScale)")]
    public float enemyTurnBack = 5f;
    public float enemyTurnSide = 2.2f;
    public float enemyTurnHeight = 2f;
    [Range(0f, 1f)] public float enemyTurnLookAlong = .75f;

    [Header("ความมีชีวิตของกล้อง")]
    [Tooltip("ความหน่วงตอนกล้องไหลไปมุมใหม่ (วินาที)")]
    public float followSmoothTime = .3f;
    [Tooltip("โคจรรอบจุดมองช้า ๆ ตอนภาพรวม (องศา)")]
    public float idleOrbitDegrees = 3f;
    public float idleOrbitPeriod = 11f;
    [Tooltip("มือถือสั่นเบา ๆ: ระยะเลื่อน (คูณ framingScale) และองศาหมุน")]
    public float handheldPosition = .06f;
    public float handheldRotation = .45f;
    public float handheldSpeed = .35f;
    [Tooltip("เลนส์หายใจเข้า-ออก (องศา FOV)")]
    public float breathingFov = .6f;

    [Header("การเปลี่ยนมุม (วินาทีจริง)")]
    public float blendOverview = .5f;
    public float blendEnemyTurn = .45f;
    [Tooltip("ตีประชิด ต้องทันการพุ่ง (dashIn 0.24 วินาที)")]
    public float blendMelee = .28f;
    public float blendRanged = .38f;
    [Tooltip("ขั้นต่ำของจุดกล้องในฉาก/เมนู (ใช้ค่าที่มากกว่าระหว่างนี้กับ transitionDuration ของจุด)")]
    public float blendMenuMin = .35f;
    [Range(0f, 1f), Tooltip("0 = ออกตัวนุ่ม (ช้าช่วงแรก), 1 = ออกตัวทันทีแล้วผ่อนเข้าที่ยาว ๆ")]
    public float blendSnappiness = .65f;
    [Tooltip("ความแรงการสั่นมือเปลี่ยนตามสถานะได้เร็วแค่ไหน (ต่อวินาที)")]
    public float livelinessChangeSpeed = 1.5f;

    [Header("UI")]
    [Tooltip("ให้เมนู BattleUI (World Space) ติดไปกับกล้อง เมนูจึงอยู่ที่เดิมบนจอแม้กล้องขยับ")]
    public bool attachBattleUIToCamera = true;

    public CameraState CurrentState { get; private set; } = CameraState.Overview;

    [Header("Parry Kick (กล้องกระแทกตอน Parry สำเร็จ ใช้เวลาจริง)")]
    public float parryKickRoll = 5f;
    [Tooltip("พุ่งเข้าหาจุดที่มองกี่หน่วย (คูณ framingScale)")]
    public float parryKickPush = .9f;
    public float parryKickTime = .5f;

    private enum Mode { Fixed, Overview, Enemy, Action, MeleeFollow }

    private Transform meleeFollower;
    private Vector3 meleeForward, meleeTarget;
    private Vector3 actionAttackerPoint, actionTargetPoint;
    private float kickStart = -10f, kickRollSign = 1f, kickStrength;

    private Transform currentAttacker;
    private Transform currentTarget;
    private Transform enemyTurnTarget;

    private Vector3 sceneDefaultPosition;
    private Quaternion sceneDefaultRotation;
    private float sceneDefaultLensSize;
    private float sceneDefaultFov;

    private Mode mode = Mode.Overview;
    private Vector3 fixedPosition, actionPosition, actionFocus;
    private Quaternion fixedRotation;
    private float smoothTime, actionStartTime;
    private Vector3 basePosition, baseVelocity;
    private Quaternion baseRotation;
    private float noiseTime, noiseSeed;
    private Vector3 blendFromPosition;
    private Quaternion blendFromRotation;
    private float blendDuration = 1f, blendElapsed = 1f;
    private float currentLiveliness = .4f;

    private Coroutine punchCoroutine;
    private float punchAmount;
    private float aimBlend;
    private bool aimZoomed;

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
            basePosition = sceneDefaultPosition;
            baseRotation = sceneDefaultRotation;
        }
        noiseSeed = Random.value * 100f;

        SetState(CameraState.Overview);
    }

    private void Start()
    {
        if (attachBattleUIToCamera) AttachBattleUI();
    }

    // เมนู BattleUI ถูกวางไว้ให้เต็มจอของมุมกล้องในฉาก ย้ายไปเป็นลูกของกล้องด้วยตำแหน่งสัมพัทธ์เดิม
    private void AttachBattleUI()
    {
        Camera cam = Camera.main;
        BattleArtUI art = FindAnyObjectByType<BattleArtUI>();
        if (cam == null || art == null) return;
        Canvas canvas = art.GetComponent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) return;

        Transform ui = art.transform;
        Quaternion inverse = Quaternion.Inverse(sceneDefaultRotation);
        Vector3 localPosition = inverse * (ui.position - sceneDefaultPosition);
        Quaternion localRotation = inverse * ui.rotation;
        Vector3 scale = ui.lossyScale;

        ui.SetParent(cam.transform, false);
        ui.localPosition = localPosition;
        ui.localRotation = localRotation;
        Vector3 parentScale = cam.transform.lossyScale;
        ui.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
    }

    public void SetState(CameraState newState, Transform attacker = null, Transform target = null)
    {
        CurrentState = newState;
        if (attacker != null) currentAttacker = attacker;
        if (target != null) currentTarget = target;

        // จุดกล้องที่ตั้งไว้ในฉากมาก่อนสูตรคำนวณเดิม
        if (MoveToPoint(newState)) return;

        switch (newState)
        {
            case CameraState.Overview:
                if (mode != Mode.Overview) BeginBlend(blendOverview);
                mode = Mode.Overview;
                smoothTime = followSmoothTime;
                break;

            case CameraState.SkillSelecting:
            {
                Vector3 focusPoint = skillMenuUIPos != null ? skillMenuUIPos.position
                    : currentAttacker != null ? currentAttacker.position + Vector3.up * (1.2f * framingScale)
                    : Vector3.zero;

                Vector3 lookDirUI = (focusPoint - sceneDefaultPosition).normalized;
                if (lookDirUI == Vector3.zero) lookDirUI = Vector3.forward;

                Vector3 skillCamPos = focusPoint - (lookDirUI * Mathf.Abs(skillSelectPreset.positionOffset.z) * framingScale) + Vector3.up * (skillSelectPreset.positionOffset.y * framingScale);
                ApplyCustomTransition(skillCamPos, Quaternion.LookRotation(focusPoint - skillCamPos), skillSelectPreset.lensSize, skillSelectPreset.transitionDuration);
                break;
            }

            case CameraState.TargetSelecting:
                if (currentTarget != null)
                {
                    float targetZOffset = targetFocusPreset.positionOffset.z;
                    float targetYOffset = targetFocusPreset.positionOffset.y;
                    float targetLensSize = targetFocusPreset.lensSize;
                    float enemyCenterHeight = 1.0f;

                    if (currentTarget.CompareTag("Boss") || currentTarget.CompareTag("Large"))
                    {
                        targetZOffset = -8.5f;
                        targetYOffset = 1.8f;
                        targetLensSize = 5.5f;
                        enemyCenterHeight = 1.5f;
                    }
                    else if (currentTarget.CompareTag("Small"))
                    {
                        targetZOffset = -4.0f;
                        targetYOffset = 0.8f;
                        targetLensSize = 3.2f;
                        enemyCenterHeight = 0.7f;
                    }

                    targetZOffset *= framingScale;
                    targetYOffset *= framingScale;
                    enemyCenterHeight *= framingScale;

                    Vector3 enemyCenter = currentTarget.position + Vector3.up * enemyCenterHeight;
                    Vector3 lookDirEnemy = (enemyCenter - sceneDefaultPosition).normalized;
                    if (lookDirEnemy == Vector3.zero) lookDirEnemy = Vector3.forward;

                    Vector3 targetCamPos = enemyCenter - (lookDirEnemy * Mathf.Abs(targetZOffset)) + Vector3.up * targetYOffset;
                    ApplyCustomTransition(targetCamPos, Quaternion.LookRotation(enemyCenter - targetCamPos), targetLensSize, targetFocusPreset.transitionDuration);
                }
                break;
        }
    }

    // เทิร์นศัตรู: ถ่ายผ่านไหล่ผู้เล่นไปหาตัวที่กำลังโจมตี ให้เห็นทั้งท่าโจมตีและตัวเราที่ต้อง Parry
    public void FrameEnemyTurn(Transform enemy)
    {
        if (enemy == null) return;
        CurrentState = CameraState.EnemyTurn;
        if (mode != Mode.Enemy || enemyTurnTarget != enemy) BeginBlend(blendEnemyTurn);
        enemyTurnTarget = enemy;
        mode = Mode.Enemy;
        smoothTime = followSmoothTime * .8f;
    }

    private bool MoveToPoint(CameraState state)
    {
        if (state == CameraState.SkillSelecting) return MoveToPoint(skillPoint, 1f);
        if (state == CameraState.WeaponSelecting) return MoveToPoint(switchWeaponPoint, 1f);
        if (state != CameraState.TargetSelecting || currentTarget == null || focusPoints == null) return false;

        BattleCameraPoint nearest = null;
        float best = float.MaxValue;
        foreach (BattleCameraPoint point in focusPoints)
        {
            if (point == null) continue;
            float distance = (point.transform.position - currentTarget.position).sqrMagnitude;
            if (distance < best) { best = distance; nearest = point; }
        }
        bool large = currentTarget.CompareTag("Boss") || currentTarget.CompareTag("Large");
        return MoveToPoint(nearest, large ? largeTargetPullback : 1f);
    }

    private bool MoveToPoint(BattleCameraPoint point, float pullback)
    {
        if (point == null) return false;

        Vector3 position = point.transform.position;
        if (pullback != 1f && currentTarget != null)
        {
            Vector3 focus = point.transform.forward * Vector3.Dot(currentTarget.position - position, point.transform.forward);
            position -= focus * (pullback - 1f);
        }
        ApplyCustomTransition(position, point.transform.rotation, sceneDefaultLensSize, point.transitionDuration);
        return true;
    }

    public void SetAimZoom(bool zoomed)
    {
        aimZoomed = zoomed;
    }

    // กล้องตอนลงมือ: ตีประชิดมองจากด้านข้างให้เห็นทั้งสองฝ่าย ยิง/ร่ายมองข้ามไหล่ แล้วค่อย ๆ ดันเข้าหาจุดปะทะ
    public void FrameAction(Vector3 attackerPosition, Vector3 targetPosition, bool melee)
    {
        CurrentState = CameraState.ActionExecution;
        attackerPosition += BodyOffset();
        Vector3 look = Vector3.up * (actionLookHeight * framingScale);
        Vector3 toTarget = targetPosition - attackerPosition;
        toTarget.y = 0f;
        toTarget = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector3.forward;

        // ตีประชิด: กล้องอยู่ด้านหลังตัวละคร วิ่งตามตอนพุ่งเข้าหา มองข้ามไหล่ไปที่ศัตรู
        PlayerCombatController player = PlayerCombatController.Instance;
        if (melee && player != null)
        {
            BeginBlend(blendMelee);
            mode = Mode.MeleeFollow;
            meleeFollower = player.transform;
            meleeForward = toTarget;
            meleeTarget = targetPosition;
            actionStartTime = Time.time;
            smoothTime = meleeFollowSmoothTime;
            return;
        }

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
            Vector3 side = ShoulderSide(toTarget, attackerPosition);
            focus = Vector3.Lerp(attackerPosition, targetPosition, 0.65f) + look + side * (frameLookShift * framingScale);
            camPos = attackerPosition - toTarget * (rangedCameraBack * framingScale) + side * (rangedCameraSide * framingScale) + Vector3.up * (rangedCameraHeight * framingScale);
        }

        BeginBlend(blendRanged);
        mode = Mode.Action;
        actionPosition = camPos;
        actionAttackerPoint = attackerPosition + look;
        actionTargetPoint = targetPosition + look;
        actionFocus = focus;
        actionStartTime = Time.time;
        smoothTime = actionTransition * .4f;
    }

    // มุมตายตัว (จุดกล้องในฉาก, เลือกสกิล/เป้าหมาย): กล้องไหลไปหาแล้วนิ่ง (ยังสั่นมือเบา ๆ)
    private void ApplyCustomTransition(Vector3 targetPos, Quaternion targetRot, float lensSize, float duration)
    {
        bool changed = mode != Mode.Fixed || (fixedPosition - targetPos).sqrMagnitude > .0001f || Quaternion.Angle(fixedRotation, targetRot) > .1f;
        if (changed) BeginBlend(Mathf.Max(duration, blendMenuMin));
        mode = Mode.Fixed;
        fixedPosition = targetPos;
        fixedRotation = targetRot;
        smoothTime = Mathf.Max(.05f, duration * .4f);
        if (activeVCam != null) SetLensSize(lensSize);
    }

    private void LateUpdate()
    {
        if (activeVCam == null) return;
        float dt = Time.deltaTime;

        float targetLiveliness = GoalPose(out Vector3 goalPosition, out Quaternion goalRotation);
        if (aimZoomed) targetLiveliness *= .25f; // เล็งต้องนิ่ง

        // นาฬิกาของกล้อง: เวลาจริง (สโลว์ตอน Parry ไม่ทำให้กล้องหนืด) แต่หยุดตอน Hitstop
        float camDt = HitstopManager.IsFrozen ? 0f : Time.unscaledDeltaTime;
        currentLiveliness = Mathf.MoveTowards(currentLiveliness, targetLiveliness, camDt * livelinessChangeSpeed);
        float liveliness = currentLiveliness;

        // เปลี่ยนมุมแบบเร่ง-ผ่อน: ไล่จากท่าตอนเริ่มเปลี่ยน ไปหาเป้าหมาย (ที่อาจกำลังขยับ) ไม่ใช่สปริงไล่ตามตรง ๆ
        blendElapsed += camDt;
        bool blending = blendElapsed < blendDuration;
        float k = blending ? BlendCurve(blendElapsed / blendDuration) : 1f;

        if (camDt > 0f)
        {
            if (blending)
            {
                // ระหว่างเปลี่ยนมุม: วางตามเส้นโค้งตรง ๆ ไม่มีสปริงซ้อนให้หน่วง (เก็บความเร็วไว้ส่งต่อให้ช่วงตามเป้า)
                Vector3 next = Vector3.LerpUnclamped(blendFromPosition, goalPosition, k);
                baseVelocity = (next - basePosition) / camDt;
                basePosition = next;
                baseRotation = Quaternion.Slerp(blendFromRotation, goalRotation, k);
            }
            else
            {
                basePosition = Vector3.SmoothDamp(basePosition, goalPosition, ref baseVelocity, smoothTime, Mathf.Infinity, camDt);
                baseRotation = Quaternion.Slerp(baseRotation, goalRotation, 1f - Mathf.Exp(-camDt * 3.5f / Mathf.Max(.05f, smoothTime)));
            }
        }
        if (dt > 0f) noiseTime += dt * handheldSpeed;

        // มือถือสั่นเบา ๆ (Perlin) ต่อเนื่อง ไม่วนซ้ำ หยุดตอน Hitstop เพราะใช้เวลาเกม
        float scale = handheldPosition * framingScale * liveliness;
        Vector3 offset = new Vector3(Noise(0), Noise(1), 0f) * scale;
        Quaternion wobble = Quaternion.Euler(Noise(2) * handheldRotation * liveliness, Noise(3) * handheldRotation * liveliness, Noise(4) * handheldRotation * .5f * liveliness);
        // Parry: พุ่งเข้าแล้วเอียงกล้องตามทิศที่ลาก คลายออกช้า ๆ (เห็นระหว่าง Hitstop เพราะใช้เวลาจริง)
        float kickT = (Time.unscaledTime - kickStart) / Mathf.Max(.05f, parryKickTime);
        float kick = kickT < 0f || kickT >= 1f ? 0f : (kickT < .08f ? kickT / .08f : 1f - Mathf.SmoothStep(0f, 1f, (kickT - .08f) / .92f)) * kickStrength;
        offset += Vector3.forward * (parryKickPush * framingScale * kick);
        wobble *= Quaternion.Euler(0f, 0f, parryKickRoll * kickRollSign * kick);

        activeVCam.transform.SetPositionAndRotation(basePosition + baseRotation * offset, baseRotation * wobble);

        aimBlend = Mathf.MoveTowards(aimBlend, aimZoomed ? 1f : 0f, Time.unscaledDeltaTime * aimZoomSpeed);
        SetFov(sceneDefaultFov + aimFovOffset * aimBlend - punchAmount + Noise(5) * breathingFov * liveliness);
    }

    // เรียกตอน Parry สำเร็จ: swipe = ทิศที่ผู้เล่นลากบนจอ กล้องเอียงตามทิศนั้น
    public void ParryKick(Vector2 swipe, float strength = 1f)
    {
        kickStart = Time.unscaledTime;
        kickStrength = strength;
        kickRollSign = Mathf.Abs(swipe.x) > .05f ? -Mathf.Sign(swipe.x) : (Random.value < .5f ? -1f : 1f);
    }

    // เริ่มเปลี่ยนมุมจากท่าปัจจุบันของกล้อง (ต่อจากกลางทางได้ ไม่กระโดด)
    private void BeginBlend(float duration)
    {
        blendFromPosition = basePosition;
        blendFromRotation = baseRotation;
        blendDuration = Mathf.Max(.01f, duration);
        blendElapsed = 0f;
    }

    // ผสมระหว่างเร่ง-ผ่อนนุ่ม ๆ กับออกตัวไวแล้วผ่อนยาว: กล้องเริ่มขยับทันที (ไม่หน่วง) แต่เข้าที่นุ่ม (ไม่กระแทก)
    private float BlendCurve(float x)
    {
        x = Mathf.Clamp01(x);
        float soft = x * x * (3f - 2f * x);
        float inv = 1f - x;
        float snappy = 1f - inv * inv * inv * inv;
        return Mathf.Lerp(soft, snappy, blendSnappiness);
    }

    private float Noise(int channel)
    {
        return (Mathf.PerlinNoise(noiseSeed + channel * 17.3f, noiseTime) - .5f) * 2f;
    }

    // เป้าหมายของกล้องเฟรมนี้ คืนความแรงของความมีชีวิต (มือสั่น/หายใจ) ตามสถานะ
    private float GoalPose(out Vector3 position, out Quaternion rotation)
    {
        position = sceneDefaultPosition;
        rotation = sceneDefaultRotation;

        switch (mode)
        {
            case Mode.Fixed:
                position = fixedPosition;
                rotation = fixedRotation;
                // เมนูสกิล/สลับอาวุธเป็น World Space ที่วางให้พอดีมุมนี้ กล้องต้องนิ่งสนิท
                return CurrentState == CameraState.SkillSelecting || CurrentState == CameraState.WeaponSelecting ? 0f : .5f;

            case Mode.MeleeFollow:
                if (meleeFollower != null)
                {
                    Vector3 origin = meleeFollower.position + BodyOffset();
                    Vector3 side = ShoulderSide(meleeForward, origin);
                    position = origin - meleeForward * (meleeBehindBack * framingScale) + side * (meleeBehindSide * framingScale) + Vector3.up * (meleeBehindHeight * framingScale);
                    Vector3 lookAt = Vector3.Lerp(origin + Vector3.up * (actionLookHeight * framingScale), meleeTarget + Vector3.up * (actionLookHeight * framingScale), meleeBehindLookAlong)
                        + side * (frameLookShift * framingScale);
                    float push = actionPushIn * Mathf.SmoothStep(0f, 1f, (Time.time - actionStartTime) / Mathf.Max(.1f, actionPushTime));
                    Vector3 chest = origin + Vector3.up * (actionLookHeight * framingScale);
                    Vector3 enemyChest = meleeTarget + Vector3.up * (actionLookHeight * framingScale);
                    if (centerAttackFraming) lookAt = Vector3.Lerp(chest, enemyChest, .5f);
                    position = Vector3.Lerp(position, lookAt, push);
                    rotation = centerAttackFraming ? CenterOn(position, chest, enemyChest) : Quaternion.LookRotation(lookAt - position);
                    return 1.4f;
                }
                goto case Mode.Overview;

            case Mode.Action:
            {
                float push = actionPushIn * Mathf.SmoothStep(0f, 1f, (Time.time - actionStartTime) / Mathf.Max(.1f, actionPushTime));
                Vector3 focus = centerAttackFraming ? Vector3.Lerp(actionAttackerPoint, actionTargetPoint, .5f) : actionFocus;
                position = Vector3.Lerp(actionPosition, focus, push);
                rotation = centerAttackFraming ? CenterOn(position, actionAttackerPoint, actionTargetPoint) : Quaternion.LookRotation(focus - position);
                return 1.4f;
            }

            case Mode.Enemy:
                if (enemyTurnTarget != null && ShoulderPose(enemyTurnTarget, enemyTurnBack, enemyTurnSide, enemyTurnHeight, enemyTurnLookAlong, out position, out Vector3 enemyLook))
                {
                    rotation = centerAttackFraming && PlayerCombatController.Instance != null
                        ? CenterOn(position, PlayerCombatController.Instance.transform.position + BodyOffset() + Vector3.up * (actionLookHeight * framingScale), BodyCenter(enemyTurnTarget))
                        : Quaternion.LookRotation(enemyLook - position);
                    return 1.2f;
                }
                goto case Mode.Overview;

            case Mode.Overview:
                if (overviewOverShoulder && ShoulderPose(null, shoulderBack, shoulderSide, shoulderHeight, shoulderLookAlong, out position, out Vector3 look))
                {
                    // โคจรช้า ๆ รอบจุดมอง ภาพรวมจึงไม่นิ่งตาย
                    float angle = Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(1f, idleOrbitPeriod)) * idleOrbitDegrees;
                    position = look + Quaternion.AngleAxis(angle, Vector3.up) * (position - look);
                    rotation = Quaternion.LookRotation(look - position);
                    return 1f;
                }
                // มุมเดิมของฉาก สั่นเบามาก ๆ ให้ยังมีชีวิต
                position = sceneDefaultPosition;
                rotation = sceneDefaultRotation;
                return .4f;
        }
        return 1f;
    }

    // ตำแหน่งกล้องหลังไหล่ผู้เล่น มองไปหา target (ไม่ระบุ = กลางกลุ่มศัตรู) ฝั่งไหล่เดียวกับกล้องในฉาก (ฝั่งที่โล่ง)
    private bool ShoulderPose(Transform target, float back, float side, float height, float along, out Vector3 position, out Vector3 look)
    {
        position = look = Vector3.zero;
        PlayerCombatController player = PlayerCombatController.Instance;
        if (player == null) return false;

        Vector3 enemyPoint;
        if (target != null) enemyPoint = BodyCenter(target);
        else
        {
            CombatManager combat = CombatManager.Instance;
            if (combat == null || combat.enemies.Count == 0) return false;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (EnemyController enemy in combat.enemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                sum += BodyCenter(enemy.transform);
                count++;
            }
            if (count == 0) return false;
            enemyPoint = sum / count;
        }

        Vector3 origin = player.transform.position + BodyOffset();
        Vector3 forward = enemyPoint - origin;
        forward.y = 0f;
        if (forward.sqrMagnitude < .01f) return false;
        forward.Normalize();

        Vector3 right = ShoulderSide(forward, origin);

        position = origin - forward * (back * framingScale) + right * (side * framingScale) + Vector3.up * (height * framingScale);
        look = Vector3.Lerp(origin + Vector3.up * (shoulderLookHeight * framingScale), enemyPoint, along) + right * (frameLookShift * framingScale);
        return true;
    }

    // หันกล้องให้จุด a กับ b อยู่ห่างจากกลางจอเท่ากันทั้งแนวนอนและแนวตั้ง (เฉลี่ยมุมที่มองเห็นทั้งสองจุด)
    // ตัวที่อยู่ใกล้กล้องจึงไม่ดันภาพไปซ้ายหรือขวา
    private static Quaternion CenterOn(Vector3 from, Vector3 a, Vector3 b)
    {
        Vector3 da = a - from, db = b - from;
        float yawA = Mathf.Atan2(da.x, da.z) * Mathf.Rad2Deg;
        float yawB = Mathf.Atan2(db.x, db.z) * Mathf.Rad2Deg;
        float pitchA = -Mathf.Atan2(da.y, new Vector2(da.x, da.z).magnitude) * Mathf.Rad2Deg;
        float pitchB = -Mathf.Atan2(db.y, new Vector2(db.x, db.z).magnitude) * Mathf.Rad2Deg;
        return Quaternion.Euler((pitchA + pitchB) * .5f, yawA + Mathf.DeltaAngle(yawA, yawB) * .5f, 0f);
    }

    // โมเดลผู้เล่นถูกเลื่อนออกจาก Player_Holder (modelLocalPosition ราว 4 หน่วยในฉากนี้) กล้องต้องอิงตัวที่เห็นจริง
    // ไม่งั้นกล้อง "หลังขวา" ของ Player_Holder จะไปอยู่หลังตัวละครตรง ๆ หรือฝั่งซ้าย
    private static Vector3 BodyOffset()
    {
        PlayerBattleAnimator animator = PlayerBattleAnimator.Instance;
        PlayerCombatController player = PlayerCombatController.Instance;
        if (animator == null || player == null) return Vector3.zero;
        Vector3 offset = animator.ModelPosition - player.transform.position;
        offset.y = 0f;
        return offset;
    }

    // ฝั่งไหล่ของกล้องหลังตัวละคร: ขวาของทิศที่หันไปหาศัตรู (ตัวละครจึงเยื้องไปทางซ้ายของจอ)
    // ปิด cameraOnRightShoulder = เลือกฝั่งเดียวกับกล้องในฉากแบบเดิม
    private Vector3 ShoulderSide(Vector3 forward, Vector3 origin)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward.normalized);
        if (!cameraOnRightShoulder && Vector3.Dot(right, sceneDefaultPosition - origin) < 0f) right = -right;
        return right;
    }

    private static Vector3 BodyCenter(Transform target)
    {
        Collider body = target.GetComponent<Collider>();
        return body != null && body.enabled ? body.bounds.center : target.position;
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

    private System.Collections.IEnumerator PunchRoutine(float fovKick)
    {
        const float attack = 0.04f, release = 0.22f;
        for (float t = 0f; t < attack + release; t += Time.unscaledDeltaTime)
        {
            float amount = t < attack ? t / attack : 1f - Mathf.SmoothStep(0f, 1f, (t - attack) / release);
            punchAmount = fovKick * amount;
            yield return null;
        }
        punchAmount = 0f;
        punchCoroutine = null;
    }

    private void SetFov(float fov)
    {
        var lens = activeVCam.Lens;
        lens.FieldOfView = fov;
        activeVCam.Lens = lens;
    }
}
