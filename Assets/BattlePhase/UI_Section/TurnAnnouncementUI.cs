using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class TurnAnnouncementUI : MonoBehaviour
{
    public static TurnAnnouncementUI Instance;

    public TextMeshProUGUI announcementText;

    [Header("Settings")]
    public float fadeSpeed = 15f;
    public float displayDuration = 1.2f;
    public Vector3 popScale = new Vector3(1.2f, 1.2f, 1.2f);

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        Instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // ฟังก์ชันรับข้อความ และ Action(คำสั่งที่จะให้ทำต่อเมื่อโชว์ป้ายเสร็จ)
    public void AnnounceTurn(string message, System.Action onComplete)
    {
        StartCoroutine(AnnouncementRoutine(message, onComplete));
    }

    private IEnumerator AnnouncementRoutine(string message, System.Action onComplete)
    {
        announcementText.text = message;

        // เซ็ตขนาดเริ่มต้นให้ใหญ่กว่าปกตินิดนึงเพื่อทำ Pop Effect
        transform.localScale = popScale;

        // 1. เฟดเข้าอย่างรวดเร็ว
        while (canvasGroup.alpha < 0.99f)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1f, Time.deltaTime * fadeSpeed);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.deltaTime * fadeSpeed);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        transform.localScale = Vector3.one;

        // 2. ค้างข้อความไว้ให้ผู้เล่นเตรียมตัว
        yield return new WaitForSeconds(displayDuration);

        // 3. เฟดออก
        while (canvasGroup.alpha > 0.01f)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0f, Time.deltaTime * fadeSpeed);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        // 4. ส่งสัญญาณกลับไปบอก CombatManager ว่า "โชว์ป้ายเสร็จแล้ว ศัตรูตีได้เลย!"
        onComplete?.Invoke();
    }
}