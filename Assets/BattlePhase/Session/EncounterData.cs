using System.Collections.Generic;
using UnityEngine;

public enum EncounterType { Standard, Standard_Fly, Boss, Ambush }

[System.Serializable]
public class EnemyWave
{
    public string waveName = "Wave 1";
    public List<GameObject> enemiesInWave = new List<GameObject>();
}

[System.Serializable]
public class ItemReward
{
    public ItemData item;
    public int amount = 1;
}

// ข้อมูลการต่อสู้ 1 ครั้ง ที่ฉากอื่น (เช่น ClimbScene) ส่งเข้ามาผ่าน BattleSession
[CreateAssetMenu(fileName = "NewEncounter", menuName = "Ascendia/Encounter Data")]
public class EncounterData : ScriptableObject
{
    public string encounterName = "Encounter";
    public EncounterType encounterType = EncounterType.Standard;

    [Tooltip("Wave แรกจะเกิดทันทีตอนเริ่มฉาก Wave ถัดไปจะเกิดเมื่อเคลียร์ Wave ก่อนหน้า")]
    public List<EnemyWave> waves = new List<EnemyWave>();

    [Header("Rewards (ได้เมื่อชนะ ฉากที่เรียกเป็นคนเอาเข้ากระเป๋า)")]
    public List<ItemReward> rewards = new List<ItemReward>();

    // ของรางวัลที่ใช้ได้จริง (ข้ามช่องที่ยังไม่ได้ใส่ไอเทม หรือจำนวน <= 0)
    public List<ItemReward> GetValidRewards()
    {
        return rewards.FindAll(r => r != null && r.item != null && r.amount > 0);
    }
}
