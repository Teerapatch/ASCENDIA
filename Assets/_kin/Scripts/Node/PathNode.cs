using UnityEngine;
using TMPro; 

public class PathNode : MonoBehaviour
{
    // 🌟 รองรับครบทั้ง 6 รูปแบบตาม Requirement [CFC00004]
    public enum RoomType 
    { 
        Normal,     // ปีนเขาปกติ (Climb เดิม)
        Battle,     // ต่อสู้มอนสเตอร์ทั่วไป
        MiniBoss,   // บอสประจำแคมป์ / มินิบอส
        Ore,        // ขุดแร่
        Rest,       // จุดพัก / Loadout / Craft อาวุธ
        Camp        // แคมป์สุดท้าย
    }

    [Header("Node Action & Routing")]
    public RoomType roomType = RoomType.Normal; 
    [Tooltip("ใส่ชื่อซีนที่จะโหลด (ถ้ายังไม่มีซีน ให้เว้นว่างไว้ ระบบจะให้ปีนต่อแทน)")]
    public string sceneToLoad = ""; 

    [Header("Node Rewards (สำหรับโหนด Ore / Rest ช่วงเทส)")]
    public int oreAmount = 5;           // จำนวนแร่ที่ได้เมื่อเลือกโหนด Ore
    public float staminaRestore = 50f;  // Stamina ที่ฟื้นฟูเมื่อเลือกโหนด Rest

    [Header("Node Identity (Icon)")]
    public Sprite nodeIcon; 

    [Header("Aura & UI (Auto Generated)")]
    [HideInInspector] public GameObject nodeAura3D; 
    [HideInInspector] public GameObject uiMapAura;  
    [HideInInspector] public RectTransform uiNodeTransform; 

    [Header("Node Info (Text)")]
    public string nodeName = "ทางปีนเขา"; 
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