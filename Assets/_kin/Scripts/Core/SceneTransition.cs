using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// เปลี่ยนฉากทั้งเกมผ่านตัวนี้: จอค่อย ๆ ดำ -> โหลดฉาก -> รอให้ฉากใหม่ตั้งตัว -> จอค่อย ๆ สว่าง
// สร้างตัวเองครั้งแรกที่เรียก อยู่ข้ามฉาก (DontDestroyOnLoad) Canvas Overlay ลำดับ 1000 ทับทุก UI ระหว่างจอดำ
// ใช้เวลาจริง (unscaled) จึงไม่ค้างแม้ Time.timeScale = 0 (เมนูหยุดเกม / Hitstop)
public class SceneTransition : MonoBehaviour
{
    private static SceneTransition instance;
    public static bool IsBusy { get; private set; }

    [Tooltip("เวลาจอค่อย ๆ ดำก่อนโหลดฉาก (วินาที)")]
    public float fadeOutTime = 0.45f;
    [Tooltip("เวลาจอค่อย ๆ สว่างหลังโหลดฉาก (วินาที)")]
    public float fadeInTime = 0.6f;
    [Tooltip("รอหลังโหลดเสร็จกี่เฟรมก่อนเปิดจอ (ให้ Start ของฉากใหม่ตั้งกล้อง/ตำแหน่งให้เสร็จ)")]
    public int settleFrames = 3;

    private CanvasGroup group;

    private static SceneTransition Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("SceneTransition");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<SceneTransition>();
                instance.Build();
            }
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        IsBusy = false;
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
        black.transform.SetParent(transform, false);
        var rect = (RectTransform)black.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        black.GetComponent<Image>().color = Color.black;
    }

    // โหลดฉากแบบมีจอดำคั่น beforeLoad = ทำตอนจอดำสนิทแล้ว (ก่อนโหลด) เช่นรีเซ็ตข้อมูลรัน
    public static void Load(string sceneName, Action beforeLoad = null)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (IsBusy) { Debug.LogWarning($"SceneTransition: กำลังเปลี่ยนฉากอยู่ ข้ามการโหลด '{sceneName}'"); return; }
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"SceneTransition: ไม่พบฉาก '{sceneName}' ใน Build Settings");
            return;
        }
        Instance.StartCoroutine(Instance.Run(sceneName, beforeLoad));
    }

    // จอดำค้างไว้ (เช่นก่อนเปิดหน้าจบ) / เปิดจอคืน
    public static void FadeToBlack(float seconds = -1f) => Instance.StartCoroutine(Instance.Fade(1f, seconds < 0f ? Instance.fadeOutTime : seconds));
    public static void FadeFromBlack(float seconds = -1f) => Instance.StartCoroutine(Instance.Fade(0f, seconds < 0f ? Instance.fadeInTime : seconds));

    private IEnumerator Run(string sceneName, Action beforeLoad)
    {
        IsBusy = true;
        group.blocksRaycasts = true; // กันคลิกซ้ำระหว่างเปลี่ยนฉาก
        yield return Fade(1f, fadeOutTime);

        try { beforeLoad?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
        Time.timeScale = 1f;

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        while (load != null && !load.isDone) yield return null;
        for (int i = 0; i < settleFrames; i++) yield return null;

        IsBusy = false;
        yield return Fade(0f, fadeInTime);
        group.blocksRaycasts = false;
    }

    private IEnumerator Fade(float target, float seconds)
    {
        float start = group.alpha;
        if (seconds <= 0f) { group.alpha = target; yield break; }
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            group.alpha = Mathf.Lerp(start, target, k * k * (3f - 2f * k));
            yield return null;
        }
        group.alpha = target;
    }
}
