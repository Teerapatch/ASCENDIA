using System.Collections.Generic;
using UnityEngine;

// ใช้แร่และดอกไม้ที่อาร์ตวางไว้ใน Cliff Prefab (ALL_Ores / ALL_Flowers) เป็นของเก็บจริง: สุ่มว่าชิ้นไหนโผล่ในชั้นนี้
// ชิ้นที่โผล่ได้ Pickup (ปุ่ม E, ไอเทม) จาก Prefab เดิมของ GemSpawner / HerbSpawner มาครอบ โดยซ่อนโมเดลของ Pickup แล้วใช้โมเดลของอาร์ตแทน
// ClimbableWall.Awake เป็นคนเพิ่มสคริปต์นี้ให้ไทล์เอง จึงไม่ต้องแก้ Prefab
public class CliffArtPickups : MonoBehaviour
{
    [Range(0f, 1f), Tooltip("โอกาสที่แร่แต่ละก้อนของอาร์ตจะโผล่ในชั้นนี้")]
    public float oreChance = .5f;
    [Range(0f, 1f), Tooltip("โอกาสที่ดอกไม้แต่ละต้นของอาร์ตจะโผล่ในชั้นนี้")]
    public float flowerChance = .5f;
    [Tooltip("จุดกดเก็บอยู่ห่างผิวหินเท่านี้")]
    public float wallOffset = .7f;
    [Tooltip("ของที่อยู่นอกเลนปีนเกินระยะนี้เอื้อมไม่ถึง จะไม่โผล่")]
    public float maxLaneShift = 6f;

    public void Setup(ClimbableWall wall)
    {
        List<GameObject> gems = null, herbs = null;
        foreach (OreSpawner spawner in GetComponentsInChildren<OreSpawner>(true))
        {
            if (spawner.name.IndexOf("Herb", System.StringComparison.OrdinalIgnoreCase) >= 0) herbs = spawner.spawnableItems;
            else gems = spawner.spawnableItems;
            spawner.enabled = false; // ไม่เสกของจากจุดเกิดเดิมซ้อนอีก
        }

        Physics.SyncTransforms();
        Transform ores = null, flowers = null;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "ALL_Ores") ores = child;
            else if (child.name == "ALL_Flowers") flowers = child;
        }
        Populate(wall, ores, gems, oreChance);
        Populate(wall, flowers, herbs, flowerChance);
    }

    private void Populate(ClimbableWall wall, Transform group, List<GameObject> prefabs, float chance)
    {
        if (group == null) return;
        group.gameObject.SetActive(true);
        if (prefabs == null) return;
        prefabs = prefabs.FindAll(p => p != null);
        if (prefabs.Count == 0) return; // ไม่มี Prefab ของเก็บ: ปล่อยเป็นของประดับ

        var pieces = new List<Transform>();
        foreach (Transform piece in group) pieces.Add(piece);

        Vector3 facing = wall.Facing;
        Transform parent = wall.bounds != null ? wall.bounds.transform : transform;
        foreach (Transform piece in pieces)
        {
            if (Random.value > chance) { piece.gameObject.SetActive(false); continue; }
            piece.gameObject.SetActive(true);

            // จุดกดเก็บ: กลางโมเดล ดึงเข้ามาในเลนปีน แล้วแนบผิวหิน
            Vector3 center = Center(piece);
            Vector3 position = wall.ClampPosition(center);
            if ((position - center).magnitude > maxLaneShift) { piece.gameObject.SetActive(false); continue; }
            if (ClimbableWall.Raycast(position - facing * 8f, facing, 24f, out RaycastHit hit)) position = hit.point - facing * wallOffset;

            GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
            GameObject pickup = Instantiate(prefab, position, prefab.transform.rotation, parent);
            Transform visual = pickup.transform.Find("Visual");
            if (visual != null) visual.gameObject.SetActive(false);
            // เก็บแล้ว Pickup ถูกลบ โมเดลของอาร์ตจึงหายไปด้วย
            piece.SetParent(pickup.transform, true);
        }
    }

    private static Vector3 Center(Transform piece)
    {
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>())
        {
            if (renderer is ParticleSystemRenderer) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found ? bounds.center : piece.position;
    }
}
