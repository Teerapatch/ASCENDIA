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
        int turnNumber = CombatManager.Instance.TurnNumber;

        // ลูปตามจำนวนการฟัน (Strikes) ในชุดคอมโบ
        for (int i = 0; i < pattern.strikes.Count; i++)
        {
            ParryStrike strike = pattern.strikes[i];

            // 1. ดีเลย์ก่อนฟัน (ซิงก์จังหวะง้างดาบ)
            yield return new WaitForSeconds(strike.delayBeforeStrike);

            // ผู้โจมตีตายจากการสวนกลับ หรือการต่อสู้จบไปแล้ว -> หยุดคอมโบที่เหลือ
            if (!IsSequenceStillValid(turnNumber)) break;

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

        hintParticle.SetActive(false);
        if (swipeLine) swipeLine.positionCount = 0;
        isAwaitingInput = false;

        // จบชุดคอมโบ ส่งเทิร์นคืนให้ Timeline (เฉพาะถ้ายังเป็นเทิร์นเดิม ไม่ใช่เทิร์นของ Wave ใหม่)
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
        if (Mouse.current == null || !isAwaitingInput) return; //[cite: 6]

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

                    OnParrySuccess(startWorldPos, endWorldPos);
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

    public void OnParrySuccess(Vector3 startPos, Vector3 endPos)
    {
        isStrikeResolved = true;
        isAwaitingInput = false;

        bool isSpellActive = (PlayerCombatController.Instance != null &&
                              PlayerCombatController.Instance.ActiveWeapon != null &&
                              PlayerCombatController.Instance.ActiveWeapon.weaponType == WeaponType.Spell);

        Debug.Log($"<color=cyan>PARRY SUCCESS! Countering with {currentPostureDamage} Posture Damage!</color>");
        CameraShakeManager.TryShake(0.1f);

        if (isSpellActive)
        {
            int smallPosture = Mathf.RoundToInt(currentPostureDamage * 0.4f);
            attackingEnemy.TakePostureDamage(smallPosture);

            // แปะดีบัฟเพิ่มความเสียหายที่ศัตรูได้รับ (สมมติมีฟังก์ชัน ApplyVulnerabilityDebuff บน EnemyController)
            // attackingEnemy.ApplyVulnerabilityDebuff(1.25f, 2); // รับดาเมจเพิ่ม 25% เป็นเวลา 2 เทิร์น

            // สุ่มธาตุแล้วส่งเข้าไปเก็บใน Slot ของผู้เล่น
            PlayerCombatController.Instance.AddElement(ElementTypes.RandomPlayable());

            Debug.Log("<color=cyan>Applied Vulnerability Debuff, Gained Elemental Slot</color>");
        }
        else
        {
            attackingEnemy.TakePostureDamage(currentPostureDamage);
            if (PlayerCombatController.Instance != null)
            {
                PlayerCombatController.Instance.AddComboStack(1);
                PlayerCombatController.Instance.RestoreAPFromAction(1, attackingEnemy);
            }
        }

        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.SpawnParrySlash(startPos, endPos);
        }

        if (HitstopManager.Instance != null)
        {
            HitstopManager.Instance.Freeze(0.12f);
        }
    }

    void FailStrike(string reason)
    {
        isStrikeResolved = true;
        isAwaitingInput = false;

        Debug.Log($"<color=red>PARRY FAILED ({reason}) - Took {incomingDamage} Damage!</color>");
        CameraShakeManager.TryShake(0.8f);

        // รับดาเมจเข้าตัว/อาวุธผู้เล่น[cite: 6]
        CombatManager.Instance.player.TakeDamage(incomingDamage);
    }
}