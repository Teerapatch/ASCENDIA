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

    // เวลาที่เหลือของท่าหลังจังหวะโดน สกิลหลายฮิตใช้กระจายดาเมจให้ทันท่า
    public float TimeAfterImpact { get; private set; }

    private bool HasState(string state)
    {
        return current != null && !string.IsNullOrEmpty(state) && current.animator.HasState(0, Animator.StringToHash(state));
    }

    // เล่นท่าจนจบ: ถ้ายังไม่ปล่อย (ready = false เช่นกำลังกด QTE) จะง้างค้างก่อนถึงจังหวะโดน
    public IEnumerator PlayAction(BattleClip clip, Func<bool> ready, Action onImpact)
    {
        if (clip == null || !HasState(clip.state))
        {
            while (ready != null && !ready()) yield return null;
            onImpact?.Invoke();
            yield break;
        }

        TimeAfterImpact = 0f;
        Animator animator = current.animator;
        animator.speed = Mathf.Max(0.01f, clip.speed);
        animator.CrossFadeInFixedTime(clip.state, set.crossFade, 0, 0f);
        yield return null;

        bool impacted = false;
        float holdPoint = clip.impact * 0.85f;
        int stateHash = Animator.StringToHash(clip.state);
        float released = -1f;

        while (true)
        {
            float time = StateTime(animator, stateHash);
            bool isReady = ready == null || ready();
            if (isReady && released < 0f) released = Time.time;

            if (!isReady && time >= holdPoint)
                animator.speed = time >= clip.impact * 0.98f ? 0f : set.qteHoldSpeed;
            else
                animator.speed = Mathf.Max(0.01f, clip.speed);

            if (!impacted && isReady && time >= clip.impact)
            {
                impacted = true;
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                TimeAfterImpact = Mathf.Max(0f, 1f - time) * info.length / Mathf.Max(0.01f, clip.speed);
                onImpact?.Invoke();
            }

            // จบท่าแล้ว หรือกันค้าง: State ถูกเปลี่ยนไปแล้ว/รอเกินเวลา
            bool finished = impacted && time >= 0.95f;
            bool stuck = released >= 0f && Time.time - released > 12f;
            if (finished || stuck) break;
            yield return null;
        }

        if (!impacted) onImpact?.Invoke();
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
    }

    public void PlayParry()
    {
        if (current == null || current.data.parries.Count == 0) return;
        PlayOneShot(current.data.parries[parryIndex++ % current.data.parries.Count]);
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

        float radius = 0f;
        foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            radius = Mathf.Max(radius, Mathf.Max(r.bounds.extents.x, r.bounds.extents.z));
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
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            transform.position = Vector3.Lerp(from, to, easeOutOnly ? 1f - (1f - k) * (1f - k) : Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        transform.position = to;
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
