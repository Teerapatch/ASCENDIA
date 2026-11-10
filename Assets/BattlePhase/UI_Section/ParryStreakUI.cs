using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// แถบแสงสีขาวพาดผ่านจอตอน Parry สำเร็จ วิ่งตามทิศที่ผู้เล่นลาก และอยู่ "หลัง" ตัวศัตรู
// - ใส่บน Canvas แบบ Screen Space - Camera (แยกจาก Canvas HUD) ระยะ planeDistance ถูกตั้งทุกเฟรมให้อยู่ลึกกว่าศัตรู
//   UI ของ Canvas แบบกล้องถูกเทียบความลึกกับฉาก -> ตัวศัตรูบังแถบ แต่แถบทับฉากหลัง
// - streak: Image แถบยาว (pivot ซ้าย x=0) ตกแต่งได้ใน Canvas (สี/สไปรต์/ลูกที่เป็นเส้นขอบ) โค้ดแค่ยืด-หมุน-จาง
public class ParryStreakUI : MonoBehaviour
{
    public static ParryStreakUI Instance;

    public Canvas canvas;
    [Tooltip("แถบหลัก (RectTransform ที่มี pivot x = 0) ลูกๆ จะถูกยืด/หมุนตามไปด้วย")]
    public RectTransform streak;
    [Tooltip("ใช้คุมความโปร่งของแถบทั้งชุด")]
    public CanvasGroup streakGroup;

    [Header("Placement")]
    [Tooltip("ระยะที่ดันแถบไปอยู่หลังจุดกลางตัวศัตรู (หน่วยโลก ตามแนวกล้อง)")]
    public float depthBehindTarget = 2.5f;
    [Tooltip("ความยาวแถบเทียบกับเส้นทแยงจอ (>1 = ยาวเลยขอบจอ)")]
    public float lengthScreenRatio = 1.6f;
    [Tooltip("ความหนาของแถบเทียบกับความสูงจอ (0.6 = 60% ของจอ)")]
    public float bandHeightScreenRatio = 0.6f;

    [Header("Animation (เวลาจริง ไม่โดน Hitstop)")]
    public float sweepTime = 0.1f;
    public float holdTime = 0.12f;
    public float fadeTime = 0.3f;
    [Tooltip("ความหนาตอนพุ่ง (เท่าของความสูงที่ตั้งใน Canvas) แล้วค่อยบีบลงเหลือ 1 ก่อนจาง")]
    public float startThickness = 1.6f;

    private Coroutine running;

    private void Awake()
    {
        Instance = this;
        if (canvas == null) canvas = GetComponent<Canvas>();
        SetVisible(false);
    }

    // target: ศัตรูที่ถูก Parry, screenDirection: ทิศที่ลาก (พิกเซลบนจอ)
    public void Play(Transform target, Vector2 screenDirection)
    {
        if (streak == null || target == null || screenDirection.sqrMagnitude < 0.0001f) return;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(PlayRoutine(target, screenDirection.normalized));
    }

    private IEnumerator PlayRoutine(Transform target, Vector2 dir)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        SetVisible(true);

        float total = sweepTime + holdTime + fadeTime;
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            if (target == null) break;
            Place(cam, target, dir, angle);

            float sweep = sweepTime > 0f ? Mathf.Clamp01(t / sweepTime) : 1f;
            float fade = Mathf.Clamp01((t - sweepTime - holdTime) / Mathf.Max(0.0001f, fadeTime));
            float easeOut = 1f - (1f - sweep) * (1f - sweep);
            float thickness = Mathf.Lerp(startThickness, 1f, Mathf.Clamp01((t - sweepTime) / Mathf.Max(0.0001f, holdTime)));
            thickness = Mathf.Lerp(thickness, 0f, fade);

            // ขยายแถบให้หนาเทียบกับจอ ไม่ว่าจะตั้งความสูงใน Canvas ไว้เท่าไร
            float bandScale = ((RectTransform)canvas.transform).rect.height * bandHeightScreenRatio / Mathf.Max(1f, streak.sizeDelta.y);
            streak.localScale = new Vector3(easeOut, thickness * bandScale, 1f);
            if (streakGroup != null) streakGroup.alpha = 1f - fade;
            yield return null;
        }

        SetVisible(false);
        running = null;
    }

    // วางแถบให้ผ่านกลางตัวศัตรูบนจอ เริ่มจากฝั่งตรงข้ามทิศที่ลาก และอยู่ลึกกว่าศัตรู depthBehindTarget
    private void Place(Camera cam, Transform target, Vector2 dir, float angle)
    {
        Vector3 center = TargetCenter(target);
        float depth = Vector3.Dot(center - cam.transform.position, cam.transform.forward);
        canvas.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, depth + depthBehindTarget);

        RectTransform canvasRect = (RectTransform)canvas.transform;

        // แถบพาดผ่านกลางจอเสมอ ความลึกยังอยู่หลังตัวศัตรู
        float length = canvasRect.rect.size.magnitude * lengthScreenRatio;
        streak.sizeDelta = new Vector2(length, streak.sizeDelta.y);
        streak.anchoredPosition = -dir * (length * 0.5f);
        streak.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // จุดกลางตัวจาก Mesh ของศัตรู (ข้าม UI/อนุภาคที่ติดตัวอยู่)
    public static Vector3 TargetCenter(Transform target)
    {
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found ? bounds.center : target.position;
    }

    private void SetVisible(bool visible)
    {
        if (streak != null) streak.gameObject.SetActive(visible);
        if (streakGroup != null) streakGroup.alpha = visible ? 1f : 0f;
    }
}
