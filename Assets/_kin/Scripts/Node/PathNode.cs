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

    private void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;

        Transform aura3D = transform.Find("Aura3D"); 
        if (aura3D != null) { nodeAura3D = aura3D.gameObject; nodeAura3D.SetActive(false); }

        if (nodeText != null) { nodeText.text = nodeName; nodeText.color = normalColor; }
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