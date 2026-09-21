using UnityEngine;
using System.Collections.Generic; // เพิ่มเข้ามาเพื่อใช้งาน List

public class NodeSpawner : MonoBehaviour
{
    [Header("Node Prefabs")]
    public GameObject climbNodePrefab;
    public GameObject battleNodePrefab;
    public GameObject campNodePrefab;

    [Header("Spawn Points (จุดวางโหนด)")]
    public Transform leftPoint;
    public Transform centerPoint;
    public Transform rightPoint;

    private void Start()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerData == null) return;

        int currentFloor = GameManager.Instance.playerData.currentFloor;
        int maxFloor = GameManager.Instance.playerData.maxFloorBeforeCamp;

        if (currentFloor >= maxFloor)
        {
            // ถึงชั้นแคมป์ (ด่าน 10) เสกโหนดแคมป์อันเดียวตรงกลางเหมือนเดิม
            if (campNodePrefab && centerPoint) 
            {
                Instantiate(campNodePrefab, centerPoint.position, centerPoint.rotation, transform);
            }
        }
        else
        {
            // 🌟 1. เตรียมโหนดใส่กระเป๋า (บังคับว่าต้องมี ปีน 2 อัน, สู้ 1 อัน)
            List<GameObject> nodesToSpawn = new List<GameObject>();
            nodesToSpawn.Add(climbNodePrefab);
            nodesToSpawn.Add(climbNodePrefab);
            nodesToSpawn.Add(battleNodePrefab);

            // 🌟 2. สับเปลี่ยนตำแหน่งโหนดในกระเป๋า (Shuffle)
            for (int i = 0; i < nodesToSpawn.Count; i++)
            {
                GameObject temp = nodesToSpawn[i];
                int randomIndex = Random.Range(i, nodesToSpawn.Count);
                nodesToSpawn[i] = nodesToSpawn[randomIndex];
                nodesToSpawn[randomIndex] = temp;
            }

            // 🌟 3. หยิบโหนดที่สลับตำแหน่งแล้ว ไปวางตามจุด ซ้าย, กลาง, ขวา
            if (leftPoint) Instantiate(nodesToSpawn[0], leftPoint.position, leftPoint.rotation, transform);
            if (centerPoint) Instantiate(nodesToSpawn[1], centerPoint.position, centerPoint.rotation, transform);
            if (rightPoint) Instantiate(nodesToSpawn[2], rightPoint.position, rightPoint.rotation, transform);
            
            Debug.Log("🎲 เสกโหนด 3 อัน (ปีน 2, สู้ 1) ที่ชั้น " + currentFloor);
        }
    }
}