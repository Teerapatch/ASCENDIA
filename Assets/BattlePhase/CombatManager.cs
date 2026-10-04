using System.Collections.Generic;
using System.Linq;
using UnityEditor.Overlays;
using UnityEngine;

public enum CombatState { Setup, CalculateTurn, PlayerTurn, EnemyTurn, ParryPhase, GameOver, Victory }
public enum EncounterType { Standard, Standard_Fly, Boss, Ambush }

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance;
    public CombatState currentState;
    public bool isTransitioningWave = false;

    [System.Serializable]
    public class WaveData
    {
        public string waveName = "Wave 1";
        public List<GameObject> enemiesInWave;
    }

    [Header("Enemy Spawning")]
    public List<GameObject> enemyPrefabsToSpawn;
    public List<Transform> enemySpawnPoints;

    [Header("Wave Settings (Optional)")]
    public List<WaveData> enemyWaves = new List<WaveData>();
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
        //InitializeCombat();
        currentWaveIndex = 0;
        StartEncounter(enemyPrefabsToSpawn);
    }

    public void CheckBattleEnd()
    {
        // 1. ถ้าผู้เล่นตาย = เกมโอเวอร์ (เงื่อนไขเดิมของคุณ)
        if (player.inventoryWeapons.TrueForAll(w => w.currentDurability <= 0))
        {
            currentState = CombatState.GameOver;
            Debug.Log("<color=red>GAME OVER! All weapons broken.</color>");
            return;
        }

        // 2. ถ้าศัตรูตายหมดฉาก
        if (enemies.Count == 0)
        {
            // --- [เพิ่มใหม่] เช็กว่ายังมี Wave เหลือไหม? ---
            if (currentWaveIndex < enemyWaves.Count)
            {
                isTransitioningWave = true;
                Debug.Log($"<color=yellow>Wave {currentWaveIndex + 1} Cleared! Spawning Next Wave...</color>");

                // สั่งให้มี UI ประกาศสั้นๆ (ถ้ามี TurnAnnouncementUI)
                if (TurnAnnouncementUI.Instance != null)
                {
                    TurnAnnouncementUI.Instance.AnnounceTurn($"<color=#FFDD55>{enemyWaves[currentWaveIndex].waveName}</color>\nApproaching!", () =>
                    {
                        SpawnNextWave();
                    });
                }
                else
                {
                    SpawnNextWave();
                }
            }
            else
            {
                // ถ้าไม่มี Wave แล้ว ถือว่าจบฉากต่อสู้จริงๆ (Victory!)
                currentState = CombatState.GameOver;
                Debug.Log("<color=green>BATTLE WON! All Waves Cleared.</color>");

                // ... (ใส่โค้ดแจก EXP / โหลดกลับฉากแผนที่ ตรงนี้) ...
            }
        }
    }
    private void SpawnNextWave()
    {
        // 1. ดึงข้อมูลศัตรูชุดใหม่มา
        List<GameObject> nextEnemies = enemyWaves[currentWaveIndex].enemiesInWave;
        currentWaveIndex++;

        // 2. เสกศัตรูลงบนจุดเกิด (ใช้ลอจิกเดียวกับ StartEncounter)
        int spawnCount = Mathf.Min(nextEnemies.Count, enemySpawnPoints.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            GameObject enemyObj = Instantiate(nextEnemies[i], enemySpawnPoints[i].position, enemySpawnPoints[i].rotation);
            EnemyController controller = enemyObj.GetComponent<EnemyController>();
            if (controller != null) enemies.Add(controller);
        }

        // 3. เริ่มต้นวงจร Turn Order ใหม่อีกครั้ง (รวมผู้เล่นและศัตรูใหม่เข้าด้วยกัน)
        RecalculateTurnOrderForNewWave();
    }

    private void RecalculateTurnOrderForNewWave()
    {
        // 1. ล้างคิวการต่อสู้เก่าทิ้งทั้งหมด (ป้องกันบั๊กตัวละครเก่าตกค้าง)
        turnQueue.Clear();

        // 2. ดึงอาวุธผู้เล่นที่ยัง "ไม่พัง" กลับเข้าคิว
        foreach (var weapon in player.inventoryWeapons)
        {
            if (weapon.currentDurability > 0)
            {
                TurnNode playerNode = new TurnNode();

                playerNode.ActorID = "W_" + weapon.GetInstanceID();
                playerNode.Speed = weapon.speed;

                playerNode.IsEnemy = false;
                playerNode.WeaponRef = weapon;
                playerNode.Name = weapon.weaponName;

                // คำนวณ AV (Action Value) เริ่มต้น 
                // สมมติสูตร: 10000 / ความเร็ว
                playerNode.CurrentAV = 10000f / weapon.speed;

                turnQueue.Add(playerNode);
            }
        }

        // 3. ดึงศัตรูชุดใหม่ (Wave ปัจจุบัน) เข้าคิว
        foreach (var enemy in enemies)
        {
            TurnNode enemyNode = new TurnNode();

            enemyNode.ActorID = "E_" + enemy.GetInstanceID();
            enemyNode.Speed = enemy.baseSpeed;

            enemyNode.IsEnemy = true;
            enemyNode.EnemyRef = enemy;
            enemyNode.Name = enemy.enemyName;

            // คำนวณ AV เริ่มต้นของศัตรู
            enemyNode.CurrentAV = 10000f / enemy.baseSpeed;

            turnQueue.Add(enemyNode);
        }

        // 4. เรียงลำดับคิวจาก AV น้อยไปมาก (ใคร AV น้อยสุดได้ตีก่อน)
        turnQueue.Sort((a, b) => a.CurrentAV.CompareTo(b.CurrentAV));

        // 5. สร้างลิสต์จำลองเพื่อส่งให้ TurnOrderUI ของคุณ
        UpdateTurnOrderUI();

        // 6. บังคับเริ่มเทิร์นแรกของ Wave นี้ทันที!
        isTransitioningWave = false;
        NextTurn();
    }
    public void UpdateTurnOrderUI()
    {
        if (turnOrderUI == null) return;

        List<PredictedTurn> predictions = new List<PredictedTurn>();

        for (int i = 0; i < turnQueue.Count; i++)
        {
            PredictedTurn pt = new PredictedTurn();
            pt.Name = turnQueue[i].Name;
            pt.IsEnemy = turnQueue[i].IsEnemy;

            // --- [สำคัญ] วิธีตั้ง UniqueID ให้ไม่ซ้ำกันแม้จะเป็นศัตรูประเภทเดียวกัน ---
            if (turnQueue[i].IsEnemy)
            {
                // ใช้ GetInstanceID() ซึ่งจะการันตีว่าศัตรูที่เกิดใหม่จะมี ID ไม่ซ้ำกับตัวที่ตายไปแล้วแน่นอน
                pt.UniqueID = "Enemy_" + turnQueue[i].EnemyRef.gameObject.GetInstanceID().ToString();
                // pt.Icon = turnQueue[i].EnemyRef.enemyIcon; // สมมติว่ามีรูปศัตรู
            }
            else
            {
                // อาวุธผู้เล่น ใช้ชื่ออาวุธได้เลย เพราะมีชิ้นเดียวในกระเป๋า
                pt.UniqueID = "Player_" + turnQueue[i].WeaponRef.weaponName;
                // pt.Icon = turnQueue[i].WeaponRef.weaponIcon;
            }

            predictions.Add(pt);
        }

        // เรียกใช้ฟังก์ชัน UpdateUI ที่คุณเขียนมา
        turnOrderUI.UpdateUI(predictions);
    }

    // ==========================================
    // Spawning System
    // ==========================================
    public void StartEncounter(List<GameObject> enemiesToSpawn)
    {
        // เคลียร์ลิสต์เก่าเผื่อมี
        enemies.Clear();

        int spawnCount = Mathf.Min(enemiesToSpawn.Count, enemySpawnPoints.Count);

        for (int i = 0; i < spawnCount; i++)
        {
            // 1. เสกศัตรู (Instantiate) ลงบนจุดเกิด
            GameObject enemyObj = Instantiate(enemiesToSpawn[i], enemySpawnPoints[i].position, enemySpawnPoints[i].rotation);

            // 2. ดึงสคริปต์ Controller มาเก็บไว้ในลิสต์หลัก
            EnemyController controller = enemyObj.GetComponent<EnemyController>();
            if (controller != null)
            {
                enemies.Add(controller);
            }
            else
            {
                Debug.LogError($"Prefab {enemyObj.name} ไม่มีสคริปต์ EnemyController!");
            }
        }

        Debug.Log($"<color=green>Encounter Started! Spawned {enemies.Count} enemies.</color>");

        // 3. เริ่มการคำนวณเทิร์น (โค้ดเดิมของคุณ)
        InitializeCombat();
    }

    public void EndCurrentTurn()
    {
        if (currentState == CombatState.GameOver) return;

        // --- [แก้ไขเล็กน้อย] เชฟกันเหนียว เช็คให้ชัวร์ว่าคิวยังมีข้อมูลก่อนเซ็ตค่า ---
        if (turnQueue.Count > 0)
        {
            // 5. รีเซ็ต AV ของคนที่เพิ่งเล่นจบ กลับไปต่อท้ายคิว
            turnQueue[0].CurrentAV = 10000f / turnQueue[0].Speed;
        }

        currentState = CombatState.CalculateTurn;
        NextTurn();
    }

    public void RemoveEnemy(EnemyController deadEnemy)
    {
        enemies.Remove(deadEnemy);
        turnQueue.RemoveAll(node => node.IsEnemy && node.EnemyRef == deadEnemy);

        if (turnOrderUI != null)
        {
            List<PredictedTurn> predictions = PredictNextTurns(6);
            turnOrderUI.UpdateUI(predictions);
        }

        //if (enemies.Count == 0)
        //{
        //    currentState = CombatState.GameOver;
        //    Debug.Log("VICTORY! All enemies defeated.");
        //}
    }

    public void RemoveWeapon(WeaponData brokenWeapon)
    {
        Debug.Log($"<color=red>Timeline: ถอด {brokenWeapon.weaponName} ออกจากคิว!</color>");

        // 1. ลบอาวุธชิ้นนี้ออกจากคิวปกติ
        turnQueue.RemoveAll(node => !node.IsEnemy && node.WeaponRef == brokenWeapon);

        // 2. สั่งคาดเดาเทิร์นใหม่ และอัปเดต UI ทันที
        if (turnOrderUI != null)
        {
            List<PredictedTurn> predictions = PredictNextTurns(6);
            turnOrderUI.UpdateUI(predictions);
        }

        // 3. เช็ค Game Over (ถ้าคิวไม่มีอาวุธของผู้เล่นเหลืออยู่เลย = แพ้)
        bool hasWeaponsLeft = turnQueue.Any(node => !node.IsEnemy);
        if (!hasWeaponsLeft)
        {
            currentState = CombatState.GameOver;
            Debug.Log("DEFEAT! All weapons broken. Game Over.");
        }
        else
        {
            // --- [เพิ่มใหม่] ถ้าอาวุธที่พังคือชิ้นที่ผู้เล่นกำลังถืออยู่ ให้บังคับสลับอาวุธ ---
            if (player != null && player.ActiveWeapon == brokenWeapon)
            {
                player.Command_SwitchWeapon();
            }
        }
    }

    void InitializeCombat()
    {
        foreach (var weapon in player.inventoryWeapons)
        {
            if (weapon.currentDurability > 0)
            {
                turnQueue.Add(new TurnNode
                {
                    ActorID = "W_" + weapon.GetInstanceID(),
                    Name = weapon.weaponName,
                    Speed = weapon.speed,
                    IsEnemy = false,
                    CurrentAV = 10000f / weapon.speed,
                    WeaponRef = weapon
                });
            }
        }

        foreach (var enemy in enemies)
        {
            turnQueue.Add(new TurnNode
            {
                ActorID = "E_" + enemy.GetInstanceID(),
                Name = enemy.enemyName,
                Speed = enemy.baseSpeed,
                IsEnemy = true,
                CurrentAV = 10000f / enemy.baseSpeed,
                EnemyRef = enemy
            });
        }

        currentState = CombatState.CalculateTurn;
        NextTurn();
    }

    public void NextTurn()
    {
        if (isTransitioningWave || currentState == CombatState.GameOver || currentState == CombatState.Victory)
        {
            return;
        }

        var brokenWeapons = turnQueue
            .Where(node => !node.IsEnemy && node.WeaponRef != null && node.WeaponRef.currentDurability <= 0)
            .Select(node => node.WeaponRef)
            .ToList(); // ToList เพื่อก็อปปี้ออกมาก่อนทำการเตะออก

        foreach (var weapon in brokenWeapons)
        {
            RemoveWeapon(weapon);
        }

        // ถ้าเตะอาวุธออกหมดจน Game Over แล้ว ให้หยุดการทำงานทันที
        if (currentState == CombatState.GameOver) return;
        // =========================================================

        turnQueue = turnQueue.OrderBy(x => x.CurrentAV).ToList();

        float avToSubtract = turnQueue[0].CurrentAV;
        foreach (var node in turnQueue) { node.CurrentAV -= avToSubtract; }

        if (turnOrderUI != null)
        {
            List<PredictedTurn> predictions = PredictNextTurns(6);
            turnOrderUI.UpdateUI(predictions);
        }

        if (turnQueue == null || turnQueue.Count == 0) return;
        TurnNode activeTurn = turnQueue[0];

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

                TurnAnnouncementUI.Instance.AnnounceTurn(warningMsg, () =>
                {
                    // โค้ดส่วนนี้จะทำงาน "หลังจาก" ป้ายประกาศเฟดหายไปแล้วเท่านั้น
                    activeTurn.EnemyRef.StartTurn();
                });
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
        List<SimNode> simQueue = new List<SimNode>();

        foreach (var node in turnQueue)
        {
            simQueue.Add(new SimNode
            {
                ActorID = node.ActorID,
                Name = node.Name,
                IsEnemy = node.IsEnemy,
                Speed = node.Speed,
                CurrentAV = node.CurrentAV
            });
        }

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

            nextActor.CurrentAV = 10000f / nextActor.Speed;
        }

        return results;
    }
}