using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// --- [แก้ไข] เพิ่ม State "ChoosingSkill" เข้ามา ---
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
    public List<WeaponData> inventoryWeapons;
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

    // --- [เพิ่มใหม่] เก็บสกิลที่ผู้เล่นเพิ่งเลือก ---
    private WeaponSkill selectedSkill;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        mainCam = Camera.main;
        if (crosshairUI != null) crosshairUI.SetActive(false);
    }

    private void Update()
    {
        if (CombatManager.Instance.currentState != CombatState.PlayerTurn)
        {
            if (isAiming) ExitAimMode();
            return;
        }

        if (actionState == PlayerActionState.ChoosingSkill)
        {
            HandleSkillSelectionInput();
            return;
        }

        else if (actionState == PlayerActionState.ChoosingTarget)
        {
            HandleTargetSelectionInput();
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
            // TODO: เล่นเสียง Error หรือสั่น UI
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
            }
            else
            {
                // ถ้ายกเลิกโจมตีปกติ ให้เปิดหน้าเมนูหลักกลับมา
                actionState = PlayerActionState.ChoosingAction;
                if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.ShowActionMenu();
            }

            if (BattleCameraController.Instance != null) BattleCameraController.Instance.ResetCamera();
        }
    }

    private void UpdateCameraFocus()
    {
        EnemyController target = CombatManager.Instance.enemies[currentTargetIndex];
        string actionName = (pendingActionType == 0) ? "โจมตีปกติ" : $"สกิล '{selectedSkill.skillName}'";

        Debug.Log($"<color=yellow>เล็งเป้า ({actionName}) ไปที่: {target.enemyName}</color>");

        if (BattleCameraController.Instance != null)
        {
            BattleCameraController.Instance.FocusOnTarget(target.transform);
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
            int damage = Mathf.RoundToInt(ActiveWeapon.baseDamage);
            targetEnemy.TakeDamage(damage);
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
        if (BattleCameraController.Instance != null) BattleCameraController.Instance.ResetCamera();

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
            if (BattleCameraController.Instance != null) BattleCameraController.Instance.ResetCamera();
        }
        else if (nextIndex == currentWeaponIndex)
        {
            Debug.LogWarning("ไม่มีอาวุธอื่นที่ใช้งานได้แล้ว!");
        }
    }

    void EnterAimMode() 
    { 
        isAiming = true; if (crosshairUI != null) crosshairUI.SetActive(true);
        if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.HideAllMenus();
    }
    void ExitAimMode() 
    { 
        isAiming = false; if (crosshairUI != null) crosshairUI.SetActive(false);
        if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.ShowActionMenu();
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
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, enemyLayer))
        {
            EnemyController enemy = hit.collider.GetComponent<EnemyController>();
            if (enemy != null)
            {
                int finalDamage = Mathf.RoundToInt(ActiveWeapon.baseDamage * ActiveWeapon.freeAimMultiplier);
                enemy.TakeDamage(finalDamage);
                enemy.TakePostureDamage(Mathf.RoundToInt(finalDamage * 0.5f));
                currentAP -= 1;
                ExitAimMode();
            }
        }
        UpdateAPUI();
    }

    public void RestoreAP(int amount) { currentAP = Mathf.Min(currentAP + amount, maxAP); }
    public void TakeDamage(int damage)
    {
        if (ActiveWeapon == null) return; // กัน Error

        ActiveWeapon.currentDurability -= damage;
        Debug.Log($"{ActiveWeapon.weaponName} took {damage} damage. Remaining: {ActiveWeapon.currentDurability}");

        DamagePopupManager.Instance.CreatePopup(transform.position, damage, false, Color.red);

        if (WeaponUIManager.Instance != null)
        {
            WeaponUIManager.Instance.UpdateWeaponUI(inventoryWeapons, ActiveWeapon);
        }

        if (CameraShakeManager.Instance != null)
            CameraShakeManager.Instance.Shake(0.8f);

        if (CurrentWeapon.currentDurability <= 0)
        {
            Debug.Log($"{CurrentWeapon.weaponName} is BROKEN!");

            if (CameraShakeManager.Instance != null)
                CameraShakeManager.Instance.Shake(1.2f);

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

        //if (CombatManager.Instance.currentState != CombatState.GameOver)
        //{
        //    Command_SwitchWeapon();
        //}
    }
    void UpdateAPUI() { if (textAP != null) textAP.text = $"{currentAP}/{maxAP}"; }
}