using UnityEngine;
using System.Collections.Generic;

public class OreSpawner : MonoBehaviour
{
    [Header("Gacha Settings (ของที่จะสุ่มเกิด)")]
    // 🌟 ใส่ Prefab ของที่เก็บได้หลายๆ แบบลงไปในนี้ (เช่น ก้อนแร่, หญ้า Herb, คริสตัล)
    public List<GameObject> spawnableItems; 
    
    [Range(0f, 1f)]
    public float spawnChance = 0.4f; // โอกาสเกิด 40%

    private void Start()
    {
        SpawnOres();
    }

    private void SpawnOres()
    {
        if (spawnableItems == null || spawnableItems.Count == 0) return;

        GameObject container = GameObject.Find("EnvironmentContainer");

        foreach (Transform spawnPoint in transform)
        {
            if (Random.value <= spawnChance)
            {
                // 🌟 สุ่มหยิบของ 1 อย่างจาก List มาเสก
                int randomIndex = Random.Range(0, spawnableItems.Count);
                GameObject selectedItem = spawnableItems[randomIndex];

                if (selectedItem != null)
                {
                    GameObject spawnedObj = Instantiate(selectedItem, spawnPoint.position, selectedItem.transform.rotation);
                    
                    if (container != null)
                    {
                        spawnedObj.transform.SetParent(container.transform, true);
                    }
                }
            }
        }
    }
}