using System.Collections.Generic;
using UnityEngine;

public class SpellOrbManager : MonoBehaviour
{
    public static SpellOrbManager Instance;

    [Header("Spawner Settings")]
    public GameObject orbPrefab; // Prefab ที่ติดสคริปต์ FloatingElementOrb
    public Transform spawnAreaCenter; // จุดศูนย์กลางพื้นที่สุ่ม (เช่น กลางสนามประลอง)
    public float spawnRadius = 4f;    // รัศมีความกว้างในการสุ่ม
    public float heightMin = 1.5f;    // ความสูงต่ำสุดจากพื้น
    public float heightMax = 3.5f;    // ความสูงสูงสุดจากพื้น

    private List<GameObject> activeOrbs = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// สุ่มสร้าง Orb ธาตุ 3 ลูกใหม่ทุกครั้งที่เริ่มเทิร์นของ Spell
    /// </summary>
    public void SpawnSpellOrbs()
    {
        ClearOrbs();

        if (orbPrefab == null || spawnAreaCenter == null)
        {
            Debug.LogWarning("SpellOrbManager: Missing OrbPrefab or SpawnAreaCenter!");
            return;
        }

        ElementType[] allElements = { ElementType.Fire, ElementType.Water, ElementType.Wind, ElementType.Earth };

        for (int i = 0; i < 3; i++)
        {
            // สุ่มตำแหน่งรอบๆ จุดศูนย์กลาง
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            float randomHeight = Random.Range(heightMin, heightMax);
            Vector3 spawnPos = spawnAreaCenter.position + new Vector3(randomCircle.x, randomHeight, randomCircle.y);

            GameObject orbObj = Instantiate(orbPrefab, spawnPos, Quaternion.identity);
            FloatingElementOrb orb = orbObj.GetComponent<FloatingElementOrb>();

            if (orb != null)
            {
                orb.Initialize(spawnPos);

                // สุ่มประเภทธาตุ
                ElementType randomElem = allElements[Random.Range(0, allElements.Length)];
                orb.elementType = randomElem;

                // เปลื่ยนสี Material ตามธาตุ
                MeshRenderer mr = orbObj.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.material.color = GetElementColor(randomElem);
                }
            }

            activeOrbs.Add(orbObj);
        }

        // สร้างเสร็จให้ซ่อนไว้ก่อน (รอจนกว่าผู้เล่นจะกดคลิกขวา Free-Aim)
        ToggleOrbsVisibility(false);
        Debug.Log("<color=magenta>🔮 Spell Turn Started: 3 Elemental Orbs generated in the air.</color>");
    }

    /// <summary>
    /// สั่งเปิด/ปิดการมองเห็นของ Orb ทั้งหมดในฉาก
    /// </summary>
    public void ToggleOrbsVisibility(bool isVisible)
    {
        foreach (var orbObj in activeOrbs)
        {
            if (orbObj != null)
            {
                FloatingElementOrb orb = orbObj.GetComponent<FloatingElementOrb>();
                if (orb != null)
                {
                    orb.SetVisible(isVisible);
                }
            }
        }
    }

    public void ClearOrbs()
    {
        foreach (var orbObj in activeOrbs)
        {
            if (orbObj != null) Destroy(orbObj);
        }
        activeOrbs.Clear();
    }

    private Color GetElementColor(ElementType type)
    {
        switch (type)
        {
            case ElementType.Fire: return Color.red;
            case ElementType.Water: return Color.blue;
            case ElementType.Wind: return Color.green;
            case ElementType.Earth: return Color.yellow;
            default: return Color.white;
        }
    }
}