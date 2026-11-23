using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class ParryManager : MonoBehaviour
{
    public static ParryManager Instance;

    [Header("Settings")]
    public float swipeThreshold = 50f; //[cite: 6]

    [Header("Visuals & UI")]
    [Tooltip("Hint แบบเก่า (ใช้เมื่อไม่ได้ใส่ Hint VFX ด้านล่าง)")]
    public GameObject hintParticle; //[cite: 6]
    public bool showSwipeTrail = true;
    public LineRenderer swipeLine;

    [Header("Hint VFX (2 จังหวะ: เตือนก่อนฟัน -> บอกทิศที่ต้องลาก)")]
    [Tooltip("จังหวะ 1: โชว์ระหว่างศัตรูง้างอาวุธ (delayBeforeStrike)")]
    public GameObject hintStep1Prefab;
    [Tooltip("จังหวะ 2: โชว์ระหว่าง Parry Window และหมุนให้ด้าน +X ชี้ไปทางทิศที่ต้องลาก")]
    public GameObject hintStep2DirectionPrefab;
    [Tooltip("ระยะจากกล้องที่วาง Hint / ParryVFX ค่าเริ่มต้นวัดจากตำแหน่งที่ฝ่ายอาร์ตวางไว้ในฉาก ENVI_Battle")]
    public float hintDistanceFromCamera = 15.69f;
    [Tooltip("ตำแหน่งบนจอ (Viewport 0-1) ที่วาง Hint / ParryVFX ค่าเริ่มต้นวัดจากฉาก ENVI_Battle")]
    public Vector2 hintViewportPosition = new Vector2(0.576f, 0.534f);
    [Tooltip("ให้ Hint / ParryVFX โผล่กลางตัวศัตรูที่กำลังโจมตี (ปิด = ใช้ Hint Viewport Position จุดเดียวตลอด)")]
    public bool hintAtEnemy = true;
    [Tooltip("เวลาที่โชว์ Hint จังหวะ 1 ก่อนเปลี่ยนเป็นจังหวะ 2 (ช่วงก่อนหน้านั้นของ delayBeforeStrike จะไม่มี Hint) ถ้า delayBeforeStrike สั้นกว่านี้ จะรอเพิ่มให้ครบ")]
    public float hintStep1Duration = 0.35f;
    [Tooltip("คูณกับสเกลของ Prefab (Prefab ตั้งไว้ 2 อยู่แล้ว)")]
    public float hintScale = 1.6f;
    [Tooltip("ตอน Hint โผล่: ขยายเกินขนาดจริงเท่านี้แล้วหดกลับ ช่วยให้สังเกตเห็นตอนเปลี่ยนจังหวะ")]
    public float hintPopScale = 1.4f;
    public float hintPopDuration = 0.15f;
    [Tooltip("Hint เต้นเป็นจังหวะระหว่างโชว์ (สัดส่วนของขนาด, 0 = ปิด)")]
    public float hintPulseAmount = 0.08f;
    public float hintPulseSpeed = 12f;
    [Tooltip("มุมกระจายของประกายใน Hint (องศา) รอบทิศที่ต้องลาก")]
    public float hintSparkSpread = 25f;
    [Tooltip("บวกเพิ่ม Sorting Order ให้ Hint/ParryVFX วาดทับอนุภาคประกอบฉาก (เมฆ หมอก แสง)")]
    public int hintSortingBoost = 1000;
    [Tooltip("ขนาด ParryVFX ตอน Parry สำเร็จ (คูณกับสเกลของ Prefab)")]
    public float parrySuccessScale = 1.8f;
    [Tooltip("ปิด Hint แล้วรอให้อนุภาคจางหายกี่วินาทีก่อนลบทิ้ง")]
    public float hintFadeOutTime = 0.4f;
    [Range(0.05f, 1f), Tooltip("ความเร็วเวลาระหว่างที่ Hint ทิศทางขึ้น (1 = ไม่ Slow)")]
    public float parryHintTimeScale = 0.6f;

    private Vector2 requiredDirection;
    private int incomingDamage;
    private int currentPostureDamage;

    private Vector2 startPosScreen;
    private bool isSwiping;
    private Camera mainCam;

    private EnemyController attackingEnemy;
    private GameObject activeHint;
    private Vector3 activeHintBaseScale;
    private float activeHintShownAt;
    private float currentHintAngle;

    // ตัวแปรควบคุม State รายฮิต
    private bool isAwaitingInput = false;
    private bool isStrikeResolved = false;
    private bool isParrySlowActive = false;

    // ผลของจังหวะล่าสุด ให้ศัตรูที่คุมเทิร์นเอง (เช่นกระสุน) ตัดสินใจต่อ
    public bool LastStrikeParried { get; private set; }
    // นับทุกครั้งที่ PlayStrike เริ่ม ใช้ผูกเหตุการณ์ (เช่น SlashVFX ของศัตรู) กับการฟันครั้งที่ถูกต้อง
    public int StrikeSerial { get; private set; }
    public Vector2 LastSwipeDirection { get; private set; } = Vector2.right;

    private void Awake()
    {
        Instance = this;
        mainCam = Camera.main; //[cite: 6]
        if (swipeLine) swipeLine.positionCount = 0;
        if (hintParticle) hintParticle.SetActive(false); //[cite: 6]
    }

    private void OnDisable()
    {
        ClearHint();
        EndParrySlow();
    }

    private void BeginParrySlow()
    {
        isParrySlowActive = true;
        HitstopManager.SetBaseTimeScale(parryHintTimeScale);
    }

    // เรียกก่อน Hitstop ของการ Parry เสมอ ไม่ให้ Slow ค้างหลังจบจังหวะ
    private void EndParrySlow()
    {
        if (!isParrySlowActive) return;
        isParrySlowActive = false;
        HitstopManager.SetBaseTimeScale(1f);
    }

    private void Update()
    {
        if (activeHint == null) return;

        // โผล่แบบเด้ง (ใหญ่เกิน -> ขนาดจริง) แล้วเต้นเบา ๆ ตลอดที่โชว์ ใช้เวลาจริงเพื่อไม่ค้างตอน Hitstop
        float t = Time.unscaledTime - activeHintShownAt;
        float pop = hintPopDuration > 0f ? Mathf.Lerp(hintPopScale, 1f, Mathf.Clamp01(t / hintPopDuration)) : 1f;
        float pulse = 1f + Mathf.Sin(t * hintPulseSpeed) * hintPulseAmount;
        activeHint.transform.localScale = activeHintBaseScale * (pop * pulse);

        // HintVFX ชุดใหม่ระเบิดครั้งเดียว: ยิงซ้ำทุก vfxHintRepeat วินาที (เวลาจริง) ตลอดช่วงที่ Hint ยังโชว์ (ของอาร์ตวนทุก 1 วิ)
        if (vfxHintRepeat > 0f && Time.unscaledTime - activeHintFiredAt >= vfxHintRepeat)
        {
            activeHintFiredAt = Time.unscaledTime;
            CombatVFX.Replay(activeHint);
        }
    }

    [Tooltip("HintVFX (VFX Graph) ยิงซ้ำทุกกี่วินาทีตามเวลาจริง ระหว่างที่ Hint ยังค้างอยู่ (0 = ครั้งเดียว)")]
    public float vfxHintRepeat = 1f;
    private float activeHintFiredAt;

    // ==========================================
    // Hint VFX
    // ==========================================
    // ตำแหน่ง/การหมุนอิงกล้อง: แกน Z ของ VFX หันตามกล้อง แล้วหมุนรอบแกนนั้นตามทิศที่ต้องลาก (มุมบนจอ)
    // วางตรงตัวศัตรูบนจอ แต่คงระยะจากกล้องเดิม ขนาด Hint จึงเท่าเดิมและวาดทับฉากเหมือนเดิม
    public Vector3 HintPosition()
    {
        Vector2 viewport = hintViewportPosition;
        if (hintAtEnemy && attackingEnemy != null)
        {
            Vector3 onScreen = mainCam.WorldToViewportPoint(EnemyCenter(attackingEnemy));
            if (onScreen.z > 0f) viewport = onScreen;
        }
        return mainCam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, hintDistanceFromCamera));
    }

    // ใช้ Collider ลำตัวก่อน เพราะ Bounds ของ Mesh รวมดาบยาวของบอสเข้าไปด้วย จุดกลางจะเบี้ยว
    private static Vector3 EnemyCenter(EnemyController enemy)
    {
        Collider body = enemy.GetComponent<Collider>();
        return body != null && body.enabled ? body.bounds.center : ParryStreakUI.TargetCenter(enemy.transform);
    }

    public Quaternion HintRotation(float screenAngle)
    {
        return mainCam.transform.rotation * Quaternion.Euler(0f, 0f, screenAngle);
    }

    private void ShowHint(GameObject prefab, float screenAngle)
    {
        ClearHint();

        if (prefab != null)
        {
            activeHint = Instantiate(prefab, HintPosition(), HintRotation(screenAngle));
            activeHintBaseScale = prefab.transform.localScale * hintScale;
            activeHintShownAt = Time.unscaledTime;
            activeHintFiredAt = activeHintShownAt;
            activeHint.transform.localScale = activeHintBaseScale * hintPopScale;
            // ประกายของ Hint พุ่งไปทางเดียวกับทิศที่ต้องลาก (แทนการกระจายรอบตัว)
            Quaternion rot = activeHint.transform.rotation;
            VFXManager.AimEmission(activeHint, rot * Vector3.right, rot * Vector3.forward, hintSparkSpread);
            VFXManager.BoostSorting(activeHint, hintSortingBoost);
        }
        else if (hintParticle != null)
        {
            // ไม่มี VFX ใหม่ -> ใช้ Hint แบบเก่า (จังหวะเตือนไม่มีของเดิม จึงโชว์แค่ตอนบอกทิศ)
            hintParticle.SetActive(true);
            hintParticle.transform.rotation = HintRotation(screenAngle);
        }
    }

    private void ShowWarningHint()
    {
        ClearHint();
        // หันตามทิศของจังหวะ 2 ด้วย ผู้เล่นจะได้เห็นทิศตั้งแต่จังหวะเตือน
        if (hintStep1Prefab != null) ShowHint(hintStep1Prefab, currentHintAngle);
    }

    [Tooltip("ตัวคูณขนาด HintVFX ชุดใหม่ (VFX_Hint) เพิ่มจาก vfxHintScreenSize")]
    public float vfxHintScale = 1f;
    [Tooltip("ความยาวเส้นประกายของ VFX_Hint เป็นสัดส่วนของความสูงจอ (ในฉากทดสอบของอาร์ตราว 0.4)")]
    public float vfxHintScreenSize = 0.35f;
    // เส้นประกายของ Hint_01 ยาวราว 10 หน่วยที่สเกล 1 (กรวย 5 หน่วย x สเกล 2 ของ Muzzle_01 ใน Prefab)
    private const float VfxHintStreakLength = 10f;

    private void ShowDirectionHint(float screenAngle)
    {
        SoundManager.Play(Sfx.ParryHint);
        // HintVFX ชุดใหม่ (Resources/BattleVFX/VFX_Hint) บอกทิศที่ศัตรูจะตี แทน Hint จังหวะ 2 เดิม
        GameObject vfxHint = CombatVFX.HintPrefab;
        if (vfxHint != null)
        {
            ShowHint(vfxHint, screenAngle);
            // ขนาดตามจอ ไม่ใช่ตามตัวศัตรู: เส้นประกายของอาร์ตยาวหลายเท่าตัวทดสอบ ถ้าขยายตามตัวศัตรูจะยาวเกินจอ
            if (activeHint != null && mainCam != null)
            {
                float viewHeight = mainCam.orthographic ? mainCam.orthographicSize * 2f
                    : 2f * hintDistanceFromCamera * Mathf.Tan(mainCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float scale = vfxHintScreenSize * viewHeight / VfxHintStreakLength * vfxHintScale;
                activeHintBaseScale = vfxHint.transform.localScale * scale;
                activeHint.transform.localScale = activeHintBaseScale * hintPopScale;
            }
        }
        else if (hintStep2DirectionPrefab != null) ShowHint(hintStep2DirectionPrefab, screenAngle);
        else if (hintParticle != null) ShowHint(null, screenAngle);
    }

    private void ClearHint()
    {
        if (hintParticle) hintParticle.SetActive(false);
        if (activeHint == null) return;

        // VFX วนลูป: หยุดปล่อยอนุภาคแล้วค่อยลบ จะได้จางหายแทนการหายวับ (ทั้ง Particle System และ VFX Graph)
        CombatVFX.StopEmitting(activeHint);
        foreach (ParryHintArrows arrows in activeHint.GetComponentsInChildren<ParryHintArrows>()) arrows.FadeOut(hintFadeOutTime);
        foreach (ParticleSystem ps in activeHint.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        Destroy(activeHint, hintFadeOutTime);
        activeHint = null;
    }

    public void StartParrySequence(EnemyAttackPattern pattern, EnemyController attacker)
    {
        attackingEnemy = attacker;
        FrameAttacker(attacker);
        StartCoroutine(PlaySequenceRoutine(pattern));
    }

    private IEnumerator PlaySequenceRoutine(EnemyAttackPattern pattern)
    {
        int turnNumber = CombatManager.Instance.TurnNumber;

        // ลูปตามจำนวนการฟัน (Strikes) ในชุดคอมโบ
        for (int i = 0; i < pattern.strikes.Count; i++)
        {
            yield return PlayStrike(pattern.strikes[i], turnNumber);
            if (!IsSequenceStillValid(turnNumber)) break;
        }

        ClearHint();
        if (swipeLine) swipeLine.positionCount = 0;
        isAwaitingInput = false;

        // ถ้าเทิร์นถัดไปเป็นของอาวุธอื่น โมเดลจะถูกสลับทันที จึงต้องรอท่า Parry เล่นจบก่อน
        if (PlayerBattleAnimator.Instance != null && CombatManager.Instance.TurnNumber == turnNumber && !CombatManager.Instance.IsBattleOver)
        {
            yield return PlayerBattleAnimator.Instance.WaitForOneShot();
        }

        // จบชุดคอมโบ ส่งเทิร์นคืนให้ Timeline (เฉพาะถ้ายังเป็นเทิร์นเดิม ไม่ใช่เทิร์นของ Wave ใหม่)
        if (CombatManager.Instance.TurnNumber == turnNumber)
        {
            CombatManager.Instance.EndCurrentTurn();
        }
    }

    // ฟัน 1 ครั้ง: เรียกจากชุดคอมโบปกติ หรือจากบอสที่กำหนดจังหวะตามอนิเมชันเอง
    public IEnumerator PlayStrike(ParryStrike strike, int turnNumber)
    {
        requiredDirection = GetDirectionFromIndex(strike.sliderDirectionIndex);
        LastStrikeParried = false;
        StrikeSerial++;
        currentHintAngle = Mathf.Atan2(requiredDirection.y, requiredDirection.x) * Mathf.Rad2Deg;

        // 1. ดีเลย์ก่อนฟัน (ซิงก์จังหวะง้างดาบ) ช่วงท้ายโชว์ Hint จังหวะ 1 สั้น ๆ ให้ผู้เล่นเตรียมตัวก่อนบอกทิศ
        float warning = strike.warningDuration >= 0f ? strike.warningDuration : hintStep1Duration;
        float quietTime = strike.delayBeforeStrike - warning;
        if (quietTime > 0f) yield return new WaitForSeconds(quietTime);
        if (!IsSequenceStillValid(turnNumber)) yield break;

        ShowWarningHint();
        if (warning > 0f) yield return new WaitForSeconds(warning);

        // ผู้โจมตีตายจากการสวนกลับ หรือการต่อสู้จบไปแล้ว -> หยุดคอมโบที่เหลือ
        if (!IsSequenceStillValid(turnNumber)) yield break;

        // Evasion (Vanish Blade): หลบฮิตนี้อัตโนมัติ นับเป็น Parry สำเร็จ
        PlayerCombatController evader = PlayerCombatController.Instance;
        if (evader != null && evader.Statuses.ConsumeCharge(CombatStatusType.Evasion))
        {
            Debug.Log("<color=cyan>EVASION! หลบการโจมตีอัตโนมัติ (นับเป็น Parry)</color>");
            evader.ShowPopup("EVADE", Color.cyan);
            evader.UpdateAPUI();

            currentPostureDamage = Mathf.RoundToInt(strike.postureDamageIfParried);
            ClearHint();
            OnParrySuccess(attackingEnemy.transform.position, evader.transform.position);
            yield break;
        }

        // 2. ตั้งค่าการป้องกันสำหรับฮิตนี้
        incomingDamage = Mathf.RoundToInt(strike.damageIfHit);
        currentPostureDamage = Mathf.RoundToInt(strike.postureDamageIfParried);

        isAwaitingInput = true;
        isStrikeResolved = false;
        isSwiping = false;

        // Hint จังหวะ 2: บอกทิศที่ต้องลาก ตลอดช่วง Parry Window
        ShowDirectionHint(currentHintAngle);
        BeginParrySlow();

        // 3. เริ่มจับเวลา Parry Window
        float timer = strike.parryWindowDuration;
        while (timer > 0 && !isStrikeResolved)
        {
            timer -= Time.deltaTime;
            HandleSwipeInput(); // ย้ายการเช็คเมาส์มาไว้ใน Loop 
            yield return null;
        }

        // 4. หมดเวลาแล้วยังไม่สำเร็จ (ไม่ได้ลาก หรือลากไม่สุด)
        if (!isStrikeResolved)
        {
            FailStrike("Too slow!");
        }

        // ปิด UI ของฮิตนี้ เตรียมรอฮิตต่อไป
        ClearHint();
        if (swipeLine) swipeLine.positionCount = 0;
    }

    public void BeginCustomAttack(EnemyController attacker)
    {
        attackingEnemy = attacker;
        FrameAttacker(attacker);
    }

    // เทิร์นศัตรู: กล้องถ่ายผ่านไหล่ผู้เล่นไปหาตัวที่กำลังโจมตี
    private static void FrameAttacker(EnemyController attacker)
    {
        if (attacker != null && BattleCameraController.Instance != null) BattleCameraController.Instance.FrameEnemyTurn(attacker.transform);
        // ผู้เล่นหันหน้าเข้าหาศัตรูตัวที่กำลังจะโจมตี เตรียม Parry
        if (attacker != null && PlayerBattleAnimator.Instance != null) PlayerBattleAnimator.Instance.FaceTarget(attacker.transform, 0.35f);
        ParryImpactFX.ResetStreak(); // นับ PARRY xN ใหม่ทุกการโจมตีของศัตรู
    }

    public bool IsTurnValid(int turnNumber)
    {
        return IsSequenceStillValid(turnNumber);
    }

    // จบเทิร์นของศัตรูที่คุมเทิร์นเอง
    public IEnumerator FinishCustomAttack(int turnNumber)
    {
        ClearHint();
        if (swipeLine) swipeLine.positionCount = 0;
        isAwaitingInput = false;

        if (PlayerBattleAnimator.Instance != null && CombatManager.Instance.TurnNumber == turnNumber && !CombatManager.Instance.IsBattleOver)
        {
            yield return PlayerBattleAnimator.Instance.WaitForOneShot();
        }

        if (CombatManager.Instance.TurnNumber == turnNumber)
        {
            CombatManager.Instance.EndCurrentTurn();
        }
    }

    private bool IsSequenceStillValid(int turnNumber)
    {
        CombatManager combat = CombatManager.Instance;
        return combat != null
            && !combat.IsBattleOver
            && combat.TurnNumber == turnNumber
            && attackingEnemy != null
            && !attackingEnemy.IsDead;
    }

    void HandleSwipeInput()
    {
        if (Mouse.current == null || !isAwaitingInput || GameExitMenu.IsOpen) return; //[cite: 6]

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = GetWorldPosition(mouseScreenPos);

        // กดคลิกเริ่มลาก
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            startPosScreen = mouseScreenPos;
            isSwiping = true;

            if (showSwipeTrail && swipeLine != null)
            {
                swipeLine.positionCount = 2;
                swipeLine.SetPosition(0, mouseWorldPos);
                swipeLine.SetPosition(1, mouseWorldPos);
            }
        }

        // กำลังลาก
        if (isSwiping && showSwipeTrail && swipeLine != null)
        {
            swipeLine.SetPosition(1, mouseWorldPos);
        }

        // ปล่อยเมาส์
        if (Mouse.current.leftButton.wasReleasedThisFrame && isSwiping)
        {
            isSwiping = false;
            if (swipeLine) swipeLine.positionCount = 0;

            Vector2 swipeDelta = mouseScreenPos - startPosScreen;
            if (swipeDelta.magnitude >= swipeThreshold)
            {
                float dot = Vector2.Dot(swipeDelta.normalized, requiredDirection);
                if (dot > 0.8f)
                {
                    Vector3 startWorldPos = GetWorldPosition(startPosScreen);
                    Vector3 endWorldPos = GetWorldPosition(mouseScreenPos);

                    OnParrySuccess(startWorldPos, endWorldPos, swipeDelta);
                }
                else FailStrike("Wrong direction!");
            }
            else
            {
                FailStrike("Swipe too short!");
            }
        }
    }

    Vector2 GetDirectionFromIndex(int index)
    {
        return ParryDirections.ToVector(index);
    }

    Vector3 GetWorldPosition(Vector2 screenPos)
    {
        Vector3 pos = new Vector3(screenPos.x, screenPos.y, 10f);
        return mainCam.ScreenToWorldPoint(pos);

        //Ray ray = mainCam.ScreenPointToRay(screenPos);

        //Vector3 planeCenter = (attackingEnemy != null) ? attackingEnemy.transform.position : transform.position;
        //Plane combatPlane = new Plane(-mainCam.transform.forward, planeCenter);

        //if (combatPlane.Raycast(ray, out float enter))
        //{
        //    return ray.GetPoint(enter);
        //}

        //return mainCam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 5f));
    }

    // swipeScreenDirection: ทิศที่ผู้เล่นลากจริงบนจอ (พิกเซล) ประกายของ ParryVFX พุ่งตามทิศนี้
    // ไม่ส่งมา (เช่น Evasion หลบอัตโนมัติ ไม่มีการลาก) -> ใช้ทิศที่ Hint บอก
    public void OnParrySuccess(Vector3 startPos, Vector3 endPos, Vector2 swipeScreenDirection = default)
    {
        isStrikeResolved = true;
        isAwaitingInput = false;
        ClearHint();
        EndParrySlow();
        LastStrikeParried = true;
        SoundManager.Play(Sfx.ParrySuccess);
        LastSwipeDirection = swipeScreenDirection.sqrMagnitude > 0.0001f ? swipeScreenDirection.normalized : requiredDirection;

        if (PlayerBattleAnimator.Instance != null) PlayerBattleAnimator.Instance.PlayParry();
        // ParryVFX เล่นพร้อมท่า Parry (เฟรมเดียวกัน) ด้านหน้าตัวผู้เล่น ทุกครั้งที่ Parry สำเร็จ
        PlayParryVFX(swipeScreenDirection.sqrMagnitude > 0.0001f ? swipeScreenDirection : requiredDirection);

        bool isSpellActive = (PlayerCombatController.Instance != null &&
                              PlayerCombatController.Instance.ActiveWeapon != null &&
                              PlayerCombatController.Instance.ActiveWeapon.weaponType == WeaponType.Spell);

        Debug.Log($"<color=cyan>PARRY SUCCESS! Countering with {currentPostureDamage} Posture Damage!</color>");
        // Parry สำเร็จ: กล้องกระแทกตามทิศที่ลาก พร้อมเลนส์กระตุก
        Vector2 impactDirection = swipeScreenDirection.sqrMagnitude > 0.0001f ? swipeScreenDirection.normalized : Vector2.right;
        CameraShakeManager.Impact(2.2f, impactDirection);

        if (isSpellActive)
        {
            int smallPosture = Mathf.RoundToInt(currentPostureDamage * 0.4f);
            attackingEnemy.TakePostureDamage(smallPosture, LastSwipeDirection);

            // แปะดีบัฟเพิ่มความเสียหายที่ศัตรูได้รับ (สมมติมีฟังก์ชัน ApplyVulnerabilityDebuff บน EnemyController)
            // attackingEnemy.ApplyVulnerabilityDebuff(1.25f, 2); // รับดาเมจเพิ่ม 25% เป็นเวลา 2 เทิร์น

            // สุ่มธาตุแล้วส่งเข้าไปเก็บใน Slot ของผู้เล่น
            PlayerCombatController.Instance.AddElement(ElementTypes.RandomPlayable());

            Debug.Log("<color=cyan>Applied Vulnerability Debuff, Gained Elemental Slot</color>");
        }
        else
        {
            attackingEnemy.TakePostureDamage(currentPostureDamage, LastSwipeDirection);
            if (PlayerCombatController.Instance != null)
            {
                PlayerCombatController.Instance.AddComboStack(1);
                PlayerCombatController.Instance.RestoreAPFromAction(1, attackingEnemy);
            }
        }

        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.SpawnParrySlash(startPos, endPos);
            // ParryVFX ตรงจุดเดียวกับ Hint หมุนตามทิศที่ผู้เล่นลากจริง (ลากเฉียงจากทิศ Hint ได้ไม่เกิน ~36° ก็ยังผ่าน)
            float sparkAngle = swipeScreenDirection.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(swipeScreenDirection.y, swipeScreenDirection.x) * Mathf.Rad2Deg
                : currentHintAngle;
            // ParryVFX ชุดใหม่ (VFX_test ของอาร์ต) เกิดที่ตัวผู้เล่น ถ้าไม่มี Prefab ใช้ชุดเดิมตรงจุด Hint
            if (!CombatVFX.HasParry)
                VFXManager.Instance.SpawnParrySuccess(HintPosition(), HintRotation(sparkAngle), parrySuccessScale, hintSortingBoost);
        }

        // แถบแสงขาวพาดหลังตัวศัตรูตามทิศที่ลาก (Evasion ไม่มีการลาก -> ใช้ทิศของ Hint)
        if (ParryStreakUI.Instance != null && attackingEnemy != null)
        {
            Vector2 streakDir = swipeScreenDirection.sqrMagnitude > 0.0001f ? swipeScreenDirection : requiredDirection;
            ParryStreakUI.Instance.Play(attackingEnemy.transform, streakDir);
        }

        // หยุดเวลา/สโลว์, แฟลช, วงแหวน, เส้นความเร็ว, PARRY!, Post FX และกล้องพุ่ง+เอียง (ParryImpactFX)
        ParryImpactFX.Play(HintPosition(), LastSwipeDirection);
    }

    [Header("ParryVFX (Resources/BattleVFX/VFX_Parry)")]
    [Tooltip("ระยะด้านหน้าตัวผู้เล่น (ตามทิศที่โมเดลหัน ซึ่งหันหาศัตรูที่ตี) ที่ ParryVFX เกิด")]
    public float parryVFXFront = 2f;
    [Tooltip("ยกขึ้น/ลงจากกลางลำตัว")]
    public float parryVFXHeight = 0f;
    [Tooltip("ขนาดเทียบกับความสูงตัวผู้เล่น (vfxBodyHeight)")]
    public float parryVFXScale = 1f;
    [Tooltip("หน่วงก่อนเกิด (วินาทีจริง) ไว้จูนให้ตรงจังหวะปะทะในท่า Parry 0 = พร้อมกันทันที")]
    public float parryVFXDelay = 0f;
    [Tooltip("ประกายไฟชุดเดิม (Cartoon FX, VFXManager.parrySuccessVFXPrefab) พุ่งตามทิศที่ลาก ณ จุดเดียวกับ ParryVFX")]
    public bool parryClassicSparks = true;

    private void PlayParryVFX(Vector2 swipe)
    {
        if (parryVFXDelay > 0f) StartCoroutine(PlayParryVFXLater(swipe, parryVFXDelay));
        else SpawnParryVFX(swipe);
    }

    private IEnumerator PlayParryVFXLater(Vector2 swipe, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        SpawnParryVFX(swipe);
    }

    private void SpawnParryVFX(Vector2 swipe)
    {
        PlayerCombatController player = PlayerCombatController.Instance;
        if (player == null) return;
        Vector3 position = player.BodyCenter + player.BodyForward * parryVFXFront + Vector3.up * parryVFXHeight;
        CombatVFX.Parry(position, player.BodyForward, swipe, player.vfxBodyHeight * parryVFXScale);

        // ประกายไฟที่เห็นชัด: ชุดเดิมที่เคยเล่นตรงจุด Hint ย้ายมาอยู่จุดปะทะหน้าตัวผู้เล่น
        // ขนาดเดิมจูนไว้ที่ระยะ hintDistanceFromCamera จึงคูณตามระยะจริงจากกล้องให้ดูเท่าเดิมบนจอ
        if (parryClassicSparks && CombatVFX.HasParry && VFXManager.Instance != null && mainCam != null)
        {
            float angle = Mathf.Atan2(swipe.y, swipe.x) * Mathf.Rad2Deg;
            float depthScale = Vector3.Distance(mainCam.transform.position, position) / Mathf.Max(0.1f, hintDistanceFromCamera);
            VFXManager.Instance.SpawnParrySuccess(position, HintRotation(angle), parrySuccessScale * depthScale * parryVFXScale);
        }
    }

    void FailStrike(string reason)
    {
        isStrikeResolved = true;
        isAwaitingInput = false;
        ClearHint();
        EndParrySlow();

        Debug.Log($"<color=red>PARRY FAILED ({reason}) - Took {incomingDamage} Damage!</color>");
        ParryImpactFX.ResetStreak();
        CameraShakeManager.TryShake(0.8f);

        // รับดาเมจเข้าตัว/อาวุธผู้เล่น ท่าเจ็บไปทางเดียวกับอาวุธศัตรู (สวนกับทิศที่ต้องลาก)
        LastStrikeParried = false;
        CombatManager.Instance.player.TakeDamage(incomingDamage, -requiredDirection);
    }
}