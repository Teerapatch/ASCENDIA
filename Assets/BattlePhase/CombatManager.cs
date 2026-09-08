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
        public string Name;
        public bool IsEnemy;
        public float SimulatedAV; // ค่า AV รวมที่ใช้ไปตั้งแต่ปัจจุบันจนถึงเทิร์นนี้
    }

    // 2. คลาสชั่วคราวสำหรับใช้ทดลองคำนวณในฟังก์ชัน
    private class SimNode
    {
        public string Name;
        public bool IsEnemy;
        public float Speed;
        public float CurrentAV;
    }

    // คลาสย่อยสำหรับเก็บคิวเทิร์น
    public class TurnNode
    {
        public string Name;
        public float Speed;
        public float CurrentAV;
        public bool IsEnemy;
        public EnemyController EnemyRef;
        public WeaponData WeaponRef;
        // Reference ไปที่ Controller (คุณสามารถปรับเป็น Interface ได้)
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

        // 5. รีเซ็ต AV ของคนที่เพิ่งเล่นจบ กลับไปต่อท้ายคิว
        turnQueue[0].CurrentAV = 10000f / turnQueue[0].Speed;
        currentState = CombatState.CalculateTurn;
        NextTurn();
    }
    public void RemoveEnemy(EnemyController deadEnemy)
    {
        // 1. ลบออกจาก List บนฟิลด์
        enemies.Remove(deadEnemy);

        // 2. ลบออกจากคิวปกติ
        turnQueue.RemoveAll(node => node.IsEnemy && node.EnemyRef == deadEnemy);

        // 3. --- แก้ไขตรงนี้ ---
        // สั่งให้ระบบ "คาดเดาอนาคตใหม่ 8 เทิร์น" แล้วค่อยส่งไปให้ UI
        if (turnOrderUI != null)
        {
            List<PredictedTurn> predictions = PredictNextTurns(8);
            turnOrderUI.UpdateUI(predictions);
        }

        // 4. เช็คว่าศัตรูตายหมดหรือยัง
        if (enemies.Count == 0)
        {
            currentState = CombatState.GameOver;
            Debug.Log("VICTORY! All enemies defeated.");
        }
    }

    void InitializeCombat()
    {
        // 1. เอา "อาวุธแต่ละชิ้น" ของ Player เข้าคิว
        foreach (var weapon in player.inventoryWeapons)
        {
            if (weapon.currentDurability > 0)
            {
                turnQueue.Add(new TurnNode
                {
                    Name = weapon.weaponName,
                    Speed = weapon.speed,
                    IsEnemy = false,
                    CurrentAV = 10000f / weapon.speed,
                    WeaponRef = weapon
                });
            }
        }

        // 2. เอาศัตรูเข้าคิว
        foreach (var enemy in enemies)
        {
            turnQueue.Add(new TurnNode
            {
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

        turnQueue = turnQueue.OrderBy(x => x.CurrentAV).ToList();

        float avToSubtract = turnQueue[0].CurrentAV;
        foreach (var node in turnQueue) { node.CurrentAV -= avToSubtract; }

        // --- จุดที่เปลี่ยน ---
        // เปลี่ยนจากการส่ง turnQueue เพียวๆ ไปเป็นการส่งผลลัพธ์ที่จำลองล่วงหน้า 8 เทิร์น
        if (turnOrderUI != null)
        {
            List<PredictedTurn> predictions = PredictNextTurns(6);
            turnOrderUI.UpdateUI(predictions);
        }
        // ------------------

        TurnNode activeTurn = turnQueue[0];

        if (!activeTurn.IsEnemy)
        {
            currentState = CombatState.PlayerTurn;
            player.StartTurn(activeTurn.WeaponRef);
        }
        else
        {
            currentState = CombatState.EnemyTurn;
            activeTurn.EnemyRef.StartTurn();
        }
    }
    private List<PredictedTurn> PredictNextTurns(int amountToPredict)
    {
        List<PredictedTurn> results = new List<PredictedTurn>();
        List<SimNode> simQueue = new List<SimNode>();

        // 3.1 ก็อปปี้สถานะปัจจุบันทั้งหมดมาใส่คิวจำลอง (เพื่อไม่ให้ค่าจริงในเกมพัง)
        foreach (var node in turnQueue)
        {
            simQueue.Add(new SimNode
            {
                Name = node.Name,
                IsEnemy = node.IsEnemy,
                Speed = node.Speed,
                CurrentAV = node.CurrentAV
            });
        }

        float totalTimePassed = 0f;

        // 3.2 เริ่มจำลองเทิร์นล่วงหน้าตามจำนวนที่ต้องการ
        for (int i = 0; i < amountToPredict; i++)
        {
            // หาคนที่ AV น้อยที่สุดในขณะนั้น
            simQueue = simQueue.OrderBy(x => x.CurrentAV).ToList();
            SimNode nextActor = simQueue[0];

            float timePassed = nextActor.CurrentAV;
            totalTimePassed += timePassed;

            // บันทึกคนที่จะได้เล่นลงในผลลัพธ์
            results.Add(new PredictedTurn
            {
                Name = nextActor.Name,
                IsEnemy = nextActor.IsEnemy,
                SimulatedAV = totalTimePassed // ใช้โชว์ใน UI ให้เห็นว่าต้องรออีกเท่าไหร่
            });

            // หักลบเวลาของทุกคนในคิวจำลอง
            foreach (var node in simQueue)
            {
                node.CurrentAV -= timePassed;
            }

            // รีเซ็ต AV ของคนที่เพิ่งจำลองเสร็จ เพื่อให้เขากลับไปต่อท้ายคิวใหม่ในโลกจำลอง
            nextActor.CurrentAV = 10000f / nextActor.Speed;
        }

        return results;
    }

    //void EnemyAttackRoutine()
    //{
    //    // สมมติศัตรูเลือกท่าโจมตี ส่งทิศทางที่ต้อง Parry ไปให้ ParryManager
    //    currentState = CombatState.ParryPhase;
    //    Vector2 attackDir = new Vector2(1, 0); // ตัวอย่าง: ศัตรูฟันจากซ้ายไปขวา
    //    ParryManager.Instance.StartParryWindow(attackDir, 50 /* ดาเมจที่จะทำ */, this);
    //}
}