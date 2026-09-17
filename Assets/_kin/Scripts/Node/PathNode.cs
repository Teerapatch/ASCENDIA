using UnityEngine;

public class PathNode : MonoBehaviour
{
    [Header("Aura & UI")]
    public GameObject nodeAura3D; 
    public GameObject uiMapAura;  

    [Header("Progression")]
    public Transform climbTarget;  
    public string sceneToLoad = "NextSceneName"; 

    [Header("Hover Animation")]
    public float hoverScaleMultiplier = 1.2f; // ขยายใหญ่ขึ้น 20% ตอนเมาส์ชี้
    public float scaleSpeed = 10f; // ความเร็วในการขยาย

    private Vector3 originalScale;
    private Vector3 targetScale;
    private bool isHovering = false;

    private void Start()
    {
        // จำขนาดดั้งเดิมของมันไว้
        originalScale = transform.localScale;
        targetScale = originalScale;

        if (nodeAura3D) nodeAura3D.SetActive(false);
        if (uiMapAura) uiMapAura.SetActive(false);
    }

    private void Update()
    {
        // ทำให้การขยายและหดดูสมูท (Lerp)
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
    }

    public void SetHover(bool hover)
    {
        isHovering = hover;
        
        // ถ้าเมาส์ชี้ ให้เป้าหมายใหญ่ขึ้น ถ้าเอาออก ให้เป้าหมายกลับเท่าเดิม
        targetScale = isHovering ? originalScale * hoverScaleMultiplier : originalScale;

        if (nodeAura3D) nodeAura3D.SetActive(isHovering);
        if (uiMapAura) uiMapAura.SetActive(isHovering);
    }
}