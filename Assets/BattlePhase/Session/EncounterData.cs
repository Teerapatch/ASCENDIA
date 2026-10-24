using System.Collections.Generic;
using UnityEngine;

public enum EncounterType { Standard, Standard_Fly, Boss, Ambush }

[System.Serializable]
public class EnemyWave
{
    public string waveName = "Wave 1";
    public List<GameObject> enemiesInWave = new List<GameObject>();
}

// ข้อมูลการต่อสู้ 1 ครั้ง ที่ฉากอื่น (เช่น ClimbScene) ส่งเข้ามาผ่าน BattleSession
[CreateAssetMenu(fileName = "NewEncounter", menuName = "Ascendia/Encounter Data")]
public class EncounterData : ScriptableObject
{
    public string encounterName = "Encounter";
    public EncounterType encounterType = EncounterType.Standard;

    [Tooltip("Wave แรกจะเกิดทันทีตอนเริ่มฉาก Wave ถัดไปจะเกิดเมื่อเคลียร์ Wave ก่อนหน้า")]
    public List<EnemyWave> waves = new List<EnemyWave>();

    [Header("Rewards (ส่งกลับไปให้ฉากที่เรียก)")]
    public ItemData rewardItem;
    public int rewardAmount = 0;
}
