using UnityEngine;
using TMPro; // สำคัญ: ต้องใส่บรรทัดนี้เพื่อเรียกใช้ TextMeshPro

public class PathNode : MonoBehaviour
{
    [Header("Aura & UI")]
    public GameObject nodeAura3D; 
    public GameObject uiMapAura;  

    [Header("Node Info (Text)")]
    public string nodeName = "จุดพักแคมป์"; // พิมพ์ชื่อที่จะให้แสดงตรงนี้
    public TextMeshPro nodeText; // ลาก 3D Text มาใส่ช่องนี้
    public Color normalColor = Color.white; // สีข้อความตอนปกติ
    public Color hoverColor = Color.yellow; // สีข้อความตอนเอาเมาส์ชี้

    [Header("Progression")]
    public Transform climbTarget;  
    public string sceneToLoad = "NextSceneName"; 

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

        if (nodeAura3D) nodeAura3D.SetActive(false);
        if (uiMapAura) uiMapAura.SetActive(false);

        // อัปเดตข้อความให้แสดงตามที่ตั้งชื่อไว้ทันที
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

        // เปลี่ยนสีข้อความตอนเมาส์ชี้
        if (nodeText != null)
        {
            nodeText.color = isHovering ? hoverColor : normalColor;
        }
    }
}