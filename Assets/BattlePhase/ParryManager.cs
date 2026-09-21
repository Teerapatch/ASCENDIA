using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class ParryManager : MonoBehaviour
{
    public static ParryManager Instance;

    [Header("Settings")]
    public float swipeThreshold = 50f; //[cite: 6]

    [Header("Visuals & UI")]
    public GameObject hintParticle; //[cite: 6]
    public bool showSwipeTrail = true;
    public LineRenderer swipeLine;

    private Vector2 requiredDirection;
    private int incomingDamage;
    private int currentPostureDamage;

    private Vector2 startPosScreen;
    private bool isSwiping;
    private Camera mainCam;

    private EnemyController attackingEnemy;

    // ตัวแปรควบคุม State รายฮิต
    private bool isAwaitingInput = false;
    private bool isStrikeResolved = false;

    private void Awake()
    {
        Instance = this;
        mainCam = Camera.main; //[cite: 6]
        if (swipeLine) swipeLine.positionCount = 0;
        hintParticle.SetActive(false); //[cite: 6]
    }

    public void StartParrySequence(EnemyAttackPattern pattern, EnemyController attacker)
    {
        attackingEnemy = attacker;
        StartCoroutine(PlaySequenceRoutine(pattern));
    }

    private IEnumerator PlaySequenceRoutine(EnemyAttackPattern pattern)
    {
        // ลูปตามจำนวนการฟัน (Strikes) ในชุดคอมโบ
        for (int i = 0; i < pattern.strikes.Count; i++)
        {
            ParryStrike strike = pattern.strikes[i];

            // 1. ดีเลย์ก่อนฟัน (ซิงก์จังหวะง้างดาบ)
            yield return new WaitForSeconds(strike.delayBeforeStrike);

            // 2. ตั้งค่าการป้องกันสำหรับฮิตนี้
            requiredDirection = GetDirectionFromIndex(strike.sliderDirectionIndex);
            incomingDamage = Mathf.RoundToInt(strike.damageIfHit);
            currentPostureDamage = Mathf.RoundToInt(strike.postureDamageIfParried);

            isAwaitingInput = true;
            isStrikeResolved = false;
            isSwiping = false;

            // เปิด UI แจ้งเตือนทิศทาง[cite: 6]
            hintParticle.SetActive(true);
            float angle = Mathf.Atan2(requiredDirection.y, requiredDirection.x) * Mathf.Rad2Deg;
            hintParticle.transform.rotation = Quaternion.Euler(0, 0, angle);

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
            hintParticle.SetActive(false); //[cite: 6]
            if (swipeLine) swipeLine.positionCount = 0;
        }

        // จบชุดคอมโบ ส่งเทิร์นคืนให้ Timeline[cite: 6]
        CombatManager.Instance.EndCurrentTurn();
    }

    void HandleSwipeInput()
    {
        if (Mouse.current == null || !isAwaitingInput) return; //[cite: 6]

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = GetWorldPosition(mouseScreenPos);

        // กดคลิกเริ่มลาก[cite: 6]
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            startPosScreen = mouseScreenPos;
            isSwiping = true;

            if (showSwipeTrail && swipeLine != null)
            {
                swipeLine.positionCount = 2; //[cite: 6]
                swipeLine.SetPosition(0, mouseWorldPos);
                swipeLine.SetPosition(1, mouseWorldPos);
            }
        }

        // กำลังลาก (อัปเดตเส้น)[cite: 6]
        if (isSwiping && showSwipeTrail && swipeLine != null)
        {
            swipeLine.SetPosition(1, mouseWorldPos);
        }

        // ปล่อยเมาส์ เช็คผลลัพธ์[cite: 6]
        if (Mouse.current.leftButton.wasReleasedThisFrame && isSwiping)
        {
            isSwiping = false;
            if (swipeLine) swipeLine.positionCount = 0;

            Vector2 swipeDelta = mouseScreenPos - startPosScreen;
            if (swipeDelta.magnitude >= swipeThreshold) //[cite: 6, 7]
            {
                // ตรวจสอบ Dot Product ว่าลากตรงทิศหรือไม่[cite: 6, 7]
                float dot = Vector2.Dot(swipeDelta.normalized, requiredDirection);
                if (dot > 0.8f) SuccessStrike(); //[cite: 6, 7]
                else FailStrike("Wrong direction!"); //[cite: 6]
            }
            else
            {
                FailStrike("Swipe too short!");
            }
        }
    }

    Vector2 GetDirectionFromIndex(int index)
    {
        switch (index)
        {
            case 0: return Vector2.right; // ขวา
            case 1: return Vector2.left;  // ซ้าย
            case 2: return Vector2.down;  // ลง
            case 3: return Vector2.up;    // ขึ้น
            case 4: return new Vector2(1, 1).normalized;   // ขวาบน
            case 5: return new Vector2(-1, 1).normalized;  // ซ้ายบน
            case 6: return new Vector2(1, -1).normalized;  // ขวาล่าง
            case 7: return new Vector2(-1, -1).normalized; // ซ้ายล่าง
            default: return Vector2.right;
        }
    }

    Vector3 GetWorldPosition(Vector2 screenPos)
    {
        Vector3 pos = new Vector3(screenPos.x, screenPos.y, 10f); //[cite: 6]
        return mainCam.ScreenToWorldPoint(pos); //[cite: 6]
    }

    void SuccessStrike()
    {
        isStrikeResolved = true;
        isAwaitingInput = false;

        Debug.Log($"<color=cyan>PARRY SUCCESS! Countering with {currentPostureDamage} Posture Damage!</color>");
        CameraShakeManager.Instance.Shake(0.1f);

        // ฟื้น AP และหักเกจ Posture ศัตรู[cite: 6]
        CombatManager.Instance.player.RestoreAP(1);
        attackingEnemy.TakePostureDamage(currentPostureDamage);
    }

    void FailStrike(string reason)
    {
        isStrikeResolved = true;
        isAwaitingInput = false;

        Debug.Log($"<color=red>PARRY FAILED ({reason}) - Took {incomingDamage} Damage!</color>");
        CameraShakeManager.Instance.Shake(0.8f);

        // รับดาเมจเข้าตัว/อาวุธผู้เล่น[cite: 6]
        CombatManager.Instance.player.TakeDamage(incomingDamage);
    }
}