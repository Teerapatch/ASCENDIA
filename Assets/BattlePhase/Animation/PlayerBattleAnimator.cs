using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// เล่นอนิเมชันการต่อสู้ของผู้เล่น: สร้างโมเดลของแต่ละอาวุธจาก FBX ของอาร์ต แล้วสลับตามอาวุธที่ถือ
// PlayerCombatController เรียก PlayAction และรอจนจบ ดาเมจจึงเกิดตรงจังหวะที่ท่าโดนเป้า
public class PlayerBattleAnimator : MonoBehaviour
{
    public static PlayerBattleAnimator Instance;

    private class Rig
    {
        public BattleWeaponAnimation data;
        public GameObject root;
        public Animator animator;
    }

    private BattleAnimationSet set;
    private readonly Dictionary<WeaponType, Rig> rigs = new Dictionary<WeaponType, Rig>();
    private Rig current;
    private Vector3 homeWorld;
    private int parryIndex;
    private Coroutine faceRoutine;
    private float oneShotUntil;

    private enum AimPhase { None, Draw, Hold, Fire, Return }
    private const float TwistFollowSpeed = 24f;
    private const float TwistBlendTime = 0.12f;
    private const float TwistMaxYaw = 70f;
    private const float TwistMaxPitch = 35f;
    private AimPhase aimPhase;
    private bool aiming, hasHoldBarrel;
    private float aimTime, aimClipLength = 1f, twist;
    private int holdFrames;
    private Vector3 aimPoint, smoothedAim, holdBarrel;
    private Transform aimBone, upperPivot;
    private readonly List<Transform> aimRoots = new List<Transform>();
    private WeaponType? weaponBeforeAim;
    private int moveToken;

    private void Awake()
    {
        Instance = this;
        set = Resources.Load<BattleAnimationSet>("BattleAnimationSet");
        homeWorld = transform.position;
    }

    private void Start()
    {
        if (set == null) return;

        // ซ่อนโมเดลท่านิ่งของอาร์ต เมื่อมีตัวที่ขยับได้แทนแล้ว
        Transform pose = transform.Find("POSE_ATTACK");
        if (pose != null) pose.gameObject.SetActive(false);

        PlayerCombatController player = GetComponent<PlayerCombatController>();
        if (player != null && player.ActiveWeapon != null) SetWeapon(player.ActiveWeapon.weaponType);
    }

    public void SetWeapon(WeaponType type)
    {
        weaponBeforeAim = null;
        if (set == null || (current != null && current.data.weapon == type)) return;

        if (!rigs.TryGetValue(type, out Rig rig))
        {
            rig = CreateRig(type);
            if (rig == null) return;
            rigs[type] = rig;
        }

        if (current != null) current.root.SetActive(false);
        current = rig;
        current.root.SetActive(true);
        current.animator.Play(current.data.idleState, 0, UnityEngine.Random.value);
    }

    private Rig CreateRig(WeaponType type)
    {
        BattleWeaponAnimation data = set.Find(type);
        if (data == null || data.model == null || data.controller == null)
        {
            Debug.LogWarning($"ไม่มีอนิเมชันของอาวุธ {type} ใน BattleAnimationSet");
            return null;
        }

        GameObject root = Instantiate(data.model, transform);
        root.name = "BattleModel_" + type;
        root.transform.localPosition = set.modelLocalPosition;
        root.transform.localRotation = Quaternion.Euler(set.modelLocalEuler);
        root.transform.localScale = set.modelLocalScale;

        Animator animator = root.GetComponent<Animator>();
        if (animator == null) animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = data.controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        root.SetActive(false);
        return new Rig { data = data, root = root, animator = animator };
    }

    public BattleClip AttackClip => current != null ? current.data.attack : null;

    public BattleClip SkillClip(WeaponSkill skill)
    {
        return current != null && skill != null ? current.data.FindSkill(skill.skillName) : null;
    }

    public float RecoveryDelay => set != null ? set.recoveryDelay : 0.2f;

    private bool HasState(string state)
    {
        return current != null && !string.IsNullOrEmpty(state) && current.animator.HasState(0, Animator.StringToHash(state));
    }

    // เล่นท่าจนจบ: ถ้ายังไม่ปล่อย (ready = false เช่นกำลังกด QTE) จะง้างค้างก่อนถึงจังหวะโดน
    public IEnumerator PlayAction(BattleClip clip, Func<bool> ready, Action<int, int> onHit)
    {
        if (clip == null || !HasState(clip.state))
        {
            while (ready != null && !ready()) yield return null;
            onHit?.Invoke(0, 1);
            yield break;
        }

        List<float> hits = clip.hits != null && clip.hits.Count > 0 ? clip.hits : new List<float> { clip.impact };
        Animator animator = current.animator;
        animator.speed = Mathf.Max(0.01f, clip.speed);
        animator.CrossFadeInFixedTime(clip.state, set.crossFade, 0, 0f);
        yield return null;

        int next = 0;
        float firstHit = hits[0];
        int stateHash = Animator.StringToHash(clip.state);
        float released = -1f;

        while (true)
        {
            float time = StateTime(animator, stateHash);
            bool isReady = ready == null || ready();
            if (isReady && released < 0f) released = Time.time;

            if (!isReady && time >= firstHit * 0.85f)
                animator.speed = time >= firstHit * 0.98f ? 0f : set.qteHoldSpeed;
            else
                animator.speed = Mathf.Max(0.01f, clip.speed);

            while (isReady && next < hits.Count && time >= hits[next])
            {
                onHit?.Invoke(next, hits.Count);
                next++;
            }

            // จบท่าแล้ว หรือกันค้าง: State ถูกเปลี่ยนไปแล้ว/รอเกินเวลา
            bool finished = next >= hits.Count && time >= 0.95f;
            bool stuck = released >= 0f && Time.time - released > 12f;
            if (finished || stuck) break;
            yield return null;
        }

        while (next < hits.Count)
        {
            onHit?.Invoke(next, hits.Count);
            next++;
        }
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(current.data.idleState, set.crossFade * 1.5f, 0);
    }

    private static float StateTime(Animator animator, int stateHash)
    {
        AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        return info.shortNameHash == stateHash ? info.normalizedTime : 0f;
    }

