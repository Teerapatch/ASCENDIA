using System.Collections.Generic;

// สรุปผลการต่อสู้ 1 ครั้ง สร้างโดย CombatManager ตอนจบ แล้วส่งให้หน้าสรุปผล / ระบบอื่นที่ฟัง OnBattleEnded
public class BattleSummary
{
    public BattleOutcome outcome;
    public EncounterData encounter;

    // ของที่ได้จากการต่อสู้ (ว่างถ้าแพ้)
    public List<ItemReward> rewards = new List<ItemReward>();

    public int enemiesDefeated;
    public int wavesCleared;
    public int totalWaves;
    public int turnsTaken;
    public int weaponsBroken;
    public int totalWeapons;
}
