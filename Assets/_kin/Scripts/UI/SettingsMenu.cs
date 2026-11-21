using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// หน้า Settings: Prefab ใน Resources (Assets/Resources/SettingsMenu.prefab) สร้างตัวเองตอนเปิดครั้งแรก อยู่ข้ามฉาก
// แต่ง/ย้ายได้ใน Prefab เลย โค้ดหาแถวจากชื่อ: Window/Rows/Row_<ชื่อ> ข้างในมี Value (ข้อความ), Left / Right (ปุ่ม <  >), Bar/Fill (แถบ ถ้ามี)
// ปุ่มปิด = Window/Btn_Close หรือกด Esc  เปิดได้จากหน้า Title และหน้าต่าง Esc ในเกม (GameExitMenu)
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.isOpen;
    // เฟรมที่ปิดล่าสุด: หน้าต่างอื่นที่ใช้ Esc (GameExitMenu) จะได้ไม่เปิด/ปิดตามในเฟรมเดียวกัน
    public static int LastClosedFrame = -1;

    public CanvasGroup group;
    public RectTransform window;
    public float fadeDuration = 0.2f;
    [Tooltip("ปรับเสียง/การสั่นทีละกี่ส่วน")]
    public float step = 0.1f;

    private bool isOpen;
    private Coroutine fadeRoutine;
    private System.Action onClosed;

    private class Row
    {
        public TMP_Text value;
        public Image fill;
    }
    private Row volume, shake, screen, resolution, quality, vsync;

    public static void Open(System.Action closed = null)
    {
        if (Instance == null)
        {
            SettingsMenu prefab = Resources.Load<SettingsMenu>("SettingsMenu");
            if (prefab == null) { Debug.LogWarning("ไม่พบ Resources/SettingsMenu.prefab (Tools/ASCENDIA/UI/Build Settings Prefab)"); return; }
            Instantiate(prefab);
        }
        Instance.Show(closed);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (group == null) group = GetComponent<CanvasGroup>();
        if (window == null) window = transform.Find("Window") as RectTransform;

        volume = BindRow("Row_Volume", d => GameSettings.SetMasterVolume(Snap(GameSettings.MasterVolume + d * step)));
        shake = BindRow("Row_Shake", d => GameSettings.SetCameraShake(Snap(GameSettings.CameraShake + d * step)));
        screen = BindRow("Row_Screen", d => GameSettings.SetFullscreen(!GameSettings.Fullscreen));
        resolution = BindRow("Row_Resolution", d =>
        {
            int count = GameSettings.Resolutions.Length;
            GameSettings.SetResolution(((GameSettings.CurrentResolutionIndex() + d) % count + count) % count);
        });
        quality = BindRow("Row_Quality", d =>
        {
            int count = QualitySettings.names.Length;
            GameSettings.SetQuality(((GameSettings.Quality + d) % count + count) % count);
        });
        vsync = BindRow("Row_VSync", d => GameSettings.SetVSync(!GameSettings.VSync));

        Button close = window != null && window.Find("Btn_Close") != null ? window.Find("Btn_Close").GetComponent<Button>() : null;
        if (close != null) close.onClick.AddListener(Close);

        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        gameObject.SetActive(true);
    }

    private static float Snap(float value) => Mathf.Clamp01(Mathf.Round(value * 10f) / 10f);

    private Row BindRow(string name, System.Action<int> change)
    {
        Transform row = window != null ? window.Find("Rows/" + name) : null;
        if (row == null) return null;
        Row view = new Row
        {
            value = row.Find("Value") != null ? row.Find("Value").GetComponent<TMP_Text>() : null,
            fill = row.Find("Bar/Fill") != null ? row.Find("Bar/Fill").GetComponent<Image>() : null,
        };
        Button left = row.Find("Left") != null ? row.Find("Left").GetComponent<Button>() : null;
        Button right = row.Find("Right") != null ? row.Find("Right").GetComponent<Button>() : null;
        if (left != null) left.onClick.AddListener(() => { change(-1); Refresh(); });
        if (right != null) right.onClick.AddListener(() => { change(1); Refresh(); });
        return view;
    }

    private void Show(System.Action closed)
    {
        onClosed = closed;
        isOpen = true;
        Refresh();
        group.blocksRaycasts = true;
        group.interactable = true;
        Fade(1f);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        LastClosedFrame = Time.frameCount;
        group.blocksRaycasts = false;
        group.interactable = false;
        Fade(0f);
        System.Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    private void Refresh()
    {
        Set(volume, Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%", GameSettings.MasterVolume);
        Set(shake, GameSettings.CameraShake <= 0f ? "OFF" : Mathf.RoundToInt(GameSettings.CameraShake * 100f) + "%", GameSettings.CameraShake);
        Set(screen, GameSettings.Fullscreen ? "FULLSCREEN" : "WINDOWED", -1f);
        Resolution[] list = GameSettings.Resolutions;
        Resolution r = list[Mathf.Clamp(GameSettings.CurrentResolutionIndex(), 0, list.Length - 1)];
        Set(resolution, $"{r.width} x {r.height}", -1f);
        string[] names = QualitySettings.names;
        Set(quality, names.Length > 0 ? names[Mathf.Clamp(GameSettings.Quality, 0, names.Length - 1)].ToUpper() : "-", -1f);
        Set(vsync, GameSettings.VSync ? "ON" : "OFF", -1f);
    }

    private static void Set(Row row, string text, float fill)
    {
        if (row == null) return;
        if (row.value != null) row.value.text = text;
        if (row.fill != null && fill >= 0f) row.fill.fillAmount = fill;
    }

    private void Update()
    {
        if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    private void Fade(float target)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    // เวลาจริง: เปิดจากหน้าต่าง Esc ตอน timeScale = 0 ได้
    private IEnumerator FadeRoutine(float target)
    {
        float from = group.alpha;
        Vector3 small = Vector3.one * 0.96f;
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            float k = t / fadeDuration;
            group.alpha = Mathf.Lerp(from, target, k);
            if (window != null) window.localScale = Vector3.Lerp(target > from ? small : Vector3.one, target > from ? Vector3.one : small, k);
            yield return null;
        }
        group.alpha = target;
        if (window != null) window.localScale = Vector3.one;
        fadeRoutine = null;
    }
}
