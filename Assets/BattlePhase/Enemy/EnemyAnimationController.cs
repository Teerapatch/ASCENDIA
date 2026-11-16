using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ศัตรูที่มีโมเดลและอนิเมชันจาก EnemyAnimationSet: คุมทั้งเทิร์น (ท่าปกติ/สกิล/คำราม) และท่าเจ็บตามทิศที่โดน
// Coroutine ของเทิร์นรันบน ParryManager เพราะศัตรูอาจตายระหว่างเทิร์นแล้วถูก Destroy แต่เทิร์นยังต้องจบ
[RequireComponent(typeof(EnemyController))]
public class EnemyAnimationController : MonoBehaviour
{
    [Tooltip("ตารางอนิเมชันของศัตรูชนิดนี้ (Assets/BattlePhase/Enemy/Anim)")]
    public EnemyAnimationSet set;

    private EnemyController enemy;
    private BossSummonBehavior summon;
    private Animator animator;
    private Transform model;
    private Transform hand;
    private HitReaction reaction;
    private bool usedSkillLastTurn;
    private bool isActing;
    private int hurtHash;
    // ท่า Idle ของแต่ละตัว: เริ่มคนละจังหวะ เร็วไม่เท่ากัน จึงไม่ขยับพร้อมกันทั้งแถว
    private float idleSpeed = 1f;
    private float idleLength = 1f;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        summon = GetComponent<BossSummonBehavior>();
    }

    // ทำใน Start เพราะ CombatManager.SpawnEnemy ตั้งสเกลหลัง Instantiate (หลัง Awake)
    private void Start()
    {
        SetupModel();
    }

    // รัศมีลำตัวในหน่วยโลก ไม่นับอาวุธ ใช้หาระยะยืนตีของผู้เล่น
    public float BodyRadius => model != null && set != null ? set.targetHeight * 0.18f : 0f;

    private void SetupModel()
    {
        if (model != null || set == null) return;

        // โมเดลฝังอยู่ใน Prefab ชื่อ Model (Tools/ASCENDIA/Setup Enemy Prefabs) ถ้าไม่มีค่อยสร้างจากตาราง
        Transform found = transform.Find("Model");
        if (found == null)
        {
            if (set.model == null) return;
            found = Instantiate(set.model, transform).transform;
            found.name = "Model";
        }
        model = found;

        // ขนาดตาม targetHeight ในโลก ไม่ขึ้นกับสเกลของ Prefab หรือจุดเกิด
        float rootScale = Mathf.Max(0.001f, transform.lossyScale.y);
        if (set.modelHeight > 0.01f) model.localScale = Vector3.one * (set.targetHeight / (set.modelHeight * rootScale));
        model.localPosition = Vector3.down * (set.groundBelowSpawn / rootScale);

        // Pivot สำหรับสะดุ้ง คั่นระหว่างรากกับโมเดล
        Transform pivot = new GameObject("HurtPivot").transform;
        pivot.SetParent(transform, false);
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        model.SetParent(pivot, true);
        reaction = pivot.gameObject.AddComponent<HitReaction>();

        animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.gameObject.AddComponent<Animator>();
        if (set.controller != null) animator.runtimeAnimatorController = set.controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        hurtHash = Animator.StringToHash(string.IsNullOrEmpty(set.hurtState) ? "Hurt" : set.hurtState);

        idleSpeed = 1f + Random.Range(-set.idleSpeedVariation, set.idleSpeedVariation);
        if (animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == set.idleState) idleLength = clip.length;
            }
        }
        if (HasState(set.idleState)) animator.Play(set.idleState, 0, Random.value);
        animator.speed = idleSpeed;

        Transform chest = null, head = null;
        foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == set.handBone) hand = bone;
            else if (bone.name == "chest") chest = bone;
            else if (bone.name == "head") head = bone;
        }

        // หายใจและโยกตัวเบา ๆ ซ้อนบนคลิป ตัวจึงไม่นิ่งแข็ง (กระดูก chest คุมท่อนบนทั้งหมดของริก Guardian)
        if (chest != null && model.GetComponent<ProceduralBodyMotion>() == null)
        {
            ProceduralBodyMotion motion = model.gameObject.AddComponent<ProceduralBodyMotion>();
            motion.axes = model;
            motion.spine = chest;
            motion.head = head;
            motion.breathAngle = Random.Range(1.3f, 1.9f);
            motion.breathsPerSecond = Random.Range(.22f, .34f);
            motion.swayAngle = Random.Range(.9f, 1.5f);
            motion.noiseAngle = set.idleNoise;
            motion.headNoiseAngle = set.idleHeadLook;
            motion.noiseSpeed = Random.Range(.25f, .45f);
            motion.leanPerSpeed = 0f;
            motion.turnLean = 0f;
            motion.headTurn = 0f;
        }

        CapsuleCollider body = GetComponent<CapsuleCollider>();
        if (body != null)
        {
            body.height = set.targetHeight / rootScale;
            body.radius = set.targetHeight * 0.18f / rootScale;
            body.center = Vector3.up * ((set.targetHeight * 0.5f - set.groundBelowSpawn) / rootScale);
        }

        // แถบเลือดอยู่เหนือหัว
        Transform bars = transform.Find("Canvas_WorldSpace");
        if (bars != null) bars.localPosition = Vector3.up * ((set.targetHeight + 0.8f - set.groundBelowSpawn) / rootScale);

        if (PlayerCombatController.Instance == null) return;
        Vector3 toPlayer = PlayerCombatController.Instance.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f) model.rotation = Quaternion.LookRotation(toPlayer, Vector3.up);
    }

    // ท่าเจ็บ: สะดุ้งตามทิศบนจอที่โดน (ศูนย์ = สุ่มทิศ) ระหว่างเทิร์นตัวเองเล่นแค่สะดุ้ง ไม่ตัดท่าโจมตี
    public void PlayHurt(Vector2 screenDirection, float strength = 1f)
    {
        if (set == null || model == null) return;
        if (screenDirection.sqrMagnitude < 0.001f) screenDirection = ParryDirections.ToVector(Random.Range(0, 8));

        Camera cam = Camera.main;
        Vector3 world = cam != null
            ? cam.transform.right * screenDirection.x + cam.transform.up * screenDirection.y
            : new Vector3(screenDirection.x, screenDirection.y, 0f);
        if (reaction != null) reaction.Hit(world, set.hurtKnockback * strength, set.hurtLean * strength);

        if (isActing || animator == null || string.IsNullOrEmpty(set.hurtState) || !animator.HasState(0, hurtHash)) return;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.shortNameHash == hurtHash && info.normalizedTime < 0.3f) return;
        animator.CrossFadeInFixedTime(set.hurtState, 0.05f, 0, 0f);
    }

    // คืน false ถ้าไม่มีข้อมูล EnemyController จะใช้ระบบโจมตีเดิม (attackPatterns)
    public bool TakeTurn()
    {
        SetupModel();
        if (animator == null || set == null || ParryManager.Instance == null) return false;
        ParryManager.Instance.StartCoroutine(TurnRoutine());
        return true;
    }

    private IEnumerator TurnRoutine()
    {
        int turn = CombatManager.Instance.TurnNumber;
        ParryManager.Instance.BeginCustomAttack(enemy);
        isActing = true;
        // ท่าโจมตีต้องเล่นความเร็วจริง จังหวะ Parry คำนวณจากเวลาของคลิป
        if (animator != null) animator.speed = 1f;

        if (summon != null && summon.CanSummon && HasState(set.rageState))
        {
            usedSkillLastTurn = false;
            yield return RageRoutine();
        }
        else if (set.HasSkill && HasState(set.skill.state) && !usedSkillLastTurn && Random.value < set.skillChance)
        {
            usedSkillLastTurn = true;
            if (DamagePopupManager.Instance != null && enemy != null)
            {
                DamagePopupManager.Instance.CreateTextPopup(transform.position + Vector3.up * 2f, set.skillName, new Color(1f, 0.55f, 0.3f));
            }
            yield return PlayMove(set.skill, set.skillDamage, set.skillPosture, turn);
        }
        else
        {
            usedSkillLastTurn = false;
            yield return BasicRoutine(turn);
        }

        isActing = false;
        if (this != null && animator != null)
        {
            animator.speed = idleSpeed;
            // กลับเข้า Idle ที่จังหวะสุ่ม ไม่เริ่มต้นคลิปพร้อมตัวอื่น
            if (HasState(set.idleState)) animator.CrossFadeInFixedTime(set.idleState, 0.2f, 0, Random.value * idleLength);
        }
        yield return ParryManager.Instance.FinishCustomAttack(turn);
    }

    // ท่าปกติ: สุ่มท่าไม่ซ้ำ RandomCount = 1-2 ที, Chain = ตีต่อตามโอกาสที่ลดลงเรื่อย ๆ
    private IEnumerator BasicRoutine(int turn)
    {
        if (set.basicAttacks.Count == 0) yield break;

        List<EnemyMove> order = new List<EnemyMove>(set.basicAttacks);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (order[i], order[swap]) = (order[swap], order[i]);
        }

        if (set.basicMode == EnemyBasicAttackMode.Chain)
        {
            float chance = set.chainChance;
            for (int i = 0; i < order.Count; i++)
            {
                yield return PlayMove(order[i], set.basicDamage, set.basicPosture, turn);
                if (!CanContinue(turn) || Random.value >= chance) yield break;
                chance = Mathf.Max(0f, chance - set.chainDecay);
            }
            yield break;
        }

        int count = Random.Range(Mathf.Max(1, set.basicMinMoves), Mathf.Max(set.basicMinMoves, set.basicMaxMoves) + 1);
        for (int i = 0; i < count; i++)
        {
            yield return PlayMove(order[i % order.Count], set.basicDamage, set.basicPosture, turn);
            if (!CanContinue(turn)) yield break;
        }
    }

    private bool CanContinue(int turn)
    {
        return this != null && enemy != null && !enemy.IsDead && !enemy.isStaggered && ParryManager.Instance.IsTurnValid(turn);
    }

    private IEnumerator PlayMove(EnemyMove move, float damage, float posture, int turn)
    {
        if (!CanContinue(turn) || !HasState(move.state)) yield break;

        animator.CrossFadeInFixedTime(move.state, 0.1f, 0, 0f);
        yield return null;

        foreach (EnemyStrike strike in move.strikes)
        {
            if (!CanContinue(turn)) yield break;

            ParryStrike parry = new ParryStrike
            {
                parryWindowDuration = set.parryWindow,
                damageIfHit = damage,
                postureDamageIfParried = posture,
                sliderDirectionIndex = set.randomDirection ? Random.Range(0, 8) : StrikeDirection(strike)
            };

            if (set.delivery == EnemyAttackDelivery.Projectile)
            {
                // ปล่อยกระสุนตรงจังหวะของคลิป ช่วง Parry เปิดตอนกระสุนใกล้ถึงตัว
                yield return WaitForState(move.state, strike.time * move.length);
                if (!CanContinue(turn)) yield break;

                EnemyProjectile projectile = LaunchProjectile();
                parry.delayBeforeStrike = Mathf.Max(0f, set.projectileFlightTime - set.parryLead);
                parry.warningDuration = Mathf.Min(parry.delayBeforeStrike, set.warningDuration);
                yield return ParryManager.Instance.PlayStrike(parry, turn);

                if (projectile != null)
                {
                    if (ParryManager.Instance.LastStrikeParried) projectile.Deflect(ScreenToWorld(ParryManager.Instance.LastSwipeDirection) + Vector3.up * 0.3f);
                    else projectile.Impact();
                }
            }
            else
            {
                parry.delayBeforeStrike = Mathf.Max(0f, strike.time * move.length - StateSeconds(move.state) - set.parryLead);
                parry.warningDuration = Mathf.Min(parry.delayBeforeStrike, set.warningDuration);
                yield return ParryManager.Instance.PlayStrike(parry, turn);
            }
        }

        if (CanContinue(turn)) yield return WaitForState(move.state, move.length * 0.95f);
    }

    private EnemyProjectile LaunchProjectile()
    {
        if (PlayerCombatController.Instance == null) return null;
        Vector3 from = hand != null ? hand.position : model.position + Vector3.up * (set.targetHeight * 0.6f);
        System.Func<Vector3> target = () => PlayerBattleAnimator.Instance != null
            ? PlayerBattleAnimator.Instance.BodyCenter
            : PlayerCombatController.Instance.transform.position + Vector3.up * 2f;
        return EnemyProjectile.Launch(set.projectilePrefab, from, target, set.projectileFlightTime, set.projectileArc, set.projectileScale, set.projectileImpactPrefab);
    }

    private IEnumerator RageRoutine()
    {
        animator.CrossFadeInFixedTime(set.rageState, 0.1f, 0, 0f);
        yield return null;

        yield return WaitForState(set.rageState, set.rageLength * set.rageSummonTime);
        if (this != null && summon != null) summon.TryExecuteSummon();
        yield return WaitForState(set.rageState, set.rageLength * 0.95f);
    }

    // รอจนคลิปถึงเวลาที่กำหนด ออกทันทีถ้าคลิปจบไปแล้วหรือศัตรูหายไป จะได้ไม่ค้างทั้งเทิร์น
    private IEnumerator WaitForState(string state, float seconds)
    {
        float limit = Time.time + seconds + 1f;
        while (this != null && animator != null && Time.time < limit)
        {
            AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(state) || info.normalizedTime * info.length >= seconds) yield break;
            yield return null;
        }
    }

    private float StateSeconds(string state)
    {
        if (animator == null) return float.MaxValue;
        AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        return info.IsName(state) ? info.normalizedTime * info.length : 0f;
    }

    private bool HasState(string state)
    {
        return animator != null && !string.IsNullOrEmpty(state) && animator.HasState(0, Animator.StringToHash(state));
    }

    private static Vector3 ScreenToWorld(Vector2 screen)
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.right * screen.x + cam.transform.up * screen.y : new Vector3(screen.x, screen.y, 0f);
    }

    // ทิศที่ตั้งเองใน Enemy Parry Editor มาก่อน ไม่ได้ตั้งจึงคำนวณจากอาวุธตามมุมกล้องตอนนั้น
    private int StrikeDirection(EnemyStrike strike)
    {
        if (strike.direction != ParryDirections.Auto) return strike.direction;

        Camera cam = Camera.main;
        if (cam == null || model == null) return ParryDirections.Up;
        return ParryDirections.FromWeaponSwing(model.rotation * strike.swing, cam.transform);
    }
}
