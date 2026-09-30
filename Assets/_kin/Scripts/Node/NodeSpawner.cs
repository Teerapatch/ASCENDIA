using UnityEngine;
using System.Collections.Generic;

public class NodeSpawner : MonoBehaviour
{
    [Header("Node Prefabs (ครบทั้ง 6 ประเภท)")]
    public GameObject normalNodePrefab;   // โหนดปีนเขาปกติ
    public GameObject battleNodePrefab;   // โหนดต่อสู้
    public GameObject miniBossNodePrefab; // โหนดบอส
    public GameObject oreNodePrefab;      // โหนดแร่
    public GameObject restNodePrefab;     // โหนดพักผ่อน/อัปเกรด
    public GameObject campNodePrefab;     // โหนดแคมป์

    [Header("Spawn Points (จุดวางโหนด)")]
    public Transform leftPoint;
    public Transform centerPoint;
    public Transform rightPoint;

    private void Start()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerData == null) return;

        int currentFloor = GameManager.Instance.playerData.currentFloor;
        int maxFloor = GameManager.Instance.playerData.maxFloorBeforeCamp;

        // 🏕️ ถ้าถึงชั้นสุดท้าย (เช่น ชั้น 10)
        if (currentFloor >= maxFloor)
        {
            // เสกโหนด Camp หรือ MiniBoss ตรงกลาง (ถ้าใส่ทั้งคู่ จะวางคู่กันให้เลือกเทสได้)
            if (campNodePrefab != null && centerPoint != null) 
            {
                Instantiate(campNodePrefab, centerPoint.position, centerPoint.rotation, transform);
            }
            if (miniBossNodePrefab != null && rightPoint != null)
            {
                Instantiate(miniBossNodePrefab, rightPoint.position, rightPoint.rotation, transform);
            }
            if (restNodePrefab != null && leftPoint != null)
            {
                Instantiate(restNodePrefab, leftPoint.position, leftPoint.rotation, transform);
            }
            Debug.Log("🏕️ ถึงชั้นสุดท้าย! เสกโหนดจบด่านที่ชั้น " + currentFloor);
        }
        else
        {
            // 🎲 ชั้น 1-9: จัดเซ็ตโหนด 3 ทางเลือก
            List<GameObject> nodesToSpawn = new List<GameObject>();

            // 1. การันตีโหนดปีนเขาปกติ (Normal) 1 อันเสมอ เพื่อไม่ให้ทางปีนตัน
            nodesToSpawn.Add(normalNodePrefab);

            // 2. การันตีโหนดต่อสู้ (Battle) 1 อัน (ถ้าไม่มีให้ใช้ Normal แทน)
            nodesToSpawn.Add(battleNodePrefab != null ? battleNodePrefab : normalNodePrefab);

            // 3. ช่องที่ 3 สุ่มระหว่าง Normal, Ore (แร่), หรือ Rest (พักผ่อน)
            List<GameObject> bonusPool = new List<GameObject>();
            if (normalNodePrefab != null) bonusPool.Add(normalNodePrefab);
            if (oreNodePrefab != null) bonusPool.Add(oreNodePrefab);
            if (restNodePrefab != null) bonusPool.Add(restNodePrefab);

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

            // วางลงจุด Spawn
            if (leftPoint && nodesToSpawn[0]) Instantiate(nodesToSpawn[0], leftPoint.position, leftPoint.rotation, transform);
            if (centerPoint && nodesToSpawn[1]) Instantiate(nodesToSpawn[1], centerPoint.position, centerPoint.rotation, transform);
            if (rightPoint && nodesToSpawn[2]) Instantiate(nodesToSpawn[2], rightPoint.position, rightPoint.rotation, transform);
            
            Debug.Log($"🎲 ชั้นที่ {currentFloor}: เสกโหนด [{nodesToSpawn[0]?.name}, {nodesToSpawn[1]?.name}, {nodesToSpawn[2]?.name}]");
        }
    }
}