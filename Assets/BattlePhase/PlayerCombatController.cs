using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerActionState { Waiting, ChoosingAction, ChoosingSkill, ChoosingTarget, DoingQTE, Executing }

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

    private void Awake()
    {
        Instance = this;
        inventoryWeapons.Clear();
        foreach (var sourceWeapon in GetStartingWeapons())
        {
            // ใช้ตัวก๊อปปี้เสมอ จะได้ไม่ไปแก้ค่าในไฟล์ .asset จริง (Durability เต็มทุกครั้งที่เข้าฉาก)
            inventoryWeapons.Add(sourceWeapon.CreateBattleInstance());
        }

        // เซ็ตอาวุธชิ้นแรกเป็นอาวุธเริ่มต้น
        if (inventoryWeapons.Count > 0)
        {
            ActiveWeapon = inventoryWeapons[0];
        }
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
        if (pendingActionType == 0)
        {
            // ตีปกติ ไม่ต้องมี QTE ทำงานได้เลย
            actionState = PlayerActionState.Executing;
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

            targetEnemy.TakeDamage(finalDamage);
            targetEnemy.TakePostureDamage(postureDamage);

            RestoreAP(1);
            FinishTurnAction();
        }
        else if (pendingActionType == 1 && selectedSkill != null)
        {
            // สกิล -> เช็คว่าสกิลนี้ต้องใช้ QTE ไหม
            if (selectedSkill.useQTE && SkillQTEManager.Instance != null)
            {
                actionState = PlayerActionState.DoingQTE;
                Debug.Log("รอผู้เล่นกด QTE...");

                // ส่งคำสั่งเปิด QTE และรอรับผลลัพธ์ผ่าน Callback
                SkillQTEManager.Instance.StartQTE(selectedSkill.qteSteps, (successCount) =>
                {
                    // [เมื่อ QTE จบลง ฟังก์ชันข้างในนี้ถึงจะทำงาน]
                    currentAP -= selectedSkill.apCost;
                    selectedSkill.ExecuteSkill(this, targetEnemy, successCount);

                    FinishTurnAction(); // สั่งจบเทิร์น
                });
            }
            else
            {
                // ถ้าสกิลนี้ไม่ต้องใช้ QTE (หรือไม่มี Manager) ก็ปล่อยสกิลเลยโดยส่ง QTE = 0
                actionState = PlayerActionState.Executing;
                currentAP -= selectedSkill.apCost;
                selectedSkill.ExecuteSkill(this, targetEnemy, 0);
                FinishTurnAction();
            }
        }
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

    public void Command_SwitchWeapon()
    {
        int nextIndex = currentWeaponIndex;
        bool foundValidWeapon = false;

        // วนลูปหาอาวุธถัดไปที่ยังไม่พัง (ป้องกันบั๊กกรณีพังเกือบหมด)
        for (int i = 0; i < inventoryWeapons.Count; i++)
        {
            nextIndex = (nextIndex + 1) % inventoryWeapons.Count;
            if (inventoryWeapons[nextIndex].currentDurability > 0)
            {
                foundValidWeapon = true;
                break;
            }
        }

        if (foundValidWeapon && nextIndex != currentWeaponIndex)
        {
            currentWeaponIndex = nextIndex;

            ActiveWeapon = CurrentWeapon;

            Debug.Log($"Switched to {CurrentWeapon.weaponName}");
            UpdateAPUI();

            if (WeaponUIManager.Instance != null)
            {
                WeaponUIManager.Instance.UpdateWeaponUI(inventoryWeapons, ActiveWeapon);
            }

            actionState = PlayerActionState.Waiting;

            // --- [อัปเกรดกล้อง] รีเซ็ตกล้อง ---
            if (BattleCameraController.Instance != null)
            {
                BattleCameraController.Instance.SetState(CameraState.Overview, transform);
            }
        }
        else if (nextIndex == currentWeaponIndex)
        {
            Debug.LogWarning("ไม่มีอาวุธอื่นที่ใช้งานได้แล้ว!");
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

                enemy.TakeDamage(finalDamage);
                enemy.TakePostureDamage(postureDamage);
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

    void UpdateAPUI() { if (textAP != null) textAP.text = $"{currentAP}/{maxAP}"; }

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

        target.TakeDamage(counterDamage);
        target.TakePostureDamage(Mathf.RoundToInt(counterDamage * 0.3f));

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