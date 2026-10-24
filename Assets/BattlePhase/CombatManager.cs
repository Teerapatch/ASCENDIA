using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CombatState { Setup, CalculateTurn, PlayerTurn, EnemyTurn, ParryPhase, GameOver, Victory }

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance;

    // แจ้งเตือนทุกระบบที่สนใจ (UI จบเกม, BattleSession) ว่าการต่อสู้จบแล้ว
    public static event System.Action<BattleOutcome> OnBattleEnded;

    // AV (Action Value) = ActionGauge / Speed  ใคร AV น้อยสุดได้เล่นก่อน
    private const float ActionGauge = 10000f;
    private const int PredictedTurnCount = 6;

    public CombatState currentState;
    public bool isTransitioningWave = false;

    public bool IsBattleOver => currentState == CombatState.GameOver || currentState == CombatState.Victory;

    // เพิ่มขึ้นทุกครั้งที่เริ่มเทิร์นใหม่ ใช้เช็กว่า Coroutine ที่ค้างอยู่ยังเป็นของเทิร์นเดิมไหม
    public int TurnNumber { get; private set; }

    public EncounterData CurrentEncounter { get; private set; }

    [Header("Enemy Spawning (ใช้เมื่อเปิดฉากนี้ตรงๆ ไม่ได้ถูกเรียกผ่าน BattleSession)")]
    public List<GameObject> enemyPrefabsToSpawn;
    public List<Transform> enemySpawnPoints;

    [Header("Wave Settings (Optional)")]
    public List<EnemyWave> enemyWaves = new List<EnemyWave>();

    // Wave ทั้งหมดของการต่อสู้นี้ (index 0 = Wave แรก)
    private List<EnemyWave> activeWaves;
    private int currentWaveIndex = 0;

    [Header("Combatants (Player & Enemies)")]
    public PlayerCombatController player;
    public List<EnemyController> enemies;
    public TurnOrderUI turnOrderUI;

    // 1. เพิ่ม Class สำหรับเก็บผลลัพธ์การคาดเดาเทิร์น
    public class PredictedTurn
    {
        public string UniqueID;
        public string Name;
        public bool IsEnemy;
        public float SimulatedAV;
    }

    // 2. คลาสชั่วคราวสำหรับใช้ทดลองคำนวณในฟังก์ชัน
    private class SimNode
    {
        public string ActorID;
        public string Name;
        public bool IsEnemy;
        public float Speed;
        public float CurrentAV;
    }

    // คลาสย่อยสำหรับเก็บคิวเทิร์น
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
        BuildWaves();

        if (activeWaves.Count == 0)
        {
            Debug.LogError("CombatManager: ไม่มีศัตรูให้เสก (Encounter และ enemyPrefabsToSpawn ว่างทั้งคู่)");
            return;
        }

        currentWaveIndex = 1;
        StartEncounter(activeWaves[0].enemiesInWave);
    }

    // ถ้าถูกเรียกผ่าน BattleSession ให้ใช้ Encounter ที่ส่งมา ไม่งั้นใช้ค่าที่ตั้งไว้ในฉาก
    private void BuildWaves()
    {
        CurrentEncounter = BattleSession.Current != null ? BattleSession.Current.encounter : null;

        if (CurrentEncounter != null && CurrentEncounter.waves.Count > 0)
        {
            activeWaves = new List<EnemyWave>(CurrentEncounter.waves);
            return;
        }

        activeWaves = new List<EnemyWave>();
        if (enemyPrefabsToSpawn != null && enemyPrefabsToSpawn.Count > 0)
        {
            activeWaves.Add(new EnemyWave { waveName = "Wave 1", enemiesInWave = enemyPrefabsToSpawn });
        }
        activeWaves.AddRange(enemyWaves);
    }

    public void CheckBattleEnd()
    {
        if (IsBattleOver) return;

        // 1. ถ้าอาวุธพังหมด = เกมโอเวอร์
        if (!player.inventoryWeapons.Any(w => w.currentDurability > 0))
        {
            EndBattle(BattleOutcome.Defeat);
            return;
        }

        if (enemies.Count > 0) return;

        // 2. ศัตรูตายหมดฉาก -> ยังมี Wave เหลือไหม?
        if (currentWaveIndex < activeWaves.Count)
        {
            isTransitioningWave = true;
            Debug.Log($"<color=yellow>Wave {currentWaveIndex} Cleared! Spawning Next Wave...</color>");

            if (TurnAnnouncementUI.Instance != null)
            {
                TurnAnnouncementUI.Instance.AnnounceTurn($"<color=#FFDD55>{activeWaves[currentWaveIndex].waveName}</color>\nApproaching!", SpawnNextWave);
            }
            else
            {
                SpawnNextWave();
            }
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
        isTransitioningWave = false;

        if (outcome == BattleOutcome.Victory)
            Debug.Log("<color=green>BATTLE WON! All Waves Cleared.</color>");
        else
            Debug.Log("<color=red>GAME OVER! All weapons broken.</color>");

        if (SpellOrbManager.Instance != null) SpellOrbManager.Instance.ClearOrbs();

        OnBattleEnded?.Invoke(outcome);
    }

    private void SpawnNextWave()
    {
        EnemyWave nextWave = activeWaves[currentWaveIndex];
        currentWaveIndex++;

        SpawnWave(nextWave.enemiesInWave);

        // เริ่มวงจร Turn Order ใหม่อีกครั้ง (รวมผู้เล่นและศัตรูใหม่เข้าด้วยกัน)
        BeginRound();
    }

    // ==========================================
    // Spawning System
    // ==========================================
    public void StartEncounter(List<GameObject> enemiesToSpawn)
    {
        enemies.Clear();
        SpawnWave(enemiesToSpawn);

        Debug.Log($"<color=green>Encounter Started! Spawned {enemies.Count} enemies.</color>");

        BeginRound();
    }

    private void SpawnWave(List<GameObject> enemyPrefabs)
    {
        int spawnCount = Mathf.Min(enemyPrefabs.Count, enemySpawnPoints.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnEnemy(enemyPrefabs[i], enemySpawnPoints[i]);
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

            // ให้ลูกน้องมีค่า AV เริ่มต้นเท่ากับคนอื่นๆ เพื่อให้แทรกเข้าคิวได้สมูท
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

        // ค้นหาจุดเกิดที่ไม่มีศัตรูยืนทับอยู่ (เช็กคร่าวๆ จากตำแหน่ง)
        foreach (var sp in enemySpawnPoints)
        {
            bool isOccupied = enemies.Any(enemy => enemy != null && Vector3.Distance(enemy.transform.position, sp.position) < 1.0f);
            if (!isOccupied) return sp;
        }

        // ถ้าเต็มหมดแล้ว ให้สุ่มจุดเกิดสำรองไปก่อน
        return enemySpawnPoints[Random.Range(0, enemySpawnPoints.Count)];
    }

    // ==========================================
    // Turn Queue
    // ==========================================
    private static float InitialAV(float speed)
    {
        return ActionGauge / Mathf.Max(1f, speed);
    }

    private TurnNode CreateWeaponNode(WeaponData weapon)
    {
        return new TurnNode
        {
            ActorID = "W_" + weapon.GetInstanceID(),
            Name = weapon.weaponName,
            Speed = weapon.speed,
            IsEnemy = false,
            CurrentAV = InitialAV(weapon.speed),
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

    // สร้างคิวใหม่ทั้งหมด (อาวุธที่ยังไม่พัง + ศัตรูในฉาก) แล้วเริ่มเทิร์นแรก
    private void BeginRound()
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

        isTransitioningWave = false;
        currentState = CombatState.CalculateTurn;
        NextTurn();
    }

    private void RefreshTurnOrderUI()
    {
        if (turnOrderUI != null) turnOrderUI.UpdateUI(PredictNextTurns(PredictedTurnCount));
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

        // ถ้าคิวไม่มีอาวุธของผู้เล่นเหลืออยู่เลย = แพ้
        if (!turnQueue.Any(node => !node.IsEnemy))
        {
            EndBattle(BattleOutcome.Defeat);
        }
        else if (player != null && player.ActiveWeapon == brokenWeapon)
        {
            // ถ้าอาวุธที่พังคือชิ้นที่ผู้เล่นกำลังถืออยู่ ให้บังคับสลับอาวุธ
            player.Command_SwitchWeapon();
        }
    }

    public void NextTurn()
    {
        if (isTransitioningWave || IsBattleOver) return;

        var brokenWeapons = turnQueue
            .Where(node => !node.IsEnemy && node.WeaponRef != null && node.WeaponRef.currentDurability <= 0)
            .Select(node => node.WeaponRef)
            .ToList(); // ToList เพื่อก็อปปี้ออกมาก่อนทำการเตะออก

        foreach (var weapon in brokenWeapons)
        {
            RemoveWeapon(weapon);
        }

        // ถ้าเตะอาวุธออกหมดจน Game Over แล้ว ให้หยุดการทำงานทันที
        if (IsBattleOver || turnQueue.Count == 0) return;

        turnQueue = turnQueue.OrderBy(x => x.CurrentAV).ToList();

        float avToSubtract = turnQueue[0].CurrentAV;
        foreach (var node in turnQueue) { node.CurrentAV -= avToSubtract; }

        RefreshTurnOrderUI();

        TurnNode activeTurn = turnQueue[0];
        TurnNumber++;

        if (!activeTurn.IsEnemy)
        {
            currentState = CombatState.PlayerTurn;
            player.StartTurn(activeTurn.WeaponRef);
        }
        else
        {
            currentState = CombatState.EnemyTurn;

            if (WeaponUIManager.Instance != null)
            {
                WeaponUIManager.Instance.UpdateWeaponUI(player.inventoryWeapons, player.ActiveWeapon);
            }

            if (TurnAnnouncementUI.Instance != null)
            {
                // ใช้สีแดงหรือสีส้มให้ดูอันตราย
                string warningMsg = $"<color=#FF5555>{activeTurn.Name}</color>\nIs Attacking!";

                // ศัตรูจะเริ่มตี "หลังจาก" ป้ายประกาศเฟดหายไปแล้วเท่านั้น
                TurnAnnouncementUI.Instance.AnnounceTurn(warningMsg, activeTurn.EnemyRef.StartTurn);
            }
            else
            {
                activeTurn.EnemyRef.StartTurn();
            }
        }
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

            if (!occurrenceCounts.ContainsKey(nextActor.ActorID)) occurrenceCounts[nextActor.ActorID] = 0;
            occurrenceCounts[nextActor.ActorID]++;
            string uniqueTurnID = nextActor.ActorID + "_" + occurrenceCounts[nextActor.ActorID];

            results.Add(new PredictedTurn
            {
                UniqueID = uniqueTurnID,
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
