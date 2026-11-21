using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PitonUIController : MonoBehaviour
{
    [Header("Piton Icons")]
    public Image[] pitonIcons; 
    public Color activeColor = Color.white; 
    public Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.3f); 

    [Header("Animation Settings")]
    public float popMultiplier = 1.25f; // ตัวคูณตอนขยายใหญ่ (1.25 คือขยายขึ้น 25%)
    public float popDuration = 0.15f; 
    public float fadeDuration = 0.4f;

    // ตัวแปรเก็บขนาดดั้งเดิมที่คุณตั้งไว้ใน Inspector
    private Vector3[] originalScales;

    private void Start()
    {
        // 1. จำขนาดดั้งเดิมของหมุดแต่ละอันไว้ตั้งแต่ตอนเริ่มเกม
        originalScales = new Vector3[pitonIcons.Length];
        for (int i = 0; i < pitonIcons.Length; i++)
        {
            if (pitonIcons[i] != null)
            {
                originalScales[i] = pitonIcons[i].transform.localScale;
            }
        }

        if (GameManager.Instance != null)
        {
            InitPitonIcons(GameManager.Instance.playerData.piton);
        }
    }

    public void InitPitonIcons(int currentPitons)
    {
        for (int i = 0; i < pitonIcons.Length; i++)
        {
            if (pitonIcons[i] == null) continue;

            pitonIcons[i].color = (i < currentPitons) ? activeColor : inactiveColor;
            // ใช้ขนาดดั้งเดิมที่จำไว้ แทนการล็อคค่า
            pitonIcons[i].transform.localScale = originalScales[i]; 
        }
    }

    public void ShowLostPitonAlert(int currentPitons)
    {
        if (pitonIcons == null || currentPitons < 0 || currentPitons >= pitonIcons.Length) return;
        Image icon = pitonIcons[currentPitons];
        if (icon == null) return;

        // ClimbArtUI เปลี่ยน pitonIcons เป็นไอคอนของอาร์ตแล้วซ่อน HUD เก่า (ที่มี Component นี้อยู่) ไว้
        // Start จึงไม่เคยทำงาน (originalScales ยังว่าง) และเริ่ม Coroutine บนวัตถุที่ปิดอยู่ไม่ได้
        Vector3 baseScale = originalScales != null && currentPitons < originalScales.Length && originalScales[currentPitons] != Vector3.zero
            ? originalScales[currentPitons] : icon.transform.localScale;
        if (isActiveAndEnabled) StartCoroutine(AnimateLostPiton(icon, baseScale));
        else icon.color = inactiveColor;
    }

    private IEnumerator AnimateLostPiton(Image icon, Vector3 baseScale)
    {
        float time = 0;
        
        // เอาขนาดดั้งเดิมของคุณ มาคูณกับตัวคูณให้ใหญ่ขึ้นนิดนึง
        Vector3 popScale = baseScale * popMultiplier; 

        // เฟส 1: เด้งขยาย
        while (time < popDuration)
        {
            time += Time.deltaTime;
            float t = time / popDuration;
            icon.transform.localScale = Vector3.Lerp(baseScale, popScale, t);
            yield return null;
        }

        time = 0;

        // เฟส 2: หดกลับพร้อมเฟดสี
        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float t = time / fadeDuration;
            icon.transform.localScale = Vector3.Lerp(popScale, baseScale, t);
            icon.color = Color.Lerp(activeColor, inactiveColor, t);
            yield return null;
        }

        icon.transform.localScale = baseScale;
        icon.color = inactiveColor;
    }
}