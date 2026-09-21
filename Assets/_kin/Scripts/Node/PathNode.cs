using UnityEngine;
using TMPro; 

public class PathNode : MonoBehaviour
{
    // *** 1. เพิ่มตัวเลือกประเภทของโหนด ***
    public enum NodeAction { TeleportInScene, LoadNewScene }

    [Header("Node Action")]
    public NodeAction nodeAction = NodeAction.TeleportInScene; // เลือกประเภทโหนด
    [Tooltip("จุดวาร์ปในซีนเดียวกัน (ใช้เมื่อเลือก TeleportInScene)")]
    public Transform teleportDestination; 
    [Tooltip("ชื่อซีนที่จะโหลด (ใช้เมื่อเลือก LoadNewScene)")]
    public string sceneToLoad = "NextSceneName";

    [Header("Node Identity (Icon)")]
    public Sprite nodeIcon; 

    [Header("Aura & UI (Auto Generated)")]
    [HideInInspector] public GameObject nodeAura3D; 
    [HideInInspector] public GameObject uiMapAura;  
    [HideInInspector] public RectTransform uiNodeTransform; 

    [Header("Node Info (Text)")]
    public string nodeName = "จุดพักแคมป์"; 
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
        if (aura3D != null) 
        {
            nodeAura3D = aura3D.gameObject;
            nodeAura3D.SetActive(false);
        }

        if (nodeText != null)
        {
            nodeText.text = nodeName;
            nodeText.color = normalColor;
        }
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