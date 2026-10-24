using UnityEngine;
using UnityEngine.SceneManagement;

public enum BattleOutcome { Victory, Defeat }

/// <summary>
/// จุดเชื่อมระหว่างฉากอื่น (เช่น ClimbScene) กับฉาก TurnBase_Phase
/// - ฉากที่เรียก: BattleSession.StartBattle(EncounterType, ชื่อซีนต่อสู้, ชื่อซีนที่จะกลับ)
/// - ฉากต่อสู้: CombatManager อ่าน BattleSession.CurrentEncounter แล้วเรียก Finish() ตอนจบ
/// ถ้าเปิด TurnBase_Phase ตรงๆ (ไม่มี Request) จะทำงานแบบ Standalone เหมือนเดิม
/// </summary>
public static class BattleSession
{
    public static bool HasRequest { get; private set; }
    public static EncounterType CurrentEncounter { get; private set; } = EncounterType.Standard;
    public static string ReturnSceneName { get; private set; }
    public static BattleOutcome? LastOutcome { get; private set; }

    // ล้างค่า static ทุกครั้งที่กด Play (กันค่าค้างกรณีปิด Domain Reload)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        HasRequest = false;
        CurrentEncounter = EncounterType.Standard;
        ReturnSceneName = null;
        LastOutcome = null;
    }

    public static void StartBattle(EncounterType encounter, string battleSceneName, string returnSceneName)
    {
        HasRequest = true;
        CurrentEncounter = encounter;
        ReturnSceneName = returnSceneName;
        LastOutcome = null;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log($"<color=orange>⚔️ BattleSession: {encounter} -> {battleSceneName} (return to {returnSceneName})</color>");
        SceneManager.LoadScene(battleSceneName);
    }

    public static EncounterType EncounterFor(PathNode.RoomType roomType)
    {
        switch (roomType)
        {
            case PathNode.RoomType.MiniBoss: return EncounterType.MiniBoss;
            case PathNode.RoomType.Boss: return EncounterType.Boss;
            default: return EncounterType.Standard;
        }
    }

    public static bool IsCombatRoom(PathNode.RoomType roomType)
    {
        return roomType == PathNode.RoomType.Battle ||
               roomType == PathNode.RoomType.MiniBoss ||
               roomType == PathNode.RoomType.Boss;
    }

    /// <summary>
    /// ถูกเรียกจาก CombatManager เมื่อการต่อสู้จบ: บันทึกผลลง PlayerData แล้วกลับซีนเดิม
    /// คืนค่า false ถ้าเป็นโหมด Standalone (ให้ฉากต่อสู้แสดงหน้าจอผลลัพธ์เองต่อไป)
    /// </summary>
    public static bool Finish(BattleOutcome outcome)
    {
        LastOutcome = outcome;
        Time.timeScale = 1f;

        if (!HasRequest) return false;

        ApplyOutcomeToPlayerData(outcome);

        string returnScene = ReturnSceneName;
        HasRequest = false;
        ReturnSceneName = null;

        SceneManager.LoadScene(returnScene);
        return true;
    }

    private static void ApplyOutcomeToPlayerData(BattleOutcome outcome)
    {
        if (GameManager.Instance == null || GameManager.Instance.playerData == null) return;

        // แพ้การต่อสู้ = เสีย Piton (ชีวิต) 1 อัน เหมือนตอน Stamina หมดระหว่างปีน
        if (outcome == BattleOutcome.Defeat)
        {
            GameManager.Instance.UsePiton();
        }

        // TODO: ดรอปของหลังชนะ (เช่น InventoryManager.Instance.AddItem(monsterPartItem, n))
    }
}
