using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    [Header("Tile Settings")]
    public GameObject[] mountainPrefabs; 
    public GameObject topMountainPrefab; 
    
    public float tileHeight = 20f; 
    public int maxTilesOnScreen = 4; 
    
    [Header("Room Progression")]
    public int totalTilesPerRoom = 10; 

    private Transform playerTransform; 
    private float spawnY = 0f; 
    private int tilesSpawned = 0; 
    private List<GameObject> activeTiles = new List<GameObject>();

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        for (int i = 0; i < maxTilesOnScreen; i++)
        {
            if (tilesSpawned < totalTilesPerRoom) SpawnTile();
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        if (activeTiles.Count > 0 && activeTiles[0] != null && activeTiles[0].transform.position.y + tileHeight < playerTransform.position.y - (tileHeight / 2))
        {
            DeleteTile();
            if (tilesSpawned < totalTilesPerRoom) SpawnTile();
        }
    }

    private void SpawnTile()
    {
        GameObject tileToSpawn;
        bool isTopTile = false;

        if (tilesSpawned == totalTilesPerRoom - 1 && topMountainPrefab != null)
        {
            tileToSpawn = topMountainPrefab;
            isTopTile = true;
        }
        else
        {
            tileToSpawn = mountainPrefabs[Random.Range(0, mountainPrefabs.Length)];
        }
        
        GameObject go = Instantiate(tileToSpawn, transform);
        go.transform.localPosition = new Vector3(0, spawnY, 0);
        spawnY += tileHeight;
        
        activeTiles.Add(go);
        tilesSpawned++; 

        // *** ส่วนที่เพิ่มมา: ถ้าเป็นยอดเขา ให้เปิดใช้งาน Trigger ***
        if (isTopTile)
        {
            Debug.Log("🏔️ ถึงยอดเขาแล้ว! เตรียมตัวเลือกโหนด...");
        }
    }

    private void DeleteTile()
    {
        if (activeTiles.Count > 0)
        {
            Destroy(activeTiles[0]);
            activeTiles.RemoveAt(0);
        }
    }
}