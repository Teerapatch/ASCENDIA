using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PathNode : MonoBehaviour
{
    public enum RoomType 
    { 
        Normal,     // ทางปีนเขาปกติ
        Battle,     // สู้มอนสเตอร์ทั่วไป
        MiniBoss,   // สู้มินิบอสระหว่างทาง (คล้าย Battle แต่ตึงมือขึ้น)
        Boss,       // 🌟 บอสใหญ่ท้ายสุดของเกม (โหดที่สุด)
        Ore,        // แจ็คพอตแร่
        Rest,       // พักฟื้นฟูระหว่างทาง
        Camp        // แคมป์พักใหญ่ท้ายช่วง
    }

    [Header("Node Action & Routing")]
    public RoomType roomType = RoomType.Normal; 
    [Tooltip("ชื่อซีนที่ต้องการโหลดเมื่อเลือกโหนดนี้ (เช่น BattleScene, MiniBossScene, BossScene, CampScene)")]
    public string sceneToLoad = "";

    [Header("Battle (เฉพาะ Battle / MiniBoss / Boss)")]
    [Tooltip("สุ่ม 1 อันตอนเลือกโหนดนี้ ถ้าว่างฉากต่อสู้จะใช้ศัตรูที่ตั้งไว้ในฉากเอง")]
    public List<EncounterData> encounterPool = new List<EncounterData>();

    [Header("Node Rewards (เฉพาะ Ore / Rest)")]
    public ItemData oreRewardItem;
    public int oreAmount = 20;
    public float staminaRestore = 50f;

    [Header("Node Identity (Icon)")]
    public Sprite nodeIcon; 

    [Header("Aura & UI (Auto Generated)")]
    [HideInInspector] public GameObject nodeAura3D; 
    [HideInInspector] public GameObject uiMapAura;  
    [HideInInspector] public RectTransform uiNodeTransform; 

    [Header("Node Info (Text)")]
    public string nodeName = "Node";
    public TextMeshPro nodeText; 
    public Color normalColor = Color.white; 
    public Color hoverColor = Color.yellow; 

    [Header("Progression")]
    public Transform climbTarget;  

    [Header("Hover Animation")]
    public float hoverScaleMultiplier = 1.2f; 
    public float scaleSpeed = 10f; 

    private Vector3 originalScale;
    private Vector3 targetScale;
    private bool isHovering = false;

    public bool IsBattle => roomType == RoomType.Battle || roomType == RoomType.MiniBoss || roomType == RoomType.Boss;

    // ชื่อบนแผนที่ UI (ฟอนต์ของอาร์ตไม่มีอักษรไทย)
    public string DisplayName
    {
        get
        {
            switch (roomType)
            {
                case RoomType.Battle: return "BATTLE";
                case RoomType.MiniBoss: return "MINI BOSS";
                case RoomType.Boss: return "BOSS";
                case RoomType.Ore: return "MINE"; // ตรงกับการ์ดของอาร์ต
                case RoomType.Rest: return "REST";
                case RoomType.Camp: return "CAMP";
                default: return "NORMAL";
            }
        }
    }

    // คำอธิบายบนการ์ดเลือกเส้นทาง (UI ReArt v2) ฟอนต์ไม่มีภาษาไทยจึงเป็นอังกฤษ
    public string Description
    {
        get
        {
            switch (roomType)
            {
                case RoomType.Battle: return "Encounter with Mystery Guardians.";
                case RoomType.MiniBoss: return "A stronger guardian blocks the way.";
                case RoomType.Boss: return "The guardian of the summit awaits.";
                case RoomType.Ore: return "Mine Path, you can get more ores with this path.";
                case RoomType.Rest: return "A safe ledge to catch your breath.";
                case RoomType.Camp: return "Set up camp to rest and forge.";
                default: return "Random Path you can encounter with battle, ore, etc.";
            }
        }
    }

    public EncounterData PickEncounter()
    {
        List<EncounterData> valid = encounterPool.FindAll(e => e != null);
        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
    }

    private void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;

        Transform aura3D = transform.Find("Aura3D"); 
        if (aura3D != null) { nodeAura3D = aura3D.gameObject; nodeAura3D.SetActive(false); }

        if (nodeText != null) { nodeText.text = nodeName; nodeText.color = normalColor; }

        if (NodeSelectionManager.Instance != null && NodeSelectionManager.Instance.hideNodesInScene) HideVisuals();
    }

    // เลือกเส้นทางด้วยการ์ดแล้ว ไม่ต้องเห็น Node บนหน้าผา: ซ่อนทุกอย่างที่มองเห็น/ชนได้ แต่ยังเก็บข้อมูลของ Node (ประเภท, Encounter) ไว้ให้การ์ดใช้
    public void HideVisuals()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (Canvas c in GetComponentsInChildren<Canvas>(true)) c.enabled = false;
        foreach (Light l in GetComponentsInChildren<Light>(true)) l.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (ParticleSystem p in GetComponentsInChildren<ParticleSystem>(true)) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
    }

    public void SetHover(bool hover)
    {
        isHovering = hover;
        targetScale = isHovering ? originalScale * hoverScaleMultiplier : originalScale;

        if (nodeAura3D) nodeAura3D.SetActive(isHovering);
        if (uiMapAura) uiMapAura.SetActive(isHovering);
        if (nodeText != null) nodeText.color = isHovering ? hoverColor : normalColor;
    }
}