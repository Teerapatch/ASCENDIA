using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// หน้า Title (TitleScene): โลโก้ + ปุ่ม PLAY / SETTING / CREDIT
// ฉากสร้างด้วย Tools/ASCENDIA/UI/Build Title Scene แล้วแต่งต่อใน Hierarchy ได้ โค้ดหาชิ้นจากชื่อ:
//   Background, Logo, Menu/Btn_Play, Menu/Btn_Setting, Menu/Btn_Credit, Credits (หน้าต่างเครดิต มี Btn_Back), Fade
public class TitleMenu : MonoBehaviour
{
    [Tooltip("ฉากที่กด PLAY แล้วไป (ต้องอยู่ใน Build Settings)")]
    public string playScene = "ClimbScene";
    public float introFade = 0.9f;
    public float buttonStagger = 0.12f;
    public float playFade = 0.7f;
    [Tooltip("พื้นหลังค่อย ๆ ซูมช้า ๆ (เท่าต่อวินาที)")]
    public float backgroundDrift = 0.006f;

    private RectTransform background, logo, menu;
    private CanvasGroup fade, credits, logoGroup;
    private readonly System.Collections.Generic.List<RectTransform> buttons = new System.Collections.Generic.List<RectTransform>();
    private readonly System.Collections.Generic.List<Vector2> buttonPositions = new System.Collections.Generic.List<Vector2>();
    private bool busy;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        background = transform.Find("Background") as RectTransform;
        logo = transform.Find("Logo") as RectTransform;
        menu = transform.Find("Menu") as RectTransform;
        fade = Group("Fade");
        credits = Group("Credits");
        logoGroup = logo != null ? GetOrAdd<CanvasGroup>(logo.gameObject) : null;

        Bind("Menu/Btn_Play", Play);
        Bind("Menu/Btn_Setting", () => { if (!busy) SettingsMenu.Open(); });
        Bind("Menu/Btn_Credit", () => ShowCredits(true));
        Bind("Credits/Window/Btn_Back", () => ShowCredits(false));

        if (menu != null)
            foreach (Transform child in menu)
                if (child.gameObject.activeSelf) { buttons.Add((RectTransform)child); buttonPositions.Add(((RectTransform)child).anchoredPosition); }
        if (credits != null) { credits.alpha = 0f; credits.blocksRaycasts = false; credits.gameObject.SetActive(true); }
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private CanvasGroup Group(string path)
    {
        Transform t = transform.Find(path);
        return t != null ? GetOrAdd<CanvasGroup>(t.gameObject) : null;
    }

    private void Bind(string path, UnityEngine.Events.UnityAction action)
    {
        Transform t = transform.Find(path);
        Button button = t != null ? t.GetComponent<Button>() : null;
        if (button != null) button.onClick.AddListener(action);
    }

    private IEnumerator Start()
    {
        // เปิดฉาก: จอดำค่อย ๆ จาง โลโก้ลอยลงเข้าที่ ปุ่มเลื่อนเข้าทีละอัน
        if (fade != null) { fade.alpha = 1f; fade.blocksRaycasts = true; }
        if (logoGroup != null) logoGroup.alpha = 0f;
        foreach (RectTransform b in buttons) GetOrAdd<CanvasGroup>(b.gameObject).alpha = 0f;

        Vector2 logoPos = logo != null ? logo.anchoredPosition : Vector2.zero;
        float total = introFade + buttonStagger * buttons.Count + 0.5f;
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            if (fade != null) fade.alpha = 1f - Ease(t / introFade);
            if (logo != null)
            {
                float k = Ease((t - introFade * 0.35f) / 0.8f);
                logoGroup.alpha = k;
                logo.anchoredPosition = logoPos + Vector2.up * 40f * (1f - k);
                logo.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, k);
            }
            for (int i = 0; i < buttons.Count; i++)
            {
                float k = Ease((t - introFade * 0.6f - i * buttonStagger) / 0.45f);
                buttons[i].GetComponent<CanvasGroup>().alpha = k;
                buttons[i].anchoredPosition = buttonPositions[i] + Vector2.right * 80f * (1f - k);
            }
            yield return null;
        }
        if (fade != null) { fade.alpha = 0f; fade.blocksRaycasts = false; }
    }

    private static float Ease(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    private void Update()
    {
        if (background != null) background.localScale = Vector3.one * (1.04f + Mathf.Sin(Time.unscaledTime * 0.1f) * backgroundDrift * 4f);
        if (credits != null && credits.blocksRaycasts && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ShowCredits(false);
    }

    private void ShowCredits(bool show)
    {
        if (credits == null || busy) return;
        if (creditsRoutine != null) StopCoroutine(creditsRoutine);
        creditsRoutine = StartCoroutine(FadeCredits(show));
    }

    private Coroutine creditsRoutine;

    private IEnumerator FadeCredits(bool show)
    {
        credits.blocksRaycasts = show;
        credits.interactable = show;
        float from = credits.alpha, to = show ? 1f : 0f;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            credits.alpha = Mathf.Lerp(from, to, t / 0.25f);
            yield return null;
        }
        credits.alpha = to;
    }

    private void Play()
    {
        if (busy) return;
        busy = true;
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (fade != null)
        {
            fade.blocksRaycasts = true;
            for (float t = 0f; t < playFade; t += Time.unscaledDeltaTime)
            {
                fade.alpha = t / playFade;
                yield return null;
            }
            fade.alpha = 1f;
        }
        SceneManager.LoadScene(playScene);
    }
}
