using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    [Header("Tile Settings")]
    public GameObject[] mountainPrefabs; // เอา Prefab หน้าผาแบบต่างๆ มาใส่ช่องนี้
    public float tileHeight = 20f; // ความสูงของ Prefab หน้าผา 1 บล็อก
    public int maxTilesOnScreen = 4; // โหลดรอไว้กี่บล็อก
    
    [Header("References")]
    public Transform playerTransform; // ลากตัวละครมาใส่

    private float spawnY = 0f; 
    private List<GameObject> activeTiles = new List<GameObject>();

    private void Start()
    {
        // สุ่มสร้างหน้าผาเรียงกันขึ้นไปรอไว้ก่อนตอนเริ่มเกม
        for (int i = 0; i < maxTilesOnScreen; i++)
        {
            SpawnTile();
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        // ถ้าระยะ Y (แบบ World Space) ของชิ้นล่างสุด มันต่ำกว่าผู้เล่นมากแล้ว ให้ลบชิ้นล่างสุดทิ้ง แล้วสุ่มสร้างชิ้นใหม่ต่อด้านบน
        if (activeTiles[0].transform.position.y + tileHeight < playerTransform.position.y - (tileHeight / 2))
        {
            DeleteTile();
            SpawnTile();
        }
    }

    private void SpawnTile()
    {
        // สุ่มหยิบแผนที่ 1 อัน
        int randomIndex = Random.Range(0, mountainPrefabs.Length);
        
        // สร้างขึ้นมาเป็นลูกของ EnvironmentContainer
        GameObject go = Instantiate(mountainPrefabs[randomIndex], transform);
        
        // ต่อด้านบนไปเรื่อยๆ (ใช้ Local Position เพราะมันเลื่อนไปพร้อม Container)
        go.transform.localPosition = new Vector3(0, spawnY, 0);
        spawnY += tileHeight;
        
        activeTiles.Add(go);
    }

    private void DeleteTile()
    {
        Destroy(activeTiles[0]);
        activeTiles.RemoveAt(0);
    }
}