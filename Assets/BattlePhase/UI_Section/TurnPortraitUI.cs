using UnityEngine;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class TurnPortraitUI : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public Image background;

    [Header("Animation")]
    public float slideSpeed = 15f;
    public float fadeSpeed = 10f;
    public float scaleSpeed = 12f; // [เพิ่มใหม่] ความเร็วในการเด้งขยาย/หด

    private RectTransform rect;
    private CanvasGroup canvasGroup;

    private Vector2 targetPosition;
    private float targetScale = 1f;

    [HideInInspector] public bool isNewlySpawned = true;
    private bool isDying = false;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // เกิดมาต้องโปร่งใสก่อน
    }

    public void Setup(CombatManager.PredictedTurn data)
    {
        if (isDying) return;
        nameText.text = data.Name;
        background.color = data.IsEnemy ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 0.7f, 1f);
    }

    // [เปลี่ยนชื่อและปรับลอจิก]
    public void UpdateRankAndPosition(Vector2 newPos, int currentRank)
    {
        if (isDying) return;

        // ถ้าไม่ใช่ตัวเพิ่งเกิดใหม่ และมีการเปลี่ยนตำแหน่ง (ตกอันดับ/ขึ้นอันดับ)
        if (!isNewlySpawned && targetPosition != newPos)
        {
            // บังคับวาร์ป (Snap) ไปตำแหน่งใหม่ทันที ไม่ต้องสไลด์
            rect.anchoredPosition = newPos;

            // เด้งขยายใหญ่ขึ้นชั่วคราว (Pop Effect) เพื่อกระแทกตาผู้เล่น
            //transform.localScale = Vector3.one * 1.4f;
        }

        targetPosition = newPos;

        // คิวแรกสุด (Index 0) จะสเกลใหญ่ค้างไว้ (1.2) ส่วนคิวอื่นๆ จะหดกลับไปขนาดปกติ (1.0)
        targetScale = (currentRank == 0) ? 1.4f : 1f;
    }

    public void SlideOutAndDestroy(Vector2 exitOffset)
    {
        isDying = true;
        targetPosition = rect.anchoredPosition + exitOffset; // พุ่งไปจุดตาย
        Destroy(gameObject, 0.5f);
    }

    private void Update()
    {
        // 1. จัดการตำแหน่ง: สไลด์ "เฉพาะ" ตอนเพิ่งเกิด หรือ ตอนกำลังปลิวตาย
        if (isNewlySpawned || isDying)
        {
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, targetPosition, Time.deltaTime * slideSpeed);

            // ถ้าเพิ่งสไลด์เข้ามาถึงจุดแล้ว ให้ปิดสถานะเพิ่งเกิด (ครั้งหน้าที่อัปเดตจะได้ Snap ตำแหน่งแทน)
            if (isNewlySpawned && Vector2.Distance(rect.anchoredPosition, targetPosition) < 5f)
            {
                isNewlySpawned = false;
            }
        }

        // 2. จัดการสเกล (ขยายตัว): ค่อยๆ เลิปกลับไปหาเป้าหมาย (ใหญ่สุดสำหรับคิวแรก, ปกติสำหรับคิวอื่น)
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, Time.deltaTime * scaleSpeed);

        // 3. เฟดจางเข้า-ออก
        float targetAlpha = isDying ? 0f : 1f;
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
    }
}