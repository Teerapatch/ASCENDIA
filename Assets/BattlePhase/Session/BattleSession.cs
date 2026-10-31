using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum BattleOutcome { Victory, Defeat }

public class BattleRequest
{
    public EncounterData encounter;      // null = ใช้ศัตรูที่ตั้งไว้ใน CombatManager ของฉากต่อสู้
    public List<WeaponData> weapons;     // null/ว่าง = ใช้ baseInventoryWeapons ของ PlayerCombatController
    public string battleSceneName = BattleSession.DefaultBattleScene;
    public string returnSceneName;       // ว่าง = กลับไปฉากที่เรียก Begin() (ใช้เฉพาะโหมด Single)
    public string defeatPenaltyText;     // ข้อความบทลงโทษตอนแพ้ที่โชว์บนหน้าสรุปผล (ฉากที่เรียกเป็นคนกำหนดกติกา)

    // สกิลที่เลือกพกของอาวุธแต่ละชิ้น (key = ไฟล์อาวุธต้นฉบับ) null/ไม่มี key = ใช้ 3 สกิลแรกของอาวุธ
    public Dictionary<WeaponData, List<WeaponSkill>> equippedSkills;
}

public class BattleResult
{
    public BattleOutcome outcome;
    public EncounterData encounter;

    // true = ฉากที่เรียกไม่ได้ถูกโหลดใหม่ (โหมด Additive) สถานะในฉากยังอยู่ครบ ต้องเดินเกมต่อเอง
    public bool callerSceneKept;
}

/// <summary>
/// สะพานเชื่อมระหว่างฉากอื่นกับฉากต่อสู้ (เป็น static จึงอยู่รอดข้ามการโหลดฉาก)
///   ฉากที่เรียก : BattleSession.Begin(request)  -> โหลดฉากต่อสู้
///   ฉากต่อสู้   : BattleSession.Current         -> อ่านศัตรู/อาวุธที่ถูกส่งมา
///                 BattleSession.Finish(outcome) -> กลับฉากเดิม
///   ฉากที่เรียก : BattleSession.TryConsumeResult(out result) ตอนกลับมา (OnEnable)
/// ถ้าเปิดฉากต่อสู้ตรงๆ ใน Editor (Current == null) ฉากต่อสู้จะใช้ค่าที่ตั้งไว้ในฉากเหมือนเดิม
///
/// LoadMode:
///   Single   = โหลดฉากต่อสู้แทนที่ฉากเดิม แล้วโหลดฉากเดิมใหม่ตอนจบ (ค่าเริ่มต้น)
///   Additive = ซ้อนฉากต่อสู้ ปิด Root ของฉากเดิมไว้ชั่วคราว แล้วเปิดกลับตอนจบ (ใช้ใน Loop_Playtest)
/// </summary>
public static class BattleSession
{
    public const string DefaultBattleScene = "TurnBase_Phase";

    public static LoadSceneMode LoadMode = LoadSceneMode.Single;

    public static BattleRequest Current { get; private set; }
    public static bool IsActive => Current != null;

    private static BattleResult pendingResult;

    // ใช้เฉพาะโหมด Additive
    private static LoadSceneMode activeMode;
    private static Scene callerScene;
    private static readonly List<GameObject> hiddenCallerRoots = new List<GameObject>();

    public static void Begin(BattleRequest request)
    {
        if (request == null || IsActive) return;

        if (string.IsNullOrEmpty(request.battleSceneName)) request.battleSceneName = DefaultBattleScene;
        if (string.IsNullOrEmpty(request.returnSceneName)) request.returnSceneName = SceneManager.GetActiveScene().name;

        Current = request;
        pendingResult = null;
        activeMode = LoadMode;

        if (activeMode == LoadSceneMode.Single)
        {
            SceneManager.LoadScene(request.battleSceneName);
            return;
        }

        // ปิดฉากเดิมก่อนโหลด กล้อง/แสง/EventSystem ของฉากเดิมจะได้ไม่ตีกับของฉากต่อสู้
        callerScene = SceneManager.GetActiveScene();
        SetCallerRootsActive(false);

        SceneManager.sceneLoaded += OnBattleSceneLoaded;
        SceneManager.LoadSceneAsync(request.battleSceneName, LoadSceneMode.Additive);
    }

    // sceneLoaded ถูกเรียกหลัง Awake แต่ก่อน Start ของฉากใหม่
    // ศัตรูที่ CombatManager.Start() เสก จึงไปอยู่ในฉากต่อสู้และถูกลบไปพร้อมฉาก
    private static void OnBattleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Current == null || scene.name != Current.battleSceneName) return;

        SceneManager.sceneLoaded -= OnBattleSceneLoaded;
        SceneManager.SetActiveScene(scene);
    }

    public static void Finish(BattleOutcome outcome)
    {
        if (Current == null) return;

        BattleResult result = new BattleResult
        {
            outcome = outcome,
            encounter = Current.encounter,
            callerSceneKept = activeMode == LoadSceneMode.Additive
        };
        string battleScene = Current.battleSceneName;
        string returnScene = Current.returnSceneName;
        Current = null;

        // กันกรณีออกจากฉากระหว่าง Hitstop หรือหน้า Pause
        Time.timeScale = 1f;

        if (activeMode == LoadSceneMode.Single)
        {
            pendingResult = result;
            SceneManager.LoadScene(returnScene);
            return;
        }

        AsyncOperation unload = SceneManager.UnloadSceneAsync(battleScene);
        if (unload == null)
        {
            ReturnToCaller(result);
            return;
        }
        unload.completed += _ => ReturnToCaller(result);
    }

    private static void ReturnToCaller(BattleResult result)
    {
        if (callerScene.IsValid() && callerScene.isLoaded) SceneManager.SetActiveScene(callerScene);

        // ตั้งผลไว้ก่อนเปิดฉากเดิม เพราะ OnEnable ของฉากเดิมจะเรียก TryConsumeResult ทันที
        pendingResult = result;
        SetCallerRootsActive(true);
    }

    private static void SetCallerRootsActive(bool active)
    {
        if (!active)
        {
            hiddenCallerRoots.Clear();
            if (!callerScene.IsValid()) return;

            foreach (GameObject root in callerScene.GetRootGameObjects())
            {
                if (!root.activeSelf) continue;
                hiddenCallerRoots.Add(root);
                root.SetActive(false);
            }
            return;
        }

        foreach (GameObject root in hiddenCallerRoots)
        {
            if (root != null) root.SetActive(true);
        }
        hiddenCallerRoots.Clear();
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
        LoadMode = LoadSceneMode.Single;
        activeMode = LoadSceneMode.Single;
        callerScene = default;
        hiddenCallerRoots.Clear();
        SceneManager.sceneLoaded -= OnBattleSceneLoaded;
    }
}
