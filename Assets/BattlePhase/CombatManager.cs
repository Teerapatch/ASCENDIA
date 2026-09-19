using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum CombatState { Setup, CalculateTurn, PlayerTurn, EnemyTurn, ParryPhase, GameOver }

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance;
    public CombatState currentState;

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

    private void Awake() { Instance = this; }

    private void Start()
    {
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

        if (enemies.Count == 0)
        {
            currentState = CombatState.GameOver;
            Debug.Log("VICTORY! All enemies defeated.");
        }
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
        if (currentState == CombatState.GameOver) return;

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

            activeTurn.EnemyRef.StartTurn();
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