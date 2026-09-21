using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    public TextMeshPro textMesh;

    private float disappearTimer;
    private Color textColor;
    private Vector3 moveVector;
    private float baseFontSize;
    private Camera mainCam;

    public void Setup(int damageAmount, bool isCritical, Color color)
    {
        textMesh.text = damageAmount.ToString();
        textColor = color;
        textMesh.color = textColor;
        disappearTimer = 1f; // ตัวเลขจะอยู่บนจอนาน 1 วินาที
        baseFontSize = textMesh.fontSize;

        // สุ่มทิศทางการเด้งกระจายเล็กน้อย (ลอยขึ้นด้านบนเป็นหลัก)
        moveVector = new Vector3(Random.Range(-1f, 1f), Random.Range(2f, 4f), 0f);

        mainCam = Camera.main;

        // ถ้าเป็นคริติคอล หรือดาเมจรุนแรง ให้ตัวเลขใหญ่ขึ้น
        if (isCritical)
        {
            textMesh.fontSize = baseFontSize * 1.5f;
            textMesh.fontStyle = FontStyles.Bold;
        }
    }

    private void Update()
    {
        // 1. ทำให้ตัวเลขลอยขึ้น และค่อยๆ ช้าลง (Friction)
        transform.position += moveVector * Time.deltaTime;
        moveVector -= moveVector * 4f * Time.deltaTime;

        if (mainCam != null)
        {
            transform.rotation = mainCam.transform.rotation;
        }

        // 2. แอนิเมชัน Pop (ขยายใหญ่แล้วหดกลับมาขนาดปกติในช่วง 0.2 วินาทีแรก)
        if (disappearTimer > 0.8f)
        {
            float scaleAmount = 1f + (disappearTimer - 0.8f) * 2f;
            transform.localScale = Vector3.one * scaleAmount;
        }
        else
        {
            transform.localScale = Vector3.one;
        }

        // 3. เริ่มเฟดจางหายไปในช่วงครึ่งวินาทีสุดท้าย
        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0)
        {
            float fadeSpeed = 5f;
            textColor.a -= fadeSpeed * Time.deltaTime;
            textMesh.color = textColor;

            // ทำลายทิ้งเมื่อจางสนิท
            if (textColor.a < 0)
            {
                Destroy(gameObject);
            }
        }
    }
}