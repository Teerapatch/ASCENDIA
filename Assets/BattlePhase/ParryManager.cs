using UnityEngine;
using UnityEngine.InputSystem;

public class ParryManager : MonoBehaviour
{
    public static ParryManager Instance;

    [Header("Settings")]
    public float parryWindowDuration = 1.5f;
    public float swipeThreshold = 50f;

    [Header("Visuals & UI")]
    public GameObject hintParticle;

    [Header("Parry Trail (Swipe Line)")]
    public bool showSwipeTrail = true; // เปิด-ปิดการโชว์เส้น
    public LineRenderer swipeLine; // ลาก LineRenderer มาใส่

    private bool isParryActive = false;
    private float timer = 0f;
    private Vector2 requiredDirection;
    private int incomingDamage;

    private Vector2 startPosScreen;
    private bool isSwiping;
    private Camera mainCam;

    private EnemyController attackingEnemy;

    private void Awake()
    {
        Instance = this;
        mainCam = Camera.main;
        if (swipeLine) swipeLine.positionCount = 0; // ซ่อนเส้นตอนเริ่ม
        hintParticle.SetActive(false);
    }

    public void StartParryWindow(Vector2 direction, int damage, EnemyController attacker)
    {
        requiredDirection = direction.normalized;
        incomingDamage = damage;
        attackingEnemy = attacker; // เก็บเป้าหมายไว้สวนกลับ

        timer = parryWindowDuration;
        isParryActive = true;
        isSwiping = false;

        hintParticle.SetActive(true);
        // หมุน Sprite/Particle ให้ตรงทิศ
        float angle = Mathf.Atan2(requiredDirection.y, requiredDirection.x) * Mathf.Rad2Deg;
        hintParticle.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void Update()
    {
        if (!isParryActive) return;

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            FailParry("Too slow!");
            return;
        }

        HandleInputAndTrail();
    }

    void HandleInputAndTrail()
    {
        if (Mouse.current == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = GetWorldPosition(mouseScreenPos);

        // 1. กดคลิกเริ่มลาก
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

        // 2. กำลังลาก (อัปเดตเส้น)
        if (isSwiping && showSwipeTrail && swipeLine != null)
        {
            swipeLine.SetPosition(1, mouseWorldPos);
        }

        // 3. ปล่อยเมาส์
        if (Mouse.current.leftButton.wasReleasedThisFrame && isSwiping)
        {
            isSwiping = false;
            if (swipeLine) swipeLine.positionCount = 0; // ซ่อนเส้น

            Vector2 swipeDelta = mouseScreenPos - startPosScreen;
            if (swipeDelta.magnitude >= swipeThreshold)
            {
                float dot = Vector2.Dot(swipeDelta.normalized, requiredDirection);
                if (dot > 0.8f) SuccessParry();
                else FailParry("Wrong direction!");
            }
        }
    }

    Vector3 GetWorldPosition(Vector2 screenPos)
    {
        // คำนวณหาจุดในโลก 3D/2D จากเมาส์ (ห่างจากกล้อง 10 หน่วย)
        Vector3 pos = new Vector3(screenPos.x, screenPos.y, 10f);
        return mainCam.ScreenToWorldPoint(pos);
    }

    void SuccessParry()
    {
        isParryActive = false;
        hintParticle.SetActive(false);
        CombatManager.Instance.player.RestoreAP(1);

        WeaponData weapon = CombatManager.Instance.player.ActiveWeapon;
        int counterDamage = Mathf.RoundToInt(weapon.baseDamage * weapon.parryMultiplier);

        Debug.Log($"<color=cyan>PARRY SUCCESS! Countering with {counterDamage} Posture Damage!</color>");
        attackingEnemy.TakePostureDamage(counterDamage); // สวนกลับหลอดสีเหลือง (Posture)
        attackingEnemy.TakeDamage(counterDamage); // สวนกลับหลอดเลือด (HP)

        CombatManager.Instance.EndCurrentTurn();
    }

    void FailParry(string reason)
    {
        isParryActive = false;
        hintParticle.SetActive(false);
        CombatManager.Instance.player.TakeDamage(incomingDamage);
        CombatManager.Instance.EndCurrentTurn();
    }
}