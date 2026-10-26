using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ฉากทดสอบ 1 Loop ก่อนเข้า Camp ครั้งแรก: ปีน -> เลือกโหนด -> ต่อสู้ -> ปีนต่อ ... -> โหนด Camp
/// โหลด ClimbScene แบบ Additive แล้วตั้ง BattleSession ให้ซ้อนฉากต่อสู้ทับ (ฉากปีนไม่ถูกโหลดใหม่ทุกครั้ง)
/// เมื่อเลือกโหนด Camp เกมจะโหลด CampScene ตามปกติ = จบ Loop
/// </summary>
public class PlaytestLoopDirector : MonoBehaviour
{
    [Header("Scenes")]
    public string climbSceneName = "ClimbScene";

    [Header("Run Setup")]
    [Tooltip("จำนวนชั้นก่อนถึงโหนด Camp (0 = ใช้ค่า maxFloorBeforeCamp ใน PlayerData)")]
    public int floorsBeforeCamp = 4;

    [Tooltip("อาวุธเริ่มต้นก่อนเข้า Camp ครั้งแรก (ว่าง = ฉากต่อสู้ใช้ baseInventoryWeapons ของตัวเอง)")]
    public List<WeaponData> startingLoadout = new List<WeaponData>();

    [Header("Debug")]
    public bool showHud = true;

    [Tooltip("Encounter ที่ใช้กับเมนู 'Debug/Start Battle Now' (ว่าง = ศัตรูที่ตั้งไว้ในฉากต่อสู้)")]
    public EncounterData debugEncounter;

    private int battlesWon;
    private int battlesLost;
    private string lastEvent = "เริ่ม Loop";
    private GUIStyle hudStyle;

    private void Start()
    {
        BattleSession.LoadMode = LoadSceneMode.Additive;
        CombatManager.OnBattleEnded += HandleBattleEnded;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.LoadScene(climbSceneName, LoadSceneMode.Additive);
    }

    private void OnDestroy()
    {
        BattleSession.LoadMode = LoadSceneMode.Single;
        CombatManager.OnBattleEnded -= HandleBattleEnded;
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        PlayerData data = GetPlayerData();
        if (data != null)
        {
            Debug.Log($"<color=cyan>[Playtest] สรุป Loop: ถึงชั้น {data.currentFloor}/{data.maxFloorBeforeCamp} | ชนะ {battlesWon} แพ้ {battlesLost} | Piton {data.piton} | Gem {data.gemStone} | Monster Part {data.monsterPart}</color>");
        }
    }

    // sceneLoaded ถูกเรียกหลัง Awake ของฉากปีน (GameManager รีเซ็ต PlayerData แล้ว) แต่ก่อน Start
    // จึงตั้งค่าตรงนี้ทันก่อนที่ NodeSpawner / RoomManager จะอ่านค่า
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != climbSceneName) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        // ห้อง/โหนดที่ถูก Instantiate จะได้ไปอยู่ในฉากปีน และใช้แสงของฉากปีน
        SceneManager.SetActiveScene(scene);

        PlayerData data = GetPlayerData();
        if (data == null)
        {
            Debug.LogWarning("[Playtest] ไม่พบ GameManager/PlayerData ใน ClimbScene");
            return;
        }

        if (floorsBeforeCamp > 0) data.maxFloorBeforeCamp = floorsBeforeCamp;

        for (int i = 0; i < startingLoadout.Count && i < data.activeLoadout.Length; i++)
        {
            data.activeLoadout[i] = startingLoadout[i];
        }

        Debug.Log($"<color=cyan>[Playtest] เริ่ม Loop: {data.maxFloorBeforeCamp} ชั้นก่อนถึง Camp</color>");
    }

    private void HandleBattleEnded(BattleSummary summary)
    {
        if (summary.outcome == BattleOutcome.Victory)
        {
            battlesWon++;
            List<string> gained = summary.rewards.ConvertAll(r => $"{r.item.itemName} x{r.amount}");
            lastEvent = gained.Count > 0 ? "Won: " + string.Join(", ", gained) : "Won (no rewards)";
        }
        else
        {
            battlesLost++;
            lastEvent = "Lost (-1 Piton)";
        }
    }

    private static PlayerData GetPlayerData()
    {
        return GameManager.Instance != null ? GameManager.Instance.playerData : null;
    }

    // ==========================================
    // เมนูลัดสำหรับทดสอบ (คลิกขวาที่ Component ตอน Play)
    // ==========================================
    [ContextMenu("Debug/Start Battle Now")]
    public void DebugStartBattle()
    {
        if (BattleSession.IsActive) return;

        PlayerData data = GetPlayerData();
        BattleSession.Begin(new BattleRequest
        {
            encounter = debugEncounter,
            weapons = data != null ? data.GetEquippedWeapons() : null,
            defeatPenaltyText = "-1 Piton"
        });
    }

    [ContextMenu("Debug/Force Win Battle")]
    public void DebugWinBattle()
    {
        BattleSession.Finish(BattleOutcome.Victory);
    }

    [ContextMenu("Debug/Force Lose Battle")]
    public void DebugLoseBattle()
    {
        BattleSession.Finish(BattleOutcome.Defeat);
    }

    private void OnGUI()
    {
        if (!showHud) return;

        PlayerData data = GetPlayerData();
        if (data == null) return;

        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };
            hudStyle.padding = new RectOffset(10, 10, 8, 8);
        }

        string phase = BattleSession.IsActive ? "<color=#FF7777>Battle</color>" : "<color=#77FF77>Climb</color>";
        string text =
            $"<b>Loop Playtest</b>  [{phase}]\n" +
            $"Floor {data.currentFloor} / {data.maxFloorBeforeCamp}   Camp Lv {data.currentCampLevel}\n" +
            $"Piton {data.piton}   Stamina {data.stamina:0}/{data.maxStamina:0}\n" +
            $"Gem {data.gemStone}   Monster {data.monsterPart}   Weight {data.weight:0.#}/{data.maxWeight:0}\n" +
            $"Battles  W {battlesWon} / L {battlesLost}\n" +
            $"<i>{lastEvent}</i>";

        const float width = 330f;
        const float height = 130f;
        GUI.Box(new Rect(10f, Screen.height - height - 10f, width, height), text, hudStyle);
    }
}
