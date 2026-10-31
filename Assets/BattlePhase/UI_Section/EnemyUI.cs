using UnityEngine;
using UnityEngine.UI;

public class EnemyUI : MonoBehaviour
{
    [Header("References")]
    public EnemyController enemy; // ลากสคริปต์ศัตรูตัวนั้นมาใส่

    [Header("HP Bar (ลดจากขวามาซ้าย)")]
    public Image hpFillImage;

    [Header("Posture Bar (ขยายจากตรงกลาง)")]
    public RectTransform postureFillRect;

    [Header("Animation Settings")]
    public float smoothSpeed = 10f;

    [Header("Status UI")]
    public Transform statusContainer; // กล่องที่จะเอาไอคอนไปเรียงต่อกัน
    public GameObject statusIconPrefab; // ตัว Prefab รูปไอคอน

    private Camera mainCam;
    private float targetHpFill;
    private float targetPostureScale;

    private void Start()
    {
        mainCam = Camera.main;

        // ถ้าไม่ได้ลากใส่ ให้หาจาก Object ตัวแม่
        if (enemy == null)
            enemy = GetComponentInParent<EnemyController>();

        // เซ็ตหลอด Posture ให้เริ่มต้นที่ 0 (มองไม่เห็น)
        if (postureFillRect != null)
            postureFillRect.localScale = new Vector3(0, 1, 1);

        if (enemy != null)
        {
            enemy.OnStatusChanged += RefreshStatusIcons;
            RefreshStatusIcons();
        }
    }
    private void OnDestroy()
    {
        // ยกเลิกการรับข่าวสารเมื่อศัตรูตาย ป้องกัน Error
        if (enemy != null)
        {
            enemy.OnStatusChanged -= RefreshStatusIcons;
        }
    }
    public void RefreshStatusIcons()
    {
        if (statusContainer == null || statusIconPrefab == null) return;

        foreach (Transform child in statusContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var status in enemy.activeStatuses)
        {
            GameObject iconObj = Instantiate(statusIconPrefab, statusContainer);

            Image img = iconObj.GetComponent<Image>();
            if (img != null && status.icon != null)
            {
                img.sprite = status.icon;
            }

            // สถานะจากสกิลไม่มีรูป ใช้กล่องสีเข้ม + ตัวย่อ (เช่น EXP2 / SHT / SLW1)
            if (!string.IsNullOrEmpty(status.label))
            {
                if (img != null && status.icon == null) img.color = new Color(0.15f, 0.05f, 0.25f, 0.85f);
                AddStatusLabel(iconObj.transform, status.label);
            }
        }
    }

    private static void AddStatusLabel(Transform iconTransform, string label)
    {
        GameObject labelObj = new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(iconTransform, false);

        RectTransform rect = (RectTransform)labelObj.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI text = labelObj.AddComponent<TMPro.TextMeshProUGUI>();
        text.text = label;
        text.enableAutoSizing = true;
        text.fontSizeMin = 1f;
        text.fontSizeMax = 40f;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (enemy == null) return;

        // --- 1. คำนวณเป้าหมายของหลอดเลือด (HP) ---
        targetHpFill = (float)enemy.currentHP / enemy.maxHP;

        if (hpFillImage != null)
        {
            // ใช้ Lerp เพื่อให้หลอดเลือดค่อยๆ ไหลลดลงมาอย่างนุ่มนวล
            hpFillImage.fillAmount = Mathf.Lerp(hpFillImage.fillAmount, targetHpFill, Time.deltaTime * smoothSpeed);
        }

        // --- 2. คำนวณเป้าหมายของหลอด Posture ---
        // หมายเหตุ: ในโค้ดของคุณ currentPosture เริ่มที่ max แล้วลดลง 0 
        // ดังนั้นภาพที่ต้องโชว์คือความเสียหายที่สะสม (ยิ่งเลือดเหลือน้อย หลอดยิ่งเต็ม)
        float postureDamageTaken = enemy.maxPosture - enemy.currentPosture;
        targetPostureScale = postureDamageTaken / enemy.maxPosture;

        if (postureFillRect != null)
        {
            float currentScale = postureFillRect.localScale.x;
            float newScale = Mathf.Lerp(currentScale, targetPostureScale, Time.deltaTime * smoothSpeed);

            // ใช้การ Scale แกน X เพื่อทำให้มันขยายออกสองข้าง (ต้องตั้ง Pivot ให้ถูกต้องใน Unity)
            postureFillRect.localScale = new Vector3(newScale, 1f, 1f);
        }

        // --- 3. บังคับให้ Canvas หันหน้าเข้าหากล้องเสมอ (Billboard) ---
        if (mainCam != null)
        {
            transform.rotation = mainCam.transform.rotation;
        }
    }
}