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