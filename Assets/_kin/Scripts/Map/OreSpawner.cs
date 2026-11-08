using UnityEngine;
using System.Collections.Generic;

public class OreSpawner : MonoBehaviour
{
    [Header("Gacha Settings (ของที่จะสุ่มเกิด)")]
    // 🌟 ใส่ Prefab ของที่เก็บได้หลายๆ แบบลงไปในนี้ (เช่น ก้อนแร่, หญ้า Herb, คริสตัล)
    public List<GameObject> spawnableItems; 
    
    [Range(0f, 1f)]
    public float spawnChance = 0.4f; // โอกาสเกิด 40%

    [Header("Wall Snap")]
    [Tooltip("วางของให้ติดผิวหินจริง (ClimbableWall) ตามแกน +Z ของ Spawner แทนตำแหน่งจุดเกิดตรงๆ")]
    public bool snapToWall = false;
    [Tooltip("ระยะห่างจากผิวหิน")]
    public float wallOffset = 0.7f;
    public float wallProbeBack = 8f;

    private void Start()
    {
        SpawnOres();
    }

    private void SpawnOres()
    {
        if (spawnableItems == null || spawnableItems.Count == 0) return;

        if (snapToWall) Physics.SyncTransforms();

        foreach (Transform spawnPoint in transform)
        {
            if (Random.value <= spawnChance)
            {
                // 🌟 สุ่มหยิบของ 1 อย่างจาก List มาเสก
                int randomIndex = Random.Range(0, spawnableItems.Count);
                GameObject selectedItem = spawnableItems[randomIndex];

                if (selectedItem != null)
                {
                    Vector3 position = spawnPoint.position;
                    if (snapToWall)
                    {
                        // MeshCollider ของหินถูกสร้างใน ClimbableWall.Awake ก่อน Start นี้แล้ว
                        Vector3 facing = transform.forward;
                        if (ClimbableWall.Raycast(position - facing * wallProbeBack, facing, wallProbeBack * 3f, out RaycastHit hit))
                        {
                            position = hit.point - facing * wallOffset;
                        }
                    }

                    // ใส่ไว้ใต้พ่อของ Spawner (ไม่ใช่ตัว Spawner เอง เพราะกำลังวนลูกอยู่): ของที่ไม่ได้เก็บจะหายไปพร้อมห้องของชั้นนี้
                    Instantiate(selectedItem, position, selectedItem.transform.rotation, transform.parent);
                }
            }
        }
    }
}