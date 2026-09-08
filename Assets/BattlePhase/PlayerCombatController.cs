using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatController : MonoBehaviour
{
    [Header("Stats")]
    public float baseSpeed = 100f;
    public int maxAP = 5;
    public int currentAP = 3; // เริ่มต้นอาจจะมีไม่เต็ม
    public TextMeshProUGUI textAP;

    [Header("Weapons")]
    public List<WeaponData> inventoryWeapons; // ลิสต์อาวุธที่พกมา
    public int currentWeaponIndex = 0;

    public WeaponData CurrentWeapon => inventoryWeapons[currentWeaponIndex];
    public WeaponData ActiveWeapon;

    [Header("Free Aim Settings")]
    public GameObject crosshairUI; // รูปเป้าเล็ง
    public LayerMask enemyLayer;   // แยก Layer ให้ยิงโดนเฉพาะศัตรู
    private bool isAiming = false;
    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
        if (crosshairUI != null) crosshairUI.SetActive(false); // ซ่อนเป้าเล็งตอนเริ่มเกม
    }

    // เพิ่มฟังก์ชัน Update เพื่อดักจับการคลิกเมาส์
    private void Update()
    {
        // ระบบเล็งจะทำงานได้ ก็ต่อเมื่อเป็นเทิร์นของผู้เล่นเท่านั้น
        if (CombatManager.Instance.currentState != CombatState.PlayerTurn)
        {
            if (isAiming) ExitAimMode();
            return;
        }

        // กดคลิกขวาค้าง = เข้าโหมดเล็ง
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

    void EnterAimMode()
    {
        isAiming = true;
        if (crosshairUI != null) crosshairUI.SetActive(true);
        // 💡 คุณสามารถสั่งให้กล้องซูมเข้า (FOV ลดลง) หรือทำ Slow Motion เพิ่มตรงนี้ได้
    }

    void ExitAimMode()
    {
        isAiming = false;
        if (crosshairUI != null) crosshairUI.SetActive(false);
    }

    void AimingRoutine()
    {
        // 1. บังคับเป้าเล็ง UI ให้ขยับตามเมาส์
        Vector2 mousePos = Mouse.current.position.ReadValue();
        if (crosshairUI != null) crosshairUI.transform.position = mousePos;

        // 2. ถ้าคลิกซ้ายขณะที่เล็งอยู่ = ทำการยิง
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            ExecuteFreeAimShoot(mousePos);
        }
    }

    void ExecuteFreeAimShoot(Vector2 screenPos)
    {
        // เช็คว่ามี AP พอหรือไม่ (สมมติว่าใช้ 1 AP)
        if (currentAP < 1)
        {
            Debug.Log("AP ไม่พอสำหรับการยิง!");
            return;
        }

        // 3. ยิงเส้น Raycast จากกล้องเข้าไปใน 3D Space
        Ray ray = mainCam.ScreenPointToRay(screenPos);
        RaycastHit hit;

        // เช็คว่าชนกับวัตถุที่อยู่ใน Enemy Layer หรือไม่ (ระยะยิง 100 หน่วย)
        if (Physics.Raycast(ray, out hit, 1000f, enemyLayer))
        {
            EnemyController enemy = hit.collider.GetComponent<EnemyController>();
            if (enemy != null)
            {
                int finalDamage = Mathf.RoundToInt(ActiveWeapon.baseDamage * ActiveWeapon.freeAimMultiplier);

                Debug.Log($"<color=green>🎯 ยิงโดนเป้าหมาย: {enemy.enemyName}</color>");
                enemy.TakeDamage(finalDamage);
                enemy.TakePostureDamage(Mathf.RoundToInt(finalDamage * 0.5f));

                currentAP -= 1;

                ExitAimMode();
                //CombatManager.Instance.EndCurrentTurn();
            }
        }
        else
        {
            Debug.Log("<color=red>❌ ยิงพลาด!</color>");
            // (ถ้าอยากให้ยิงพลาดแล้วเสียเทิร์นเลย ก็สั่งหัก AP และ EndCurrentTurn ตรงนี้ได้ครับ)
        }

        UpdateAPUI();
    }

    public void StartTurn(WeaponData weaponData)
    {
        Debug.Log($"<color=cyan>Player's Turn!</color>");
        ActiveWeapon = weaponData;
        // ฟื้นฟู AP ทุกเทิร์นอัตโนมัติ
        //RestoreAP(1);
        Debug.Log("Player Turn! Waiting for input...");
        // ตรงนี้คุณจะไปเปิด UI Menu (Attack, Skill, Switch) ให้ผู้เล่นกด

        UpdateAPUI();
    }

    // --- Action Methods (เรียกจากปุ่ม UI) ---

    public void Command_BasicAttack()
    {
        int damage = ActiveWeapon.baseDamage;
        if (CombatManager.Instance.enemies.Count == 0) return;
        EnemyController targetEnemy = CombatManager.Instance.enemies[0];

        Debug.Log($"Player attacks with {CurrentWeapon.weaponName} for {damage} damage");
        targetEnemy.TakeDamage(damage);
        RestoreAP(1); // โจมตีธรรมดาได้ AP


        // เล่น Animation โจมตีศัตรู และลด HP ศัตรู
        UpdateAPUI();
        CombatManager.Instance.EndCurrentTurn();
    }

    public void Command_Skill()
    {
        if (currentAP >= 2) // สมมติสกิลใช้ 2 AP
        {
            currentAP -= 2;
            Debug.Log($"Player uses skill with {CurrentWeapon.weaponName}");
            // ทำดาเมจสกิล

            UpdateAPUI();
            CombatManager.Instance.EndCurrentTurn();
        }
        else
        {
            Debug.Log("AP ไม่พอ!");
        }
    }

    public void Command_SwitchWeapon()
    {
        // หาอาวุธถัดไปที่ยังไม่พัง
        int nextIndex = (currentWeaponIndex + 1) % inventoryWeapons.Count;

        if (inventoryWeapons[nextIndex].currentDurability > 0)
        {
            currentWeaponIndex = nextIndex;
            Debug.Log($"Switched to {CurrentWeapon.weaponName}");
            // อาจจะเสีย AP 1 หน่วย หรือ ฟรี ก็ได้ แล้วแต่บาลานซ์
            // สลับอาวุธเสร็จ ไม่จบเทิร์น (หรือจบเทิร์นก็ได้)
            UpdateAPUI();
        }
        else
        {
            Debug.Log("อาวุธชิ้นนั้นพังไปแล้ว!");
        }
    }

    // --- Utility Methods ---

    public void RestoreAP(int amount)
    {
        currentAP = Mathf.Min(currentAP + amount, maxAP);
    }

    public void TakeDamage(int damage)
    {
        CurrentWeapon.currentDurability -= damage;
        Debug.Log($"{CurrentWeapon.weaponName} took {damage} damage. Remaining: {CurrentWeapon.currentDurability}");

        if (CurrentWeapon.currentDurability <= 0)
        {
            HandleWeaponBreak();
        }
    }

    void HandleWeaponBreak()
    {
        Debug.Log($"{CurrentWeapon.weaponName} is BROKEN!");
        // ลอจิกบังคับเปลี่ยนอาวุธชิ้นถัดไป ถ้าพังหมดให้เรียก GameOver
    }

    void UpdateAPUI()
    {
        textAP.text = $"AP: {currentAP}/{maxAP}";
    }
}