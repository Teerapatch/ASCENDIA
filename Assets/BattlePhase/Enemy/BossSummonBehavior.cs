using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(EnemyController))]
public class BossSummonBehavior : MonoBehaviour
{
    [Header("Summon Settings")]
    [Tooltip("รายชื่อ Prefab ของลูกน้องที่จะเรียกออกมา")]
    public List<GameObject> minionsToSummon;

    [Range(0.1f, 1.0f)]
    [Tooltip("เงื่อนไขเลือด: เรียกได้เมื่อเลือดเหลือน้อยกว่าหรือเท่ากับกี่เปอร์เซ็นต์ (เช่น 0.5 คือ 50%)")]
    public float healthThreshold = 0.5f;

    [Header("Presentation")]
    public string summonMessage = "Rallying the Minions!";

    private EnemyController enemyController;
    private bool hasSummoned = false; // ป้องกันไม่ให้เรียกซ้ำ

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
    }

    /// <summary>
    /// ฟังก์ชันที่จะถูกเรียกจาก EnemyController เมื่อถึงเทิร์นของบอส
    /// </summary>
    /// <returns>คืนค่าเป็น true ถ้าบอสทำการเรียกพรรคพวกในเทิร์นนี้ (เพื่อจะได้ข้ามการโจมตีปกติ)</returns>
    public bool TryExecuteSummon()
    {
        if (hasSummoned || minionsToSummon == null || minionsToSummon.Count == 0)
            return false;

        // คำนวณเปอร์เซ็นต์เลือดปัจจุบัน
        float currentHpPercent = (float)enemyController.currentHP / enemyController.maxHP;

        if (currentHpPercent <= healthThreshold)
        {
            hasSummoned = true;
            Debug.Log($"<color=red>👑 BOSS SUMMON TRIGGERED! Health is at {currentHpPercent * 100}%</color>");

            // 1. แสดงป้ายประกาศ (ถ้ามีระบบ TurnAnnouncementUI)
            if (TurnAnnouncementUI.Instance != null)
            {
                TurnAnnouncementUI.Instance.AnnounceTurn($"<color=#FF4444>{enemyController.enemyName}</color>\n{summonMessage}", () =>
                {
                    // 2. สั่งให้ CombatManager เสกลูกน้องและอัปเดต Timeline
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.SpawnMinions(minionsToSummon);
                    }
                });
            }
            else
            {
                // กรณีไม่มีป้ายประกาศ ให้เสกและจบเทิร์นทันที
                if (CombatManager.Instance != null)
                {
                    CombatManager.Instance.SpawnMinions(minionsToSummon);
                }
            }

            return true; // บอกว่าเทิร์นนี้ใช้เป็นการเรียกพรรคพวกแล้ว
        }

        return false; // เลือดยังไม่ถึงเกณฑ์ ให้สู้ต่อตามปกติ
    }
}