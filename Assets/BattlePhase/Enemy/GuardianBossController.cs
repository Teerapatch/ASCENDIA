using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// บอส Guardian: สร้างโมเดลจาก FBX แล้วคุมทั้งเทิร์น ท่าโจมตีตรงกับทิศ Parry (ฟาดลง = ผู้เล่นต้องลากขึ้น)
// Coroutine รันบน ParryManager เพราะบอสอาจตายระหว่างเทิร์นแล้วถูก Destroy แต่เทิร์นยังต้องจบ
[RequireComponent(typeof(EnemyController))]
public class GuardianBossController : MonoBehaviour
{
    private BossAnimationSet set;
    private EnemyController enemy;
    private BossSummonBehavior summon;
    private Animator animator;
    private Transform model;
    private bool usedDanceLastTurn;

    private void Awake()
    {
        set = Resources.Load<BossAnimationSet>("GuardianBossSet");
        enemy = GetComponent<EnemyController>();
        summon = GetComponent<BossSummonBehavior>();
    }

    // ทำใน Start เพราะ CombatManager.SpawnEnemy ตั้งสเกลหลัง Instantiate (หลัง Awake)
    private void Start()
    {
        SetupModel();
    }

    // รัศมีลำตัวในหน่วยโลก ไม่นับดาบ ใช้หาระยะยืนตีของผู้เล่น
    public float BodyRadius => model != null ? set.targetHeight * 0.18f : 0f;

    private void SetupModel()
    {
        if (model != null || set == null || set.model == null || set.controller == null) return;

        // โมเดลมีขนาดตาม targetHeight ไม่ขึ้นกับสเกลของ Prefab เดิม
        float rootScale = Mathf.Max(0.001f, transform.lossyScale.y);
        model = Instantiate(set.model, transform).transform;
        model.name = "GuardianModel";
        model.localScale = Vector3.one * (set.targetHeight / (set.modelHeight * rootScale));
        model.localPosition = Vector3.down * (set.groundBelowSpawn / rootScale);

        animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = set.controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

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

    // คืน false ถ้าไม่มีข้อมูลบอส EnemyController จะใช้ระบบโจมตีเดิม
    public bool TakeTurn()
    {
        SetupModel();
        if (animator == null || ParryManager.Instance == null) return false;
        ParryManager.Instance.StartCoroutine(TurnRoutine());
        return true;
    }

    private IEnumerator TurnRoutine()
    {
        int turn = CombatManager.Instance.TurnNumber;
        ParryManager.Instance.BeginCustomAttack(enemy);

        if (summon != null && summon.CanSummon)
        {
            usedDanceLastTurn = false;
            yield return RageRoutine();
        }
        else if (!usedDanceLastTurn && set.dance != null && set.dance.strikes.Count > 0 && Random.value < set.danceChance)
        {
            usedDanceLastTurn = true;
            yield return PlayMove(set.dance, set.danceDamage, set.dancePosture, turn);
        }
        else
        {
            usedDanceLastTurn = false;
            yield return ChainRoutine(turn);
        }

        if (this != null && animator != null) animator.CrossFadeInFixedTime(set.idleState, 0.2f, 0);
        yield return ParryManager.Instance.FinishCustomAttack(turn);
    }

    // ตีท่าแรก แล้วลุ้นตีต่อ: โอกาสเริ่ม 70% ลด 10% ทุกครั้ง ท่าไม่ซ้ำในชุดเดียว ตีได้สูงสุดเท่าจำนวนท่า
    private IEnumerator ChainRoutine(int turn)
    {
        List<BossMove> order = new List<BossMove>(set.basicAttacks);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (order[i], order[swap]) = (order[swap], order[i]);
        }

        float chance = set.chainChance;
        for (int i = 0; i < order.Count; i++)
        {
            yield return PlayMove(order[i], set.basicDamage, set.basicPosture, turn);
            if (!CanContinue(turn) || Random.value >= chance) yield break;
            chance = Mathf.Max(0f, chance - set.chainDecay);
        }
    }

    private bool CanContinue(int turn)
    {
        return this != null && enemy != null && !enemy.IsDead && !enemy.isStaggered && ParryManager.Instance.IsTurnValid(turn);
    }

    private IEnumerator PlayMove(BossMove move, float damage, float posture, int turn)
    {
        if (!CanContinue(turn)) yield break;

        animator.CrossFadeInFixedTime(move.state, 0.1f, 0, 0f);
        yield return null;

        foreach (BossStrike bossStrike in move.strikes)
        {
            if (!CanContinue(turn)) yield break;

            float delay = Mathf.Max(0f, bossStrike.time * move.length - StateSeconds(move.state) - set.parryLead);
            ParryStrike strike = new ParryStrike
            {
                delayBeforeStrike = delay,
                warningDuration = Mathf.Min(delay, set.warningDuration),
                parryWindowDuration = set.parryWindow,
                damageIfHit = damage,
                postureDamageIfParried = posture,
                sliderDirectionIndex = StrikeDirection(bossStrike)
            };
            yield return ParryManager.Instance.PlayStrike(strike, turn);
        }

        if (CanContinue(turn)) yield return WaitForState(move.state, move.length * 0.95f);
    }

    private IEnumerator RageRoutine()
    {
        animator.CrossFadeInFixedTime(set.rageState, 0.1f, 0, 0f);
        yield return null;

        yield return WaitForState(set.rageState, set.rageLength * set.rageSummonTime);
        if (this != null && summon != null) summon.TryExecuteSummon();
        yield return WaitForState(set.rageState, set.rageLength * 0.95f);
    }

    // รอจนคลิปถึงเวลาที่กำหนด ออกทันทีถ้าคลิปจบไปแล้วหรือบอสหายไป จะได้ไม่ค้างทั้งเทิร์น
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

    // ทิศที่ตั้งเองใน Boss Parry Editor มาก่อน ไม่ได้ตั้งจึงคำนวณจากอาวุธตามมุมกล้องตอนนั้น
    private int StrikeDirection(BossStrike strike)
    {
        if (strike.direction != ParryDirections.Auto) return strike.direction;

        Camera cam = Camera.main;
        if (cam == null || model == null) return ParryDirections.Up;
        return ParryDirections.FromWeaponSwing(model.rotation * strike.swing, cam.transform);
    }
}
