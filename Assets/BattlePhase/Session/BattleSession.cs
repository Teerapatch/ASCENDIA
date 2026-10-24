using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum BattleOutcome { Victory, Defeat }

public class BattleRequest
{
    public EncounterData encounter;      // null = ใช้ศัตรูที่ตั้งไว้ใน CombatManager ของฉากต่อสู้
    public List<WeaponData> weapons;     // null/ว่าง = ใช้ baseInventoryWeapons ของ PlayerCombatController
    public string battleSceneName = BattleSession.DefaultBattleScene;
    public string returnSceneName;       // ว่าง = กลับไปฉากที่เรียก Begin()
}

public class BattleResult
{
    public BattleOutcome outcome;
    public EncounterData encounter;
}

/// <summary>
/// สะพานเชื่อมระหว่างฉากอื่นกับฉากต่อสู้ (เป็น static จึงอยู่รอดข้ามการโหลดฉาก)
///   ฉากที่เรียก : BattleSession.Begin(request)  -> โหลดฉากต่อสู้
///   ฉากต่อสู้   : BattleSession.Current         -> อ่านศัตรู/อาวุธที่ถูกส่งมา
///                 BattleSession.Finish(outcome) -> โหลดกลับฉากเดิม
///   ฉากที่เรียก : BattleSession.TryConsumeResult(out result) ตอนโหลดกลับมา
/// ถ้าเปิดฉากต่อสู้ตรงๆ ใน Editor (Current == null) ฉากต่อสู้จะใช้ค่าที่ตั้งไว้ในฉากเหมือนเดิม
/// </summary>
public static class BattleSession
{
    public const string DefaultBattleScene = "TurnBase_Phase";

    public static BattleRequest Current { get; private set; }
    public static bool IsActive => Current != null;

    private static BattleResult pendingResult;

    public static void Begin(BattleRequest request)
    {
        if (request == null) return;

        if (string.IsNullOrEmpty(request.battleSceneName)) request.battleSceneName = DefaultBattleScene;
        if (string.IsNullOrEmpty(request.returnSceneName)) request.returnSceneName = SceneManager.GetActiveScene().name;

        Current = request;
        pendingResult = null;
        SceneManager.LoadScene(request.battleSceneName);
    }

    public static void Finish(BattleOutcome outcome)
    {
        if (Current == null) return;

        pendingResult = new BattleResult { outcome = outcome, encounter = Current.encounter };
        string returnScene = Current.returnSceneName;
        Current = null;

        // กันกรณีออกจากฉากระหว่าง Hitstop หรือหน้า Pause
        Time.timeScale = 1f;
        SceneManager.LoadScene(returnScene);
    }

    public static bool TryConsumeResult(out BattleResult result)
    {
        result = pendingResult;
        pendingResult = null;
        return result != null;
    }

    // รองรับ Enter Play Mode แบบไม่ Reload Domain (ค่า static จะไม่ถูกล้างเอง)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
        pendingResult = null;
    }
}
