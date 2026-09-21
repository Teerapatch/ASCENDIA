using UnityEngine;
using TMPro; 

public class PathNode : MonoBehaviour
{
    public enum RoomType { Climb, Battle, CampScene }

    [Header("Node Action")]
    public RoomType roomType = RoomType.Climb; 
    [Tooltip("ใส่ชื่อซีนที่จะโหลด (ใช้เมื่อเป็น Battle หรือ CampScene)")]
    public string sceneToLoad = ""; // ปล่อยว่างไว้ จะได้พิมพ์ใส่เองใน Inspector

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