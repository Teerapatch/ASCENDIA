using UnityEngine;
using System.Collections.Generic;

public class NodeSpawner : MonoBehaviour
{
    [Header("Node Prefabs")]
    public GameObject normalNodePrefab;   // โหนดปีนเขาปกติ
    public GameObject battleNodePrefab;   // โหนดมอนสเตอร์ปกติ
    public GameObject miniBossNodePrefab; // 🌟 โหนดมินิบอส (สุ่มเกิดระหว่างทาง)
    public GameObject bossNodePrefab;     // 🌟 โหนดบอสใหญ่ (เกิดอันเดียวตอนจบแคมป์สุดท้าย)
    public GameObject oreNodePrefab;      // โหนดแร่
    public GameObject restNodePrefab;     // โหนดพักผ่อน
    public GameObject campNodePrefab;     // โหนดแคมป์ (เกิดอันเดียวตอนจบแคมป์ปกติ)

    [Header("Mini Boss Settings")]
    [Tooltip("ชั้นขั้นต่ำที่จะเริ่มมีโอกาสเจอ Mini Boss ในแต่ละช่วงแคมป์")]
    public int minFloorForMiniBoss = 3;

    [Header("Spawn Points")]
    public Transform leftPoint;
    public Transform centerPoint;
    public Transform rightPoint;

    private void Start()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerData == null) return;

        PlayerData data = GameManager.Instance.playerData;
        int currentFloor = data.currentFloor;
        int maxFloor = data.maxFloorBeforeCamp;

        // ==========================================================
        // 🌟 กรณีถึงชั้นสุดท้ายของช่วงแคมป์นั้นๆ (เสกแค่ 1 โหนดตรงกลาง!)
        // ==========================================================
        if (currentFloor >= maxFloor)
        {
            if (data.IsFinalBossStage)
            {
                // ถ้าเป็นแคมป์สุดท้าย -> เหลือแค่โหนด Boss อันเดียวตรงกลาง
                GameObject prefabToSpawn = bossNodePrefab != null ? bossNodePrefab : miniBossNodePrefab;
                if (prefabToSpawn != null && centerPoint != null)
                {
                    Instantiate(prefabToSpawn, centerPoint.position, centerPoint.rotation, transform);
                }
                Debug.Log($"💀 [Final Stage: Camp {data.currentCampLevel}/{data.finalCampLevel}] ชั้นที่ {currentFloor}: เสกโหนด BOSS เพียงอันเดียว!");
            }
            else
            {
                // ถ้าเป็นแคมป์ปกติ -> เหลือแค่โหนด Camp อันเดียวตรงกลาง
                if (campNodePrefab != null && centerPoint != null)
                {
                    Instantiate(campNodePrefab, centerPoint.position, centerPoint.rotation, transform);
                }
                Debug.Log($"🏕️ [Camp {data.currentCampLevel}/{data.finalCampLevel}] ชั้นที่ {currentFloor}: เสกโหนด CAMP เพียงอันเดียว!");
            }
        }
        // ==========================================================
        // 🌟 กรณีชั้นระหว่างทาง (ชั้น 1 ถึง maxFloor - 1) เสก 3 โหนด
        // ==========================================================
        else
        {
            List<GameObject> nodesToSpawn = new List<GameObject>();

            // 1. การันตีโหนดทางธรรมดา (Normal) 1 อัน
            nodesToSpawn.Add(normalNodePrefab);

            // 2. การันตีโหนดต่อสู้ (Battle) 1 อัน
            nodesToSpawn.Add(battleNodePrefab != null ? battleNodePrefab : normalNodePrefab);

            // 3. สุ่มโหนดที่ 3 จาก Pool (Normal, Ore, Rest, และ MiniBoss!)
            List<GameObject> bonusPool = new List<GameObject>();
            if (normalNodePrefab != null) bonusPool.Add(normalNodePrefab);
            if (oreNodePrefab != null) bonusPool.Add(oreNodePrefab);
            if (restNodePrefab != null) bonusPool.Add(restNodePrefab);

            // ถ้าปีนถึงชั้นที่กำหนด ให้เอา MiniBoss เข้าไปสุ่มใน Pool ด้วย
            if (currentFloor >= minFloorForMiniBoss && miniBossNodePrefab != null)
            {
                bonusPool.Add(miniBossNodePrefab);
            }

            GameObject thirdNode = bonusPool.Count > 0 
                ? bonusPool[Random.Range(0, bonusPool.Count)] 
                : normalNodePrefab;
            nodesToSpawn.Add(thirdNode);

            // สลับตำแหน่ง ซ้าย-กลาง-ขวา (Shuffle)
            for (int i = 0; i < nodesToSpawn.Count; i++)
            {
                GameObject temp = nodesToSpawn[i];
                int randomIndex = Random.Range(i, nodesToSpawn.Count);
                nodesToSpawn[i] = nodesToSpawn[randomIndex];
                nodesToSpawn[randomIndex] = temp;
            }

            // เสกโหนดทั้ง 3 จุด
            if (leftPoint && nodesToSpawn[0]) Instantiate(nodesToSpawn[0], leftPoint.position, leftPoint.rotation, transform);
            if (centerPoint && nodesToSpawn[1]) Instantiate(nodesToSpawn[1], centerPoint.position, centerPoint.rotation, transform);
            if (rightPoint && nodesToSpawn[2]) Instantiate(nodesToSpawn[2], rightPoint.position, rightPoint.rotation, transform);
            
            Debug.Log($"🧗 [Camp {data.currentCampLevel}/{data.finalCampLevel}] ชั้นที่ {currentFloor}/{maxFloor}: สุ่มได้ [{nodesToSpawn[0]?.name}, {nodesToSpawn[1]?.name}, {nodesToSpawn[2]?.name}]");
        }
    }
}