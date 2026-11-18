using System.Collections.Generic;
using UnityEngine;

// ลูกแก้วธาตุของ Spell: เกิด 3 ลูกตอนเริ่มเทิร์นของ Spell ซ่อนไว้จนผู้เล่นคลิกขวาค้าง (Free Aim) แล้วคลิกซ้ายเก็บได้ 1 ลูกต่อเทิร์น
public class SpellOrbManager : MonoBehaviour
{
    public static SpellOrbManager Instance;

    [Header("Spawner Settings")]
    public GameObject orbPrefab; // Prefab ที่ติดสคริปต์ FloatingElementOrb
    [Tooltip("ใช้เมื่อปิด Spawn Between Fighters เท่านั้น")]
    public Transform spawnAreaCenter;
    public float spawnRadius = 4f;
    public float heightMin = 1.5f;
    public float heightMax = 3.5f;

    [Header("วางลูกแก้วระหว่างผู้เล่นกับศัตรู (มองเห็นจากกล้องเสมอ)")]
    public bool spawnBetweenFighters = true;
    [Tooltip("ความสูงจากพื้นของลูกแก้ว (หน่วยโลกของฉากต่อสู้)")]
    public Vector2 heightAboveGround = new Vector2(4f, 9f);
    [Tooltip("กระจายซ้าย-ขวาตามแนวกล้องได้กี่หน่วย")]
    public float sideSpread = 9f;
    [Tooltip("ลูกแก้วห่างกันอย่างน้อยกี่หน่วย")]
    public float minSeparation = 3f;
    [Tooltip("ขนาดลูกแก้วในโลก")]
    public float orbWorldSize = 1.4f;

    [Header("การเล็งเก็บ")]
    [Tooltip("เมาส์ห่างจากลูกแก้วบนจอได้กี่ส่วนของความสูงจอ")]
    public float pickRadius = .06f;
    public float hoverScale = 1.35f;

    private readonly List<GameObject> activeOrbs = new List<GameObject>();

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

        if (orbPrefab == null || (!spawnBetweenFighters && spawnAreaCenter == null))
        {
            Debug.LogWarning("SpellOrbManager: Missing OrbPrefab or SpawnAreaCenter!");
            return;
        }

        var placed = new List<Vector3>();
        for (int i = 0; i < 3; i++)
        {
            Vector3 spawnPos = PickPosition(placed);
            placed.Add(spawnPos);

            GameObject orbObj = Instantiate(orbPrefab, spawnPos, Quaternion.identity);
            if (spawnBetweenFighters) orbObj.transform.localScale = Vector3.one * orbWorldSize;
            FloatingElementOrb orb = orbObj.GetComponent<FloatingElementOrb>();

            if (orb != null)
            {
                orb.Initialize(spawnPos);

                // สุ่มประเภทธาตุ
                ElementType randomElem = ElementTypes.RandomPlayable();
                orb.elementType = randomElem;
                Color color = ElementTypes.GetColor(randomElem);

                // สีตามธาตุ: URP Lit ใช้ _BaseColor (.color ของ Material ตั้งได้แค่ _Color) จึงตั้งทั้งคู่
                MeshRenderer mr = orbObj.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Material material = mr.material;
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                    if (material.HasProperty("_EmissionColor"))
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", color * 1.5f);
                    }
                }

                // แสงเรืองและประกายสีธาตุ ช่วยให้เห็นชัดตอนเล็ง
                SoftGlow.Sparkle(orbObj.transform, spawnPos, color, orbWorldSize * .5f);
            }

            activeOrbs.Add(orbObj);
        }

        // สร้างเสร็จให้ซ่อนไว้ก่อน (รอจนกว่าผู้เล่นจะกดคลิกขวา Free-Aim)
        ToggleOrbsVisibility(false);
        Debug.Log("<color=magenta>🔮 Spell Turn Started: 3 Elemental Orbs generated in the air.</color>");
    }

    // จุดเกิด: ระหว่างผู้เล่นกับกลุ่มศัตรู ลอยสูงจากพื้น กระจายตามแนวซ้าย-ขวาของกล้อง ไม่ซ้อนกัน
    private Vector3 PickPosition(List<Vector3> placed)
    {
        if (!spawnBetweenFighters)
        {
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            return spawnAreaCenter.position + new Vector3(circle.x, Random.Range(heightMin, heightMax), circle.y);
        }

        Camera cam = Camera.main;
        Vector3 player = PlayerCombatController.Instance != null ? PlayerCombatController.Instance.transform.position : transform.position;
        Vector3 enemies = player + (cam != null ? Flat(cam.transform.forward) : Vector3.forward) * 20f;
        if (CombatManager.Instance != null && CombatManager.Instance.enemies.Count > 0)
        {
            Vector3 sum = Vector3.zero;
            foreach (EnemyController enemy in CombatManager.Instance.enemies) sum += enemy.transform.position;
            enemies = sum / CombatManager.Instance.enemies.Count;
        }

        Vector3 middle = Vector3.Lerp(player, enemies, .55f);
        float ground = Mathf.Min(player.y, enemies.y);
        Vector3 side = cam != null ? Flat(cam.transform.right) : Vector3.right;

        Vector3 best = middle;
        float bestGap = -1f;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 candidate = middle + side * Random.Range(-sideSpread, sideSpread);
            candidate.y = ground + Random.Range(heightAboveGround.x, heightAboveGround.y);

            float gap = float.MaxValue;
            foreach (Vector3 other in placed) gap = Mathf.Min(gap, Vector3.Distance(other, candidate));
            if (gap >= minSeparation) return candidate;
            if (gap > bestGap) { bestGap = gap; best = candidate; }
        }
        return best;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > .0001f ? v.normalized : Vector3.forward;
    }

    // ลูกแก้วที่ใกล้เมาส์ที่สุดบนจอ (ในระยะ pickRadius) เล็งง่ายกว่ายิงรังสีโดนลูกเล็ก ๆ และไม่ถูกตัวละคร/ฉากบัง
    public FloatingElementOrb FindOrbAt(Vector2 screenPosition, Camera cam)
    {
        if (cam == null) return null;
        FloatingElementOrb best = null;
        float bestDistance = pickRadius * Screen.height;
        foreach (GameObject orbObj in activeOrbs)
        {
            if (orbObj == null) continue;
            FloatingElementOrb orb = orbObj.GetComponent<FloatingElementOrb>();
            if (orb == null || !orb.IsVisible) continue;

            Vector3 screen = cam.WorldToScreenPoint(orbObj.transform.position);
            if (screen.z <= 0f) continue;
            float distance = Vector2.Distance(screenPosition, screen);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = orb;
            }
        }
        return best;
    }

    // ลูกแก้วใต้เมาส์ขยายขึ้น บอกว่าคลิกแล้วจะเก็บลูกไหน
    public void Highlight(FloatingElementOrb target)
    {
        foreach (GameObject orbObj in activeOrbs)
        {
            if (orbObj == null) continue;
            FloatingElementOrb orb = orbObj.GetComponent<FloatingElementOrb>();
            if (orb != null) orb.SetHighlighted(orb == target, hoverScale);
        }
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
}
