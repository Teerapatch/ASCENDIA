using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CombatState { Setup, CalculateTurn, PlayerTurn, EnemyTurn, ParryPhase, GameOver, Victory }
public enum EncounterType { Standard, MiniBoss, Boss }

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance;
    public CombatState currentState;
    public bool isTransitioningWave = false;

    // สูตร Action Value: ยิ่งเร็ว AV ยิ่งน้อย ได้เล่นก่อน
    private const float ActionValueBase = 10000f;
    private const int TurnsToPredict = 6;

    public bool IsBattleOver => currentState == CombatState.GameOver || currentState == CombatState.Victory;

    /// <summary>ยิงเมื่อการต่อสู้จบ (ก่อนกลับซีนเดิม) ให้ระบบอื่นมาฟังได้ เช่น UI สรุปผล / แจกของ</summary>
    public event System.Action<BattleOutcome> OnBattleEnded;

    [System.Serializable]
    public class WaveData
    {
        public string waveName = "Wave 1";
        public List<GameObject> enemiesInWave;
    }

    [System.Serializable]
    public class EncounterPreset
    {
        public EncounterType encounterType = EncounterType.Standard;
        [Tooltip("Wave แรกคือศัตรูที่เกิดตอนเริ่ม ที่เหลือจะเกิดต่อเมื่อเคลียร์ Wave ก่อนหน้า")]
        public List<WaveData> waves = new List<WaveData>();
    }

    [Header("Enemy Spawning (Default / Standalone)")]
    public List<GameObject> enemyPrefabsToSpawn;
    public List<Transform> enemySpawnPoints;

    [Header("Wave Settings (Optional)")]
    public List<WaveData> enemyWaves = new List<WaveData>();
    private int currentWaveIndex = 0;

    [Header("Encounter Presets (ใช้เมื่อถูกเรียกผ่าน BattleSession)")]
    [Tooltip("ถ้าไม่มี Preset ตรงกับ EncounterType ที่ถูกเรียก จะใช้ enemyPrefabsToSpawn / enemyWaves ด้านบนแทน")]
    public List<EncounterPreset> encounterPresets = new List<EncounterPreset>();

    [Header("Battle End")]
    [Tooltip("เวลาค้างหน้าจอผลลัพธ์ก่อนกลับซีนเดิม (วินาทีจริง ไม่ขึ้นกับ timeScale)")]
    public float returnDelay = 2f;

    [Header("Combatants (Player & Enemies)")]
    public PlayerCombatController player;
    public List<EnemyController> enemies;
    public TurnOrderUI turnOrderUI;

    public class PredictedTurn
    {
        public string UniqueID;
        public string Name;
        public bool IsEnemy;
        public float SimulatedAV;
    }

    // คลาสชั่วคราวสำหรับใช้ทดลองคำนวณในฟังก์ชัน
    private class SimNode
    {
        public string ActorID;
        public string Name;
        public bool IsEnemy;
        public float Speed;
        public float CurrentAV;
    }

    public class TurnNode
    {
        public string ActorID;
        public string Name;
        public float Speed;
        public float CurrentAV;
        public bool IsEnemy;
        public EnemyController EnemyRef;
        public WeaponData WeaponRef;
    }

    private List<TurnNode> turnQueue = new List<TurnNode>();

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Instance = this;
    }

    private void Start()
    {
        currentWaveIndex = 0;

        EncounterPreset preset = BattleSession.HasRequest
            ? encounterPresets.Find(p => p.encounterType == BattleSession.CurrentEncounter && p.waves.Count > 0)
            : null;

        if (preset != null)
        {
            enemyWaves = preset.waves.Skip(1).ToList();
            StartEncounter(preset.waves[0].enemiesInWave);
        }
        else
        {
            StartEncounter(enemyPrefabsToSpawn);
        }
    }

    // ==========================================
    // Spawning System
    // ==========================================
    public void StartEncounter(List<GameObject> enemiesToSpawn)
    {
        enemies.Clear();
        SpawnWave(enemiesToSpawn);

        Debug.Log($"<color=green>Encounter Started! Spawned {enemies.Count} enemies.</color>");

        BuildTurnQueue();
    }

    private void SpawnWave(List<GameObject> prefabs)
    {
        int spawnCount = Mathf.Min(prefabs.Count, enemySpawnPoints.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnEnemy(prefabs[i], enemySpawnPoints[i]);
        }
    }

    private EnemyController SpawnEnemy(GameObject prefab, Transform spawnPoint)
    {
        GameObject enemyObj = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        EnemyController controller = enemyObj.GetComponent<EnemyController>();

        if (controller == null)
        {
            Debug.LogError($"Prefab {enemyObj.name} ไม่มีสคริปต์ EnemyController!");
            return null;
        }

        enemies.Add(controller);
        return controller;
    }

    private void SpawnNextWave()
    {
        List<GameObject> nextEnemies = enemyWaves[currentWaveIndex].enemiesInWave;
        currentWaveIndex++;

        SpawnWave(nextEnemies);

        isTransitioningWave = false;
        BuildTurnQueue();
    }

    /// <summary>
    /// ฟังก์ชันสำหรับเสกลูกน้องของบอสกลางคัน และอัปเดต Timeline ทันที
    /// </summary>
    public void SpawnMinions(List<GameObject> minionPrefabs)
    {
        if (minionPrefabs == null || minionPrefabs.Count == 0) return;

        int spawnedCount = 0;

        foreach (var minionPrefab in minionPrefabs)
        {
            Transform spawnPoint = GetAvailableSpawnPoint();
            if (spawnPoint == null) continue;

            EnemyController minion = SpawnEnemy(minionPrefab, spawnPoint);
            if (minion == null) continue;

            turnQueue.Add(CreateEnemyNode(minion));
            spawnedCount++;
        }

        Debug.Log($"<color=magenta>✨ Successfully summoned {spawnedCount} minions into the battle!</color>");

        turnQueue = turnQueue.OrderBy(x => x.CurrentAV).ToList();
        RefreshTurnOrderUI();
    }

    /// <summary>
    /// Helper: หาจุดเกิดที่ยังว่างอยู่ หรือสุ่มจุดเกิดสำรอง
    /// </summary>
    private Transform GetAvailableSpawnPoint()
    {
        if (enemySpawnPoints == null || enemySpawnPoints.Count == 0) return null;

        foreach (var sp in enemySpawnPoints)
        {
            bool isOccupied = enemies.Any(enemy => enemy != null && Vector3.Distance(enemy.transform.position, sp.position) < 1.0f);
            if (!isOccupied) return sp;
        }

        return enemySpawnPoints[Random.Range(0, enemySpawnPoints.Count)];
    }

    // ==========================================
    // Turn Queue
    // ==========================================
    private static float InitialAV(float speed) => ActionValueBase / Mathf.Max(speed, 1f);

    private TurnNode CreateWeaponNode(WeaponData weapon)
    {
        float speed = weapon.GetCurrentSpeed();
        return new TurnNode
        {
            ActorID = "W_" + weapon.GetInstanceID(),
            Name = weapon.weaponName,
            Speed = speed,
            IsEnemy = false,
            CurrentAV = InitialAV(speed),
            WeaponRef = weapon
        };
    }

    private TurnNode CreateEnemyNode(EnemyController enemy)
    {
        return new TurnNode
        {
            ActorID = "E_" + enemy.GetInstanceID(),
            Name = enemy.enemyName,
            Speed = enemy.baseSpeed,
            IsEnemy = true,
            CurrentAV = InitialAV(enemy.baseSpeed),
            EnemyRef = enemy
        };
    }

    // ล้างคิวแล้วสร้างใหม่จากอาวุธที่ยังไม่พัง + ศัตรูทั้งหมดในฉาก (ใช้ตอนเริ่มและตอนขึ้น Wave ใหม่)
    private void BuildTurnQueue()
    {
        turnQueue.Clear();

        foreach (var weapon in player.inventoryWeapons)
        {
            if (weapon.currentDurability > 0) turnQueue.Add(CreateWeaponNode(weapon));
        }

        foreach (var enemy in enemies)
        {
            turnQueue.Add(CreateEnemyNode(enemy));
        }

        currentState = CombatState.CalculateTurn;
        NextTurn();
    }

    public void EndCurrentTurn()
    {
        if (IsBattleOver) return;

        if (turnQueue.Count > 0)
        {
            // รีเซ็ต AV ของคนที่เพิ่งเล่นจบ กลับไปต่อท้ายคิว
            turnQueue[0].CurrentAV = InitialAV(turnQueue[0].Speed);
        }

        currentState = CombatState.CalculateTurn;
        NextTurn();
    }

    public void NextTurn()
    {
        if (isTransitioningWave || IsBattleOver) return;

        var brokenWeapons = turnQueue
            .Where(node => !node.IsEnemy && node.WeaponRef != null && node.WeaponRef.currentDurability <= 0)
            .Select(node => node.WeaponRef)
            .ToList();

        foreach (var weapon in brokenWeapons)
        {
            RemoveWeapon(weapon);
        }

        if (IsBattleOver || turnQueue.Count == 0) return;

        turnQueue = turnQueue.OrderBy(x => x.CurrentAV).ToList();

        float avToSubtract = turnQueue[0].CurrentAV;
        foreach (var node in turnQueue) { node.CurrentAV -= avToSubtract; }

        RefreshTurnOrderUI();

        TurnNode activeTurn = turnQueue[0];

        if (!activeTurn.IsEnemy)
        {
            currentState = CombatState.PlayerTurn;
            player.StartTurn(activeTurn.WeaponRef);
            return;
        }

        currentState = CombatState.EnemyTurn;

        if (WeaponUIManager.Instance != null)
        {
            WeaponUIManager.Instance.UpdateWeaponUI(player.inventoryWeapons, player.ActiveWeapon);
        }

        string warningMsg = $"<color=#FF5555>{activeTurn.Name}</color>\nIs Attacking!";
        Announce(warningMsg, () => activeTurn.EnemyRef.StartTurn());
    }

    public void RemoveEnemy(EnemyController deadEnemy)
    {
        enemies.Remove(deadEnemy);
        turnQueue.RemoveAll(node => node.IsEnemy && node.EnemyRef == deadEnemy);
        RefreshTurnOrderUI();
    }

    public void RemoveWeapon(WeaponData brokenWeapon)
    {
        Debug.Log($"<color=red>Timeline: ถอด {brokenWeapon.weaponName} ออกจากคิว!</color>");

        turnQueue.RemoveAll(node => !node.IsEnemy && node.WeaponRef == brokenWeapon);
        RefreshTurnOrderUI();

        if (!HasUsableWeapon())
        {
            EndBattle(BattleOutcome.Defeat);
            return;
        }

        // ถ้าอาวุธที่พังคือชิ้นที่ผู้เล่นกำลังถืออยู่ ให้บังคับสลับอาวุธ
        if (player != null && player.ActiveWeapon == brokenWeapon)
        {
            player.Command_SwitchWeapon();
        }
    }

    private bool HasUsableWeapon()
    {
        return player.inventoryWeapons.Any(w => w.currentDurability > 0);
    }

    // ==========================================
    // Battle End
    // ==========================================
    public void CheckBattleEnd()
    {
        if (IsBattleOver) return;

        if (!HasUsableWeapon())
        {
            EndBattle(BattleOutcome.Defeat);
            return;
        }

        if (enemies.Count > 0) return;

        if (currentWaveIndex < enemyWaves.Count)
        {
            isTransitioningWave = true;
            Debug.Log($"<color=yellow>Wave {currentWaveIndex + 1} Cleared! Spawning Next Wave...</color>");

            Announce($"<color=#FFDD55>{enemyWaves[currentWaveIndex].waveName}</color>\nApproaching!", SpawnNextWave);
        }
        else
        {
            EndBattle(BattleOutcome.Victory);
        }
    }

    private void EndBattle(BattleOutcome outcome)
    {
        if (IsBattleOver) return;

        currentState = outcome == BattleOutcome.Victory ? CombatState.Victory : CombatState.GameOver;

        if (outcome == BattleOutcome.Victory)
        {
            Debug.Log("<color=green>BATTLE WON! All Waves Cleared.</color>");
            if (TempGameFlowUI.Instance != null) TempGameFlowUI.Instance.ShowVictory();
        }
        else
        {
            Debug.Log("<color=red>GAME OVER! All weapons broken.</color>");
            if (TempGameFlowUI.Instance != null) TempGameFlowUI.Instance.ShowGameOver();
        }

        OnBattleEnded?.Invoke(outcome);

        if (BattleSession.HasRequest)
        {
            StartCoroutine(ReturnAfterDelay(outcome));
        }
        else
        {
            BattleSession.Finish(outcome);
        }
    }

    private IEnumerator ReturnAfterDelay(BattleOutcome outcome)
    {
        yield return new WaitForSecondsRealtime(returnDelay);
        BattleSession.Finish(outcome);
    }

    // ==========================================
    // UI Helpers
    // ==========================================
    private void Announce(string message, System.Action onComplete)
    {
        if (TurnAnnouncementUI.Instance != null)
        {
            TurnAnnouncementUI.Instance.AnnounceTurn(message, onComplete);
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    public void RefreshTurnOrderUI()
    {
        if (turnOrderUI == null) return;
        turnOrderUI.UpdateUI(PredictNextTurns(TurnsToPredict));
    }

    private List<PredictedTurn> PredictNextTurns(int amountToPredict)
    {
        List<PredictedTurn> results = new List<PredictedTurn>();
        if (turnQueue.Count == 0) return results;

        List<SimNode> simQueue = turnQueue.Select(node => new SimNode
        {
            ActorID = node.ActorID,
            Name = node.Name,
            IsEnemy = node.IsEnemy,
            Speed = node.Speed,
            CurrentAV = node.CurrentAV
        }).ToList();

        float totalTimePassed = 0f;
        Dictionary<string, int> occurrenceCounts = new Dictionary<string, int>();

        for (int i = 0; i < amountToPredict; i++)
        {
            simQueue = simQueue.OrderBy(x => x.CurrentAV).ToList();
            SimNode nextActor = simQueue[0];

            float timePassed = nextActor.CurrentAV;
            totalTimePassed += timePassed;

            occurrenceCounts.TryGetValue(nextActor.ActorID, out int count);
            occurrenceCounts[nextActor.ActorID] = ++count;

            results.Add(new PredictedTurn
            {
                UniqueID = nextActor.ActorID + "_" + count,
                Name = nextActor.Name,
                IsEnemy = nextActor.IsEnemy,
                SimulatedAV = totalTimePassed
            });

            foreach (var node in simQueue)
            {
                node.CurrentAV -= timePassed;
            }

            nextActor.CurrentAV = InitialAV(nextActor.Speed);
        }

        return results;
    }
}