    public void PlayOneShot(string state)
    {
        if (!HasState(state)) return;
        current.animator.speed = 1f;
        current.animator.CrossFadeInFixedTime(state, set.crossFade, 0, 0f);

        foreach (AnimationClip clip in current.animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == state) oneShotUntil = Time.time + clip.length * 0.85f;
        }
    }

    // รอท่าครั้งเดียว (เช่น Parry) ให้เล่นจบก่อน เทิร์นถัดไปอาจสลับโมเดลอาวุธแล้วตัดท่าทิ้ง
    public IEnumerator WaitForOneShot()
    {
        while (Time.time < oneShotUntil) yield return null;
    }

    public void PlayParry()
    {
        if (current == null || current.data.parries.Count == 0) return;
        PlayOneShot(current.data.parries[parryIndex++ % current.data.parries.Count]);
    }

    private bool HasAimPose => current != null && current.data.weapon == WeaponType.Gun && HasState(current.data.aimState);

    // เริ่มเล็ง: ไม่ว่าถืออาวุธอะไรจะเปลี่ยนเป็นปืน แล้วเล่น QuickSkill2 ไปค้างที่ท่าเล็ง
    public void BeginAim()
    {
        if (current == null) return;

        if (current.data.weapon != WeaponType.Gun)
        {
            WeaponType held = current.data.weapon;
            SetWeapon(WeaponType.Gun);
            if (current == null || current.data.weapon != WeaponType.Gun) return;
            weaponBeforeAim = held;
        }
        if (!HasAimPose) return;

        aiming = true;
        if (aimPhase == AimPhase.Fire) return;
        if (aimPhase == AimPhase.None)
        {
            aimTime = 0f;
            CollectAimBones();
        }
        aimPhase = AimPhase.Draw;
    }

    public void SetAimPoint(Vector3 worldPoint)
    {
        aimPoint = worldPoint;
    }

    public void EndAim()
    {
        aiming = false;
        if (aimPhase == AimPhase.Draw || aimPhase == AimPhase.Hold) aimPhase = AimPhase.Return;
    }

    // ยิงจริงตอนเล็ง: กระโดดไปจังหวะยิงทันที
    public void PlayFreeAimShot()
    {
        if (aiming && HasAimPose)
        {
            aimPhase = AimPhase.Fire;
            aimTime = current.data.aimFireStart;
            return;
        }
        if (AttackClip != null) PlayOneShot(AttackClip.state);
    }

    private void LateUpdate()
    {
        if (current == null || aimPhase == AimPhase.None) return;

        BattleWeaponAnimation data = current.data;
        float drawStep = Time.deltaTime * data.aimDrawSpeed / aimClipLength;
        switch (aimPhase)
        {
            case AimPhase.Draw:
                aimTime += drawStep;
                if (aimTime >= data.aimHoldTime)
                {
                    aimTime = data.aimHoldTime;
                    aimPhase = AimPhase.Hold;
                }
                break;
            case AimPhase.Fire:
                aimTime += Time.deltaTime / aimClipLength;
                if (aimTime >= data.aimFireEnd) aimPhase = AimPhase.Return;
                break;
            case AimPhase.Return:
                // เล่นย้อนกลับ: ยังเล็งอยู่ก็กลับไปค้างท่าเล็ง ปล่อยแล้วก็ย้อนจนถึงต้นคลิป
                float target = aiming ? data.aimHoldTime : 0f;
                aimTime -= drawStep;
                if (aimTime <= target)
                {
                    aimTime = target;
                    if (aiming) aimPhase = AimPhase.Hold;
                    else
                    {
                        FinishAim();
                        return;
                    }
                }
                break;
        }

        // คุมเวลาคลิปเอง จึงเล่นย้อนกลับได้
        current.animator.speed = 0f;
        current.animator.Play(data.aimState, 0, aimTime);

        TwistUpperBody();
    }

    private void FinishAim()
    {
        aimPhase = AimPhase.None;
        twist = 0f;
        holdFrames = 0;
        hasHoldBarrel = false;
        current.animator.speed = 1f;
        current.animator.CrossFadeInFixedTime(current.data.idleState, 0.15f, 0);
        if (weaponBeforeAim != null) SetWeapon(weaponBeforeAim.Value);
    }

    // บิดท่อนบนตามเมาส์ โดยเทียบกับทิศลำกล้องตอนค้างท่า แรงสะบัดจากท่ายิงจึงยังเห็นอยู่
    private void TwistUpperBody()
    {
        if (aimBone == null || upperPivot == null || aimRoots.Count == 0) return;

        // ท่าที่สั่งในเฟรมก่อนจะถูกวาดในเฟรมถัดไป จึงรอให้ถึงท่าค้างจริงก่อนจำทิศลำกล้อง
        if (!hasHoldBarrel && aimPhase == AimPhase.Hold && ++holdFrames >= 2)
        {
            holdBarrel = aimBone.rotation * current.data.barrelAxis;
            hasHoldBarrel = true;
            smoothedAim = aimPoint;
        }
        if (!hasHoldBarrel) return;

        bool twisting = aiming && aimPhase != AimPhase.Return;
        twist = Mathf.MoveTowards(twist, twisting || (aiming && aimPhase == AimPhase.Return) ? 1f : 0f, Time.unscaledDeltaTime / TwistBlendTime);
        if (twist <= 0f) return;

        smoothedAim = Vector3.Lerp(smoothedAim, aimPoint, 1f - Mathf.Exp(-TwistFollowSpeed * Time.unscaledDeltaTime));
        Vector3 wanted = (smoothedAim - aimBone.position).normalized;

        float yaw = Mathf.Clamp(Vector3.SignedAngle(Vector3.ProjectOnPlane(holdBarrel, Vector3.up), Vector3.ProjectOnPlane(wanted, Vector3.up), Vector3.up), -TwistMaxYaw, TwistMaxYaw);
        Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.up);
        Vector3 turned = yawRotation * holdBarrel;

        float elevation = Mathf.Asin(Mathf.Clamp(wanted.y, -1f, 1f)) - Mathf.Asin(Mathf.Clamp(turned.y, -1f, 1f));
        float pitch = Mathf.Clamp(elevation * Mathf.Rad2Deg, -TwistMaxPitch, TwistMaxPitch);
        Vector3 side = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(turned, Vector3.up).normalized);

        Quaternion delta = Quaternion.Slerp(Quaternion.identity, Quaternion.AngleAxis(-pitch, side) * yawRotation, twist);
        Vector3 pivot = upperPivot.position;
        foreach (Transform root in aimRoots)
        {
            root.position = pivot + delta * (root.position - pivot);
            root.rotation = delta * root.rotation;
        }
    }

    private void CollectAimBones()
    {
        BattleWeaponAnimation data = current.data;
        aimRoots.Clear();
        aimBone = upperPivot = null;
        holdFrames = 0;
        hasHoldBarrel = false;
        foreach (Transform bone in current.root.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == data.aimBone) aimBone = bone;
            if (data.aimUpperRoots.Contains(bone.name)) aimRoots.Add(bone);
            if (bone.name == data.aimPivotBone) upperPivot = bone;
        }
        foreach (AnimationClip clip in current.animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == data.aimState) aimClipLength = clip.length;
        }
    }

    // ตำแหน่งยืนตีหน้าศัตรู: พื้นระนาบเดียวกับจุดเริ่ม (ฉากเอียงตาม Characters จึงคิดใน local ของ parent)
    public Vector3 MeleeDestination(EnemyController target)
    {
        Transform parent = transform.parent;
        if (parent == null) return target.transform.position;

        Vector3 home = parent.InverseTransformPoint(homeWorld);
        Vector3 enemy = parent.InverseTransformPoint(target.transform.position);
        Vector3 toHome = home - enemy;
        toHome.y = 0f;
        toHome = toHome.sqrMagnitude > 0.001f ? toHome.normalized : Vector3.back;

        // บอสมีดาบยาว Bounds จึงกว้างเกินจริง ใช้รัศมีลำตัวแทน
        GuardianBossController boss = target.GetComponent<GuardianBossController>();
        float radius = boss != null ? boss.BodyRadius : 0f;
        if (radius <= 0f)
        {
            foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                radius = Mathf.Max(radius, Mathf.Max(r.bounds.extents.x, r.bounds.extents.z));
            }
        }
        float standoff = (set != null ? set.meleeStandoff : 1.3f) + radius / Mathf.Max(0.01f, parent.lossyScale.x);

        Vector3 stop = enemy + toHome * standoff;
        stop.y = home.y;
        return parent.TransformPoint(stop);
    }

    public IEnumerator DashTo(Vector3 destination)
    {
        yield return Move(transform.position, destination, set != null ? set.dashInDuration : 0.2f, true);
    }

    public IEnumerator DashBack()
    {
        yield return Move(transform.position, homeWorld, set != null ? set.dashBackDuration : 0.25f, false);
    }

    private IEnumerator Move(Vector3 from, Vector3 to, float duration, bool easeOutOnly)
    {
        // การเคลื่อนที่ใหม่แทนที่อันเก่า เช่นเทิร์นใหม่เริ่มขณะยังวิ่งกลับที่
        int token = ++moveToken;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (token != moveToken) yield break;
            float k = t / duration;
            transform.position = Vector3.Lerp(from, to, easeOutOnly ? 1f - (1f - k) * (1f - k) : Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        if (token == moveToken) transform.position = to;
    }

    // หันโมเดลหาเป้าหมาย (หมุนเฉพาะโมเดล เพราะ UI เมนูเป็นลูกของ Player_Holder)
    public void FaceTarget(Transform target)
    {
        if (current == null || target == null) return;
        Vector3 direction = target.position - current.root.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f) StartFacing(Quaternion.LookRotation(direction, current.root.transform.up));
    }

    public void ResetFacing()
    {
        if (current == null || set == null) return;
        StartFacing(transform.rotation * Quaternion.Euler(set.modelLocalEuler));
    }

    private void StartFacing(Quaternion rotation)
    {
        if (faceRoutine != null) StopCoroutine(faceRoutine);
        faceRoutine = StartCoroutine(TurnTo(current.root.transform, rotation));
    }

    private static IEnumerator TurnTo(Transform model, Quaternion rotation)
    {
        Quaternion from = model.rotation;
        for (float t = 0f; t < 0.15f; t += Time.deltaTime)
        {
            model.rotation = Quaternion.Slerp(from, rotation, t / 0.15f);
            yield return null;
        }
        model.rotation = rotation;
    }
}
