using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// ค่าถูก serialize เป็นเลข (actionState ในฉาก) เพิ่มค่าใหม่ต่อท้ายเท่านั้น
public enum PlayerActionState { Waiting, ChoosingAction, ChoosingSkill, ChoosingTarget, DoingQTE, Executing, ChoosingWeapon }

public class PlayerCombatController : MonoBehaviour
{
    public static PlayerCombatController Instance;

    [Header("Stats")]
    public float baseSpeed = 100f;
    public int maxAP = 5;
    public int currentAP = 3;
    public TextMeshProUGUI textAP;

    [Header("Weapons")]
    [Tooltip("ใช้เมื่อเปิดฉากนี้ตรงๆ ถ้าถูกเรียกผ่าน BattleSession จะใช้อาวุธจาก Loadout ที่ส่งมาแทน")]
    public List<WeaponData> baseInventoryWeapons;
    [System.NonSerialized] public List<WeaponData> inventoryWeapons = new List<WeaponData>();
    public int currentWeaponIndex = 0;

    public WeaponData CurrentWeapon => inventoryWeapons[currentWeaponIndex];
    public WeaponData ActiveWeapon;

    [Header("Weapon Switch")]
    [Tooltip("AP ที่เสียทุกครั้งที่กดสลับอาวุธเอง (อาวุธพังแล้วถูกสลับอัตโนมัติไม่เสีย AP)")]
    public int switchWeaponAPCost = 1;

    [Header("Free Aim Settings")]
    public GameObject crosshairUI;
    public LayerMask enemyLayer;
    private bool isAiming = false;
    private Camera mainCam;

    [Header("Target & Skill Selection")]
    public PlayerActionState actionState = PlayerActionState.Waiting; // [ใช้ Enum คุม State]
    private int currentTargetIndex = 0;
    private int pendingActionType = 0; // 0 = ตีปกติ, 1 = สกิล

    [Header("Passive Systems")]
    public int currentComboStacks = 0;
    public int totalGunCounterAttacks = 0;

    [Header("Spell Elemental Sigil System")]
    public List<ElementType> elementSlots = new List<ElementType>();
    private bool hasHarvestedThisTurn = false;


    private WeaponSkill selectedSkill;
    private PlayerBattleAnimator battleAnimator;

    private class CapturedHit
    {
        public EnemyController target;
        public int damage;
        public int posture;
    }
    private List<CapturedHit> damageCapture;
    private List<CapturedHit> pendingHits;
    private float capturedShakeForce;

    [Header("Hit Impact")]
    [Tooltip("จังหวะสุดท้ายของท่ารับดาเมจกี่เท่าของจังหวะอื่น")]
    public float finalHitWeight = 1.5f;
    public float lightHitShake = 0.7f;
    public float heavyHitShake = 1.5f;
    public float lightHitstop = 0.035f;
    public float heavyHitstop = 0.09f;
    public float killHitstop = 0.14f;
    [Tooltip("องศา FOV ที่เลนส์กระตุกตอนโดน")]
    public float lightHitLensKick = 1.2f;
    public float heavyHitLensKick = 3.5f;

    // บัฟของผู้เล่น (Evasion, Berserk, AttackUp) ใช้ร่วมกันทุกอาวุธ นับเทิร์นตอนจบเทิร์นของผู้เล่น
    public readonly CombatStatusSet Statuses = new CombatStatusSet();

    private void Awake()
    {
        Instance = this;
        inventoryWeapons.Clear();
        foreach (var sourceWeapon in GetStartingWeapons())
        {
            // ใช้ตัวก๊อปปี้เสมอ จะได้ไม่ไปแก้ค่าในไฟล์ .asset จริง (Durability เต็มทุกครั้งที่เข้าฉาก)
            inventoryWeapons.Add(sourceWeapon.CreateBattleInstance(GetEquippedSkills(sourceWeapon)));
        }

        // เซ็ตอาวุธชิ้นแรกเป็นอาวุธเริ่มต้น
        if (inventoryWeapons.Count > 0)
        {
            ActiveWeapon = inventoryWeapons[0];
        }

        battleAnimator = GetComponent<PlayerBattleAnimator>();
        if (battleAnimator == null) battleAnimator = gameObject.AddComponent<PlayerBattleAnimator>();
    }

    // สกิลที่ผู้เล่นเลือกไว้ใน Camp (ส่งมากับ BattleRequest) ถ้าไม่มีใช้ 3 สกิลแรกของอาวุธ
    private List<WeaponSkill> GetEquippedSkills(WeaponData sourceWeapon)
    {
        BattleRequest request = BattleSession.Current;
        if (request != null && request.equippedSkills != null && request.equippedSkills.TryGetValue(sourceWeapon, out List<WeaponSkill> skills))
        {
            return skills;
        }
        return sourceWeapon.GetDefaultSkills();
    }

    private List<WeaponData> GetStartingWeapons()
    {
        BattleRequest request = BattleSession.Current;
        List<WeaponData> source = (request != null && request.weapons != null && request.weapons.Count > 0)
            ? request.weapons
            : baseInventoryWeapons;

        return source.FindAll(w => w != null);
    }

    private void Start()
    {
        mainCam = Camera.main;
        if (crosshairUI != null) crosshairUI.SetActive(false);
    }

    private void Update()
    {
        if (actionState == PlayerActionState.Waiting)
        {
            if (ControlPromptUI.Instance != null)
            {
                ControlPromptUI.Instance.HidePrompt();
            }
        }

        // ฉากที่มีตัวผู้เล่นแต่ไม่มี CombatManager (เช่น ฉากจัดแสง/ฉากอาร์ต ENVI_Battle) ไม่ต้องทำอะไร
        if (CombatManager.Instance == null) return;

        if (CombatManager.Instance.currentState != CombatState.PlayerTurn)
        {
            if (isAiming) ExitAimMode();
            return;
        }

        if (actionState == PlayerActionState.ChoosingSkill)
        {
            HandleSkillSelectionInput();
            if (ControlPromptUI.Instance != null)
            {
                ControlPromptUI.Instance.ShowPrompt("<color=#FFaaaa>[ESC]</color> Back");
            }
            return;
        }

        else if (actionState == PlayerActionState.ChoosingWeapon)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                UI_CancelWeaponSwitch();
            }
            else if (ControlPromptUI.Instance != null)
            {
                ControlPromptUI.Instance.ShowPrompt($"Switch: <color=#FFFFaa>{switchWeaponAPCost} AP</color>     <color=#FFaaaa>[ESC]</color> Back");
            }
            return;
        }

        else if (actionState == PlayerActionState.ChoosingTarget)
        {
            HandleTargetSelectionInput();
            if (ControlPromptUI.Instance != null)
            {
                ControlPromptUI.Instance.ShowPrompt("<color=#FFFFaa>[F]</color> Target Selection     <color=#FFaaaa>[ESC]</color> Back");
            }
            return;
        }

        else if (actionState == PlayerActionState.ChoosingAction)
        {
            if (ControlPromptUI.Instance != null)
            {
                ControlPromptUI.Instance.HidePrompt();
            }
        }

        // ระหว่างเล่นท่า/กด QTE ห้ามเล็งยิงแทรก
        if (actionState == PlayerActionState.Executing || actionState == PlayerActionState.DoingQTE)
        {
            if (isAiming) ExitAimMode();
            return;
        }

        // โหมด Free Aim (คลิกขวาค้าง)
        if (actionState != PlayerActionState.ChoosingSkill)
        {
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                if (!isAiming) EnterAimMode();
                AimingRoutine();
            }
            else
            {
                if (isAiming) ExitAimMode();
            }
        }
    }

    // ==========================================
    // 1. ระบบเลือกสกิล (เมื่อกดปุ่ม Skill บน UI)
    // ==========================================
    public void Command_Skill()
    {
        if (ActiveWeapon.availableSkills == null || ActiveWeapon.availableSkills.Count == 0)
        {
            Debug.LogWarning($"อาวุธ {ActiveWeapon.weaponName} ไม่มีสกิลให้ใช้!");
            return;
        }

        actionState = PlayerActionState.ChoosingSkill;

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.ShowSkillMenu(ActiveWeapon);
        }

        // --- [อัปเกรดกล้อง] เปลี่ยนเป็นโหมดเลือกสกิล (Over-the-shoulder) ---
        if (BattleCameraController.Instance != null)
        {
            BattleCameraController.Instance.SetState(CameraState.SkillSelecting, transform);
        }
    }

    public void UI_SelectSkill(int skillIndex)
    {
        if (actionState != PlayerActionState.ChoosingSkill) return;

        WeaponSkill skillToCast = ActiveWeapon.availableSkills[skillIndex];

        if (!skillToCast.CanExecute(this, out string reason))
        {
            Debug.Log($"<color=red>ใช้ {skillToCast.skillName} ไม่ได้: {reason}</color>");
            ShowPopup(reason, new Color(1f, 0.45f, 0.45f));
            return;
        }

        if (currentAP >= skillToCast.apCost)
        {
            selectedSkill = skillToCast;
            pendingActionType = 1; // 1 = สกิล
            actionState = PlayerActionState.ChoosingTarget;
            currentTargetIndex = 0;

            // ซ่อนเมนูสกิลระหว่างเล็งเป้า
            if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.HideAllMenus();

            UpdateCameraFocus();
        }
        else
        {
            Debug.Log($"<color=red>AP ไม่พอ! (ต้องการ {skillToCast.apCost} แต่มี {currentAP})</color>");
        }
    }

    private void HandleSkillSelectionInput()
    {
        if (Keyboard.current == null) return;

        // ดักจับการกดปุ่ม ESC เพื่อกลับไปหน้าเมนูหลัก
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            actionState = PlayerActionState.ChoosingAction;
            Debug.Log("ยกเลิกการเลือกสกิล -> กลับไปหน้าเมนูหลัก");

            // สั่งซ่อนหน้าสกิล แล้วเปิดหน้าต่าง Action Menu ขึ้นมา
            if (PlayerUIManager.Instance != null)
            {
                PlayerUIManager.Instance.ShowActionMenu();
            }

            // --- [อัปเกรดกล้อง] รีเซ็ตกลับ Overview ---
            if (BattleCameraController.Instance != null)
            {
                BattleCameraController.Instance.SetState(CameraState.Overview, transform);
            }
        }
    }

    // ==========================================
    // 2. ระบบเลือกเป้าหมาย (ใช้สำหรับโจมตีปกติและสกิล)
    // ==========================================
    public void Command_BasicAttack()
    {
        if (CombatManager.Instance.enemies.Count == 0) return;

        pendingActionType = 0; // 0 = โจมตีปกติ
        actionState = PlayerActionState.ChoosingTarget;
        currentTargetIndex = 0;

        if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.HideAllMenus();

        UpdateCameraFocus();
    }

    private void HandleTargetSelectionInput()
    {
        List<EnemyController> enemies = CombatManager.Instance.enemies;
        if (enemies.Count == 0 || Keyboard.current == null) return;

        if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            currentTargetIndex = (currentTargetIndex + 1) % enemies.Count;
            UpdateCameraFocus();
        }
        else if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            currentTargetIndex--;
            if (currentTargetIndex < 0) currentTargetIndex = enemies.Count - 1;
            UpdateCameraFocus();
        }
        else if (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
        {
            ExecutePendingAction(enemies[currentTargetIndex]);
        }
        else if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (pendingActionType == 1)
            {
                // ถ้ายกเลิกสกิล ให้เปิดหน้า List สกิลกลับมา
                actionState = PlayerActionState.ChoosingSkill;
                if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.ShowSkillMenu(ActiveWeapon);

                // --- [อัปเกรดกล้อง] กลับไปโฟกัสโหมดเลือกสกิล ---
                if (BattleCameraController.Instance != null)
                {
                    BattleCameraController.Instance.SetState(CameraState.SkillSelecting, transform);
                }
            }
            else
            {
                // ถ้ายกเลิกโจมตีปกติ ให้เปิดหน้าเมนูหลักกลับมา
                actionState = PlayerActionState.ChoosingAction;
                if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.ShowActionMenu();

                // --- [อัปเกรดกล้อง] รีเซ็ตกลับ Overview ---
                if (BattleCameraController.Instance != null)
                {
                    BattleCameraController.Instance.SetState(CameraState.Overview, transform);
                }
            }
        }
    }

    private void UpdateCameraFocus()
    {
        EnemyController target = CombatManager.Instance.enemies[currentTargetIndex];
        string actionName = (pendingActionType == 0) ? "โจมตีปกติ" : $"สกิล '{selectedSkill.skillName}'";

        Debug.Log($"<color=yellow>เล็งเป้า ({actionName}) ไปที่: {target.enemyName}</color>");

        if (BattleCameraController.Instance != null)
        {
            BattleCameraController.Instance.SetState(CameraState.TargetSelecting, transform, target.transform);
        }
    }

    // ==========================================
    // 3. ระบบยืนยันการโจมตี (OOP ทำงานที่นี่)
    // ==========================================
    private void ExecutePendingAction(EnemyController targetEnemy)
    {
        if (pendingActionType == 1 && selectedSkill == null) return;

        // กันกดซ้ำระหว่างเล่นท่า และซ่อนเมนูให้เห็นตัวละครเต็มที่
        actionState = PlayerActionState.Executing;
        if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.HideAllMenus();
        if (ControlPromptUI.Instance != null) ControlPromptUI.Instance.HidePrompt();

        StartCoroutine(pendingActionType == 0 ? BasicAttackRoutine(targetEnemy) : SkillRoutine(targetEnemy, selectedSkill));
    }

    private IEnumerator BasicAttackRoutine(EnemyController target)
    {
        BattleClip clip = battleAnimator != null ? battleAnimator.AttackClip : null;
        yield return ActionRoutine(target, clip, null, null, () => ApplyBasicAttack(target));
    }

    private IEnumerator SkillRoutine(EnemyController target, WeaponSkill skill)
    {
        BattleClip clip = battleAnimator != null ? battleAnimator.SkillClip(skill) : null;
        bool useQTE = skill.useQTE && SkillQTEManager.Instance != null;
        bool qteFinished = !useQTE;
        int qteSuccesses = 0;

        // QTE เริ่มหลังพุ่งเข้าประชิดแล้ว ตัวละครง้างท่าค้างรอจนกดครบ แล้วจึงฟาดลงจริง
        void BeginQTE()
        {
            if (!useQTE) return;
            actionState = PlayerActionState.DoingQTE;
            SkillQTEManager.Instance.StartQTE(skill.qteSteps, successes =>
            {
                qteSuccesses = successes;
                qteFinished = true;
            });
        }

        void Apply()
        {
            currentAP -= skill.apCost;
            UpdateAPUI();
            skill.ExecuteSkill(this, target, qteSuccesses);
        }

        yield return ActionRoutine(target, clip, BeginQTE, () => qteFinished, Apply);
    }

    // ลำดับของการลงมือทุกแบบ: กล้องเข้า -> พุ่งเข้าหา -> เล่นท่า (แบ่งดาเมจตามจังหวะโดน) -> กลับที่ -> ค้างให้เห็นผล -> จบเทิร์น
    private IEnumerator ActionRoutine(EnemyController target, BattleClip clip, System.Action onApproached, System.Func<bool> ready, System.Action apply)
    {
        int turn = CombatManager.Instance.TurnNumber;
        bool melee = battleAnimator != null && clip != null && clip.melee;
        Vector3 attackerAt = melee ? battleAnimator.MeleeDestination(target) : transform.position;

        if (BattleCameraController.Instance != null)
        {
            BattleCameraController.Instance.FrameAction(attackerAt, target.transform.position, melee);
        }

        if (battleAnimator != null)
        {
            battleAnimator.FaceTarget(target.transform);
            if (melee) yield return battleAnimator.DashTo(attackerAt);
        }

        onApproached?.Invoke();

        System.Action<int, int> onHit = (index, count) =>
        {
            // จังหวะแรกคำนวณผลของท่าทั้งหมด (บัฟ/AP/ดาเมจรวม) แล้วทยอยแบ่งดาเมจลงทีละจังหวะ
            if (index == 0) CaptureDamage(apply);
            ResolveHit(index, count);
        };

        if (battleAnimator != null) yield return battleAnimator.PlayAction(clip, ready, onHit);
        else
        {
            while (ready != null && !ready()) yield return null;
            onHit(0, 1);
        }

        if (battleAnimator != null)
        {
            if (melee) yield return battleAnimator.DashBack();
            battleAnimator.ResetFacing();
            yield return new WaitForSeconds(battleAnimator.RecoveryDelay);
        }

        // ระหว่างเล่นท่าอาจจบการต่อสู้ หรือขึ้น Wave ใหม่ไปแล้ว
        if (CombatManager.Instance.IsBattleOver || CombatManager.Instance.TurnNumber != turn) yield break;
        FinishTurnAction();
    }

    private void CaptureDamage(System.Action apply)
    {
        damageCapture = new List<CapturedHit>();
        CameraShakeManager.BeginCapture();
        try { apply?.Invoke(); }
        finally
        {
            capturedShakeForce = CameraShakeManager.EndCapture();
            pendingHits = damageCapture;
            damageCapture = null;
        }
    }

    // แบ่งดาเมจเป็นส่วนเท่า ๆ กัน จังหวะสุดท้ายหนักกว่า ผลรวมเท่าดาเมจเดิมพอดี
    private void ResolveHit(int index, int count)
    {
        if (pendingHits == null) return;

        float totalWeight = count - 1 + finalHitWeight;
        float from = index / totalWeight;
        float to = index == count - 1 ? 1f : (index + 1) / totalWeight;
        bool last = index == count - 1;
        bool landed = false, killed = false;
        Vector3 hitPosition = transform.position;

        foreach (CapturedHit hit in pendingHits)
        {
            if (hit.target == null || hit.target.IsDead) continue;
            int damage = Mathf.RoundToInt(hit.damage * to) - Mathf.RoundToInt(hit.damage * from);
            int posture = Mathf.RoundToInt(hit.posture * to) - Mathf.RoundToInt(hit.posture * from);
            if (damage <= 0 && posture <= 0) continue;

            if (!landed) hitPosition = hit.target.transform.position;
            landed = true;
            if (damage > 0) hit.target.TakeDamage(damage);
            if (posture > 0 && !hit.target.IsDead) hit.target.TakePostureDamage(posture);
            killed |= hit.target.IsDead;
        }

        if (landed) PlayHitImpact(last, killed, count, hitPosition);
        if (last) pendingHits = null;
    }

    // ความรู้สึกตอนโดน: กล้องกระแทกไปทางเดียวกับแรงตี หยุดภาพสั้น ๆ และเลนส์กระตุก จังหวะสุดท้ายแรงสุด
    private void PlayHitImpact(bool finalHit, bool killed, int hitCount, Vector3 targetPosition)
    {
        float weapon = ActiveWeapon == null ? 1f
            : ActiveWeapon.weaponType == WeaponType.LongSword ? 1.35f
            : ActiveWeapon.weaponType == WeaponType.Dagger ? 0.8f
            : ActiveWeapon.weaponType == WeaponType.Gun ? 0.75f : 1f;

        float strength = (finalHit ? Mathf.Max(heavyHitShake, capturedShakeForce) : lightHitShake) * weapon;
        if (killed) strength *= 1.4f;

        Camera cam = Camera.main;
        float side = cam != null ? Mathf.Sign(Vector3.Dot(targetPosition - transform.position, cam.transform.right)) : 1f;
        CameraShakeManager.Impact(strength, new Vector2(side * 0.8f, finalHit ? -0.7f : -0.2f));

        if (BattleCameraController.Instance != null) BattleCameraController.Instance.Punch(finalHit ? heavyHitLensKick : lightHitLensKick);

        // ท่ายิงรัวหลายนัดไม่หยุดภาพทุกนัด เดี๋ยวท่าจะกระตุก
        if (HitstopManager.Instance != null && (finalHit || hitCount <= 6))
        {
            HitstopManager.Instance.Freeze(killed ? killHitstop : finalHit ? heavyHitstop : lightHitstop);
        }
    }

    private void ApplyBasicAttack(EnemyController targetEnemy)
    {
        if (targetEnemy == null || targetEnemy.IsDead) return;

        int finalDamage = Mathf.RoundToInt(ActiveWeapon.baseDamage);
        int postureDamage = Mathf.RoundToInt(finalDamage * ActiveWeapon.postureDamageMultiplier);

        if (ActiveWeapon.weaponType == WeaponType.LongSword)
        {
            // 1. สร้างเกจ Posture มากกว่าปกติหลายเท่า
            postureDamage = Mathf.RoundToInt(postureDamage * ActiveWeapon.postureDamageMultiplier);

            // 2. ถ้าเป้าหมายกำลังติด Stagger อยู่ดาเมจคูณเพิ่มมหาศาล!
            if (targetEnemy.isStaggered)
            {
                finalDamage = Mathf.RoundToInt(finalDamage * ActiveWeapon.staggerDamageMultiplier);
                Debug.Log($"<color=orange>⚔️ HEAVY EXECUTION! LongSword deals {ActiveWeapon.staggerDamageMultiplier}x DMG to Staggered target! Final: {finalDamage}</color>");

                // สั่นกล้องแรงสะใจ
                CameraShakeManager.TryShake(1.6f);
            }
            else
            {
                // สั่นกล้องธรรมดาเวลาฟันโดน
                CameraShakeManager.TryShake(0.8f);
            }
        }
        else
        {
            // ถ้าเป็นอาวุธประเภทอื่น (เช่น Dagger)
            if (ActiveWeapon.weaponType == WeaponType.Dagger)
            {
                AddComboStack(1);
            }

            if (ActiveWeapon.weaponType == WeaponType.Spell)
            {
                AddElement(ElementTypes.RandomPlayable());
            }
        }

        DealDamage(targetEnemy, finalDamage, postureDamage);

        RestoreAP(1);
        UpdateAPUI();
    }

    // แยกการทำงานตอนจบเทิร์นออกมา เพื่อให้เรียกง่ายขึ้น
    private void FinishTurnAction()
    {
        UpdateAPUI();

        // --- [อัปเกรดกล้อง] รีเซ็ตกล้องกลับ Overview ---
        if (BattleCameraController.Instance != null)
        {
            BattleCameraController.Instance.SetState(CameraState.Overview);
        }

        if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.HideAllMenus();

        actionState = PlayerActionState.Waiting;
        CombatManager.Instance.EndCurrentTurn();
    }

    // ==========================================
    // Utility & Free Aim Methods
    // ==========================================
    public void StartTurn(WeaponData weaponData)
    {
        Debug.Log($"<color=cyan>Player's Turn!</color>");
        ActiveWeapon = weaponData;

        currentWeaponIndex = inventoryWeapons.IndexOf(weaponData);
        if (battleAnimator != null) battleAnimator.SetWeapon(ActiveWeapon.weaponType);

        actionState = PlayerActionState.ChoosingAction;
        UpdateAPUI();

        if (ActiveWeapon.weaponType == WeaponType.Spell)
        {
            hasHarvestedThisTurn = false;

            if (SpellOrbManager.Instance != null)
            {
                SpellOrbManager.Instance.SpawnSpellOrbs();
            }
        }

        // --- [อัปเกรดกล้อง] เมื่อเริ่มเทิร์น เซ็ตกล้องเป็นมุม Overview กว้างเห็นภาพรวม ---
        if (BattleCameraController.Instance != null)
        {
            BattleCameraController.Instance.SetState(CameraState.Overview, transform);
        }

        if (WeaponUIManager.Instance != null)
        {
            WeaponUIManager.Instance.UpdateWeaponUI(inventoryWeapons, ActiveWeapon);
        }

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.ShowActionMenu();
        }
    }

    // ==========================================
    // สลับอาวุธ: ปุ่ม Switch เปิดหน้าเลือกอาวุธ (WeaponMenuPanel) เลือกแล้วเสีย switchWeaponAPCost แล้วกลับเมนูหลักในเทิร์นเดิม
    // ==========================================
    public void Command_SwitchWeapon()
    {
        if (actionState != PlayerActionState.ChoosingAction) return;

        if (!HasOtherUsableWeapon())
        {
            ShowPopup("NO OTHER WEAPON", new Color(1f, 0.45f, 0.45f));
            return;
        }
        if (currentAP < switchWeaponAPCost)
        {
            ShowPopup("NOT ENOUGH AP", new Color(1f, 0.45f, 0.45f));
            return;
        }

        actionState = PlayerActionState.ChoosingWeapon;
        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.ShowWeaponMenu(inventoryWeapons, ActiveWeapon, switchWeaponAPCost);
        }
    }

    public void UI_SelectWeapon(int weaponIndex)
    {
        if (actionState != PlayerActionState.ChoosingWeapon) return;
        if (weaponIndex < 0 || weaponIndex >= inventoryWeapons.Count) return;

        WeaponData target = inventoryWeapons[weaponIndex];
        if (target == ActiveWeapon || target.currentDurability <= 0f) return;
        if (currentAP < switchWeaponAPCost)
        {
            ShowPopup("NOT ENOUGH AP", new Color(1f, 0.45f, 0.45f));
            return;
        }

        currentAP -= switchWeaponAPCost;
        ApplyActiveWeapon(weaponIndex, duringOwnTurn: true);
        ShowPopup($"SWITCH  -{switchWeaponAPCost} AP", new Color(0.6f, 0.9f, 1f));
        Debug.Log($"Switched to {ActiveWeapon.weaponName} (-{switchWeaponAPCost} AP)");

        ReturnToActionMenu();
    }

    public void UI_CancelWeaponSwitch()
    {
        if (actionState != PlayerActionState.ChoosingWeapon) return;
        ReturnToActionMenu();
    }

    // อาวุธที่ถืออยู่พัง: สลับไปชิ้นถัดไปที่ยังใช้ได้ทันที ไม่เสีย AP (CombatManager.RemoveWeapon เรียก)
    public void AutoSwitchWeapon()
    {
        for (int i = 1; i <= inventoryWeapons.Count; i++)
        {
            int next = (currentWeaponIndex + i) % inventoryWeapons.Count;
            if (next != currentWeaponIndex && inventoryWeapons[next].currentDurability > 0)
            {
                bool ownTurn = CombatManager.Instance != null && CombatManager.Instance.currentState == CombatState.PlayerTurn;
                ApplyActiveWeapon(next, ownTurn);
                Debug.Log($"Auto-switched to {ActiveWeapon.weaponName}");
                return;
            }
        }
        Debug.LogWarning("ไม่มีอาวุธอื่นที่ใช้งานได้แล้ว!");
    }

    private bool HasOtherUsableWeapon()
    {
        return inventoryWeapons.Exists(w => w != ActiveWeapon && w.currentDurability > 0);
    }

    private void ApplyActiveWeapon(int index, bool duringOwnTurn)
    {
        WeaponData previous = ActiveWeapon;
        currentWeaponIndex = index;
        ActiveWeapon = CurrentWeapon;
        if (battleAnimator != null) battleAnimator.SetWeapon(ActiveWeapon.weaponType);

        // Orb ธาตุมีเฉพาะตอนถือ Spell ในเทิร์นตัวเอง
        if (SpellOrbManager.Instance != null)
        {
            if (duringOwnTurn && ActiveWeapon.weaponType == WeaponType.Spell)
            {
                hasHarvestedThisTurn = false;
                SpellOrbManager.Instance.SpawnSpellOrbs();
            }
            else if (previous != null && previous.weaponType == WeaponType.Spell)
            {
                SpellOrbManager.Instance.ClearOrbs();
            }
        }

        UpdateAPUI();
        if (WeaponUIManager.Instance != null)
        {
            WeaponUIManager.Instance.UpdateWeaponUI(inventoryWeapons, ActiveWeapon);
        }
    }

    private void ReturnToActionMenu()
    {
        actionState = PlayerActionState.ChoosingAction;
        if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.ShowActionMenu();
        if (BattleCameraController.Instance != null)
        {
            BattleCameraController.Instance.SetState(CameraState.Overview, transform);
        }
    }

    void EnterAimMode()
    {
        isAiming = true;
        if (crosshairUI != null) crosshairUI.SetActive(true);
        if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.HideAllMenus();

        if (ActiveWeapon != null && ActiveWeapon.weaponType == WeaponType.Spell)
        {
            if (SpellOrbManager.Instance != null)
            {
                SpellOrbManager.Instance.ToggleOrbsVisibility(true);
            }
        }
    }

    void ExitAimMode()
    {
        isAiming = false;
        if (crosshairUI != null) crosshairUI.SetActive(false);

        // ยิงตัวสุดท้ายตายแล้ว ไม่ต้องเปิดเมนูกลับมา (ช่วงหน่วงก่อนโชว์หน้าสรุปผล)
        bool battleOver = CombatManager.Instance != null && CombatManager.Instance.IsBattleOver;
        if (!battleOver && PlayerUIManager.Instance != null) PlayerUIManager.Instance.ShowActionMenu();

        if (ActiveWeapon != null && ActiveWeapon.weaponType == WeaponType.Spell)
        {
            if (SpellOrbManager.Instance != null)
            {
                SpellOrbManager.Instance.ToggleOrbsVisibility(false);
            }
        }
    }

    void AimingRoutine()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        if (crosshairUI != null) crosshairUI.transform.position = mousePos;

        if (Mouse.current.leftButton.wasPressedThisFrame) ExecuteFreeAimShoot(mousePos);
    }

    void ExecuteFreeAimShoot(Vector2 screenPos)
    {
        if (currentAP < 1) return;

        Ray ray = mainCam.ScreenPointToRay(screenPos);

        if (ActiveWeapon != null && ActiveWeapon.weaponType == WeaponType.Spell)
        {
            if (hasHarvestedThisTurn)
            {
                Debug.Log("<color=red>Spell Free-Aim has already been used to harvest this turn!</color>");
                return;
            }

            if (Physics.Raycast(ray, out RaycastHit harvestHit, 1000f))
            {
                FloatingElementOrb orb = harvestHit.collider.GetComponent<FloatingElementOrb>();
                if (orb != null)
                {
                    // เก็บธาตุเข้า Slot
                    AddElement(orb.elementType);
                    orb.Harvest();

                    hasHarvestedThisTurn = true; // ใช้สิทธิ์หมดแล้วในเทิร์นนี้
                    ExitAimMode();
                }
                else
                {
                    Debug.Log("Free-Aim Spell: No elemental orb hit!");
                }
            }
            UpdateAPUI();
            return; // จบการทำงานของ Spell Free-Aim ไม่ให้ไปทำดาเมจใส่ศัตรู
        }

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, enemyLayer))
        {
            EnemyWeakPoint weakPoint = hit.collider.GetComponent<EnemyWeakPoint>();
            EnemyController enemy = null;
            bool hitWeakPoint = false;

            if (weakPoint != null && !weakPoint.isBroken)
            {
                // ถ้ายิงโดนจุดอ่อนที่ยังไม่แตก ให้ดึงข้อมูลศัตรูหลักมาจากสคริปต์จุดอ่อน
                enemy = weakPoint.mainEnemy;
                hitWeakPoint = true;
            }
            else
            {
                // ถ้าไม่โดนจุดอ่อน ให้หา EnemyController ตามปกติ
                enemy = hit.collider.GetComponentInParent<EnemyController>();
            }

            if (enemy != null)
            {
                float rawDamage = ActiveWeapon.baseDamage * ActiveWeapon.freeAimMultiplier;
                int finalDamage = Mathf.RoundToInt(rawDamage);

                if (enemy.isMarkedByGun)
                {
                    finalDamage = Mathf.RoundToInt(finalDamage * 1.5f);
                    Debug.Log($"<color=red>TARGET MARKED: Free-Aim damage amplified by 50%! Final DMG: {finalDamage}</color>");
                    CameraShakeManager.TryShake(1.0f);
                }
                else
                {
                    CameraShakeManager.TryShake(0.6f);
                }

                int postureDamage = Mathf.RoundToInt(finalDamage * 0.5f);

                if (hitWeakPoint)
                {
                    postureDamage = Mathf.RoundToInt(postureDamage * weakPoint.postureDamageMultiplier);
                    weakPoint.BreakWeakPoint();

                    Debug.Log($"<color=red>CRITICAL HIT! Posture Damage increased to {postureDamage}!</color>");

                    // สั่นกล้องแรงสะใจเมื่อยิงโดนจุดอ่อน
                    CameraShakeManager.TryShake(1.5f);
                }

                if (battleAnimator != null && battleAnimator.AttackClip != null) battleAnimator.PlayOneShot(battleAnimator.AttackClip.state);
                DealDamage(enemy, finalDamage, postureDamage);
                currentAP -= 1;
                ExitAimMode();
            }
        }
        UpdateAPUI();
    }

    public void RestoreAP(int amount) { currentAP = Mathf.Min(currentAP + amount, maxAP); }

    public void RestoreAPFromAction(int amount, EnemyController attacker = null)
    {
        int apOverflow = 0;

        if (currentAP + amount > maxAP)
        {
            apOverflow = (currentAP + amount) - maxAP;
            currentAP = maxAP;
        }
        else
        {
            currentAP += amount;
        }

        UpdateAPUI();

        bool hasGunInInventory = false;
        foreach (var weapon in inventoryWeapons)
        {
            if (weapon.weaponType == WeaponType.Gun && weapon.currentDurability > 0)
            {
                hasGunInInventory = true;
                break;
            }
        }

        if (apOverflow > 0 && attacker != null && hasGunInInventory)
        {
            ExecuteGunCounterAttack(apOverflow, attacker);
        }
    }

    public void TakeDamage(int damage)
    {
        if (ActiveWeapon == null) return;

        ActiveWeapon.currentDurability -= damage;
        Debug.Log($"{ActiveWeapon.weaponName} took {damage} damage. Remaining: {ActiveWeapon.currentDurability}");

        DamagePopupManager.Instance.CreatePopup(transform.position, damage, false, Color.red);

        if (WeaponUIManager.Instance != null)
        {
            WeaponUIManager.Instance.UpdateWeaponUI(inventoryWeapons, ActiveWeapon);
        }

        // สั่นกล้องตอนโดนโจมตี
        CameraShakeManager.TryShake(0.8f);

        if (ActiveWeapon.currentDurability <= 0)
        {
            CameraShakeManager.TryShake(1.2f);
            HandleWeaponBreak();
        }
    }

    void HandleWeaponBreak()
    {
        Debug.Log($"<color=red>{ActiveWeapon.weaponName} is BROKEN!</color>");

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.RemoveWeapon(ActiveWeapon);
        }
    }

    public void UpdateAPUI()
    {
        if (textAP == null) return;

        // บัฟที่ติดอยู่ แสดงใต้ตัวเลข AP (เช่น BSK2  EVA)
        string buffs = "";
        foreach (CombatStatus status in Statuses.All) buffs += status.ShortLabel + "  ";
        textAP.text = buffs.Length > 0
            ? $"{currentAP}/{maxAP}\n<size=45%><color=#FFD27A>{buffs.TrimEnd()}</color></size>"
            : $"{currentAP}/{maxAP}";
    }

    // ==========================================
    // Damage Pipeline: ดาเมจทุกอย่างจากผู้เล่น (ตีปกติ / Free-Aim / สกิล / สวนกลับ) ควรผ่านตรงนี้
    // เพื่อให้บัฟของผู้เล่น (Berserk / AttackUp) และดีบัฟของศัตรู (Expose Weakness) คิดครบทุกที่
    // ==========================================
    public int DealDamage(EnemyController target, float damage, float postureDamage = 0f, WeaponType? sourceType = null)
    {
        if (target == null || target.IsDead) return 0;

        WeaponType type = sourceType ?? (ActiveWeapon != null ? ActiveWeapon.weaponType : WeaponType.Dagger);
        int finalDamage = 0;

        if (damage > 0f)
        {
            finalDamage = Mathf.RoundToInt(damage * GetOutgoingDamageMultiplier() * target.GetIncomingDamageMultiplier(type));
        }

        // ระหว่างคำนวณท่า: เก็บดาเมจไว้แบ่งลงตามจังหวะโดน
        if (damageCapture != null)
        {
            damageCapture.Add(new CapturedHit { target = target, damage = finalDamage, posture = Mathf.RoundToInt(postureDamage) });
            return finalDamage;
        }

        if (damage > 0f) target.TakeDamage(finalDamage);

        if (postureDamage > 0f) target.TakePostureDamage(Mathf.RoundToInt(postureDamage));
        return finalDamage;
    }

    public float GetOutgoingDamageMultiplier()
    {
        return 1f + Statuses.GetValue(CombatStatusType.Berserk) + Statuses.GetValue(CombatStatusType.AttackUp);
    }

    // บัฟที่ใส่ให้ตัวเองระหว่างเทิร์นตัวเอง (ไม่โดนนับลดตอนจบเทิร์นนี้)
    public void ApplySelfStatus(CombatStatusType type, float value, int turns, string popupText)
    {
        Statuses.Apply(type, value, turns, appliedDuringOwnTurn: true);
        if (!string.IsNullOrEmpty(popupText)) ShowPopup(popupText, new Color(1f, 0.82f, 0.3f));
        UpdateAPUI();
    }

    public void ShowPopup(string text, Color color)
    {
        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.CreateTextPopup(transform.position + Vector3.up * 2f, text, color);
    }

    // ========================================== WeaponPassiveSystem ==========================================
    public void AddComboStack(int amount, bool isFromSkill = false)
    {
        if (!isFromSkill)
        {
            if (ActiveWeapon == null || ActiveWeapon.weaponType != WeaponType.Dagger)
            {
                return;
            }
        }

        currentComboStacks += amount;
        Debug.Log($"<color=cyan>COMBO +{amount}! (Total: {currentComboStacks})</color>");

        if (DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.CreatePopup(transform.position + Vector3.up * 1.5f, currentComboStacks, true, Color.cyan);
        }
    }

    public int ConsumeComboStacks()
    {
        int stacksToUse = currentComboStacks;
        currentComboStacks = 0;
        Debug.Log($"<color=magenta>Consumed {stacksToUse} COMBO Stacks!</color>");
        return stacksToUse;
    }

    private void ExecuteGunCounterAttack(int overflowAmount, EnemyController target)
    {
        totalGunCounterAttacks += 1;
        WeaponData gunData = inventoryWeapons.Find(w => w.weaponType == WeaponType.Gun);

        int counterDamage = Mathf.RoundToInt(gunData.baseDamage * overflowAmount);

        Debug.Log($"<color=red>AP Overflow ({overflowAmount})! Gun Passive Counter-Attacks for {counterDamage} DMG!</color>");

        DealDamage(target, counterDamage, counterDamage * 0.3f, WeaponType.Gun);

        CameraShakeManager.TryShake(0.5f);
    }

    public void AddElement(ElementType element)
    {
        if (ActiveWeapon == null || ActiveWeapon.weaponType != WeaponType.Spell) return;

        if (elementSlots.Count < ActiveWeapon.maxElementSlots)
        {
            elementSlots.Add(element);
            Debug.Log($"<color=magenta>🔮 Elemental Slot +1 [{element}] (Total: {elementSlots.Count}/{ActiveWeapon.maxElementSlots})</color>");
        }
        else
        {
            // ตัวเลือก: ถือว่าช่องเต็มแล้ว หรือดันตัวเก่าออก (FIFO)
            //elementSlots.RemoveAt(0);
            //elementSlots.Add(element);
            Debug.Log($"<color=yellow>🔮 Slots Full! Shifted and added [{element}]</color>");
        }

        // UI Update Spell
    }
}