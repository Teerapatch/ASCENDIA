using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// หน้าต่างออกเกม (กด Esc ได้ทุกฉาก) และหน้าจบรัน (ชนะบอสยอดเขา / Game Over) ตกแต่งได้ที่ Assets/Resources/GameExitMenu.prefab
// ปุ่ม MAIN MENU (กลับหน้า Title) ถ้า Prefab ยังไม่มี จะก๊อปปุ่มเดิมมาใส่ให้ตอนเริ่ม: QuitWindow/Btn_MainMenu, EndWindow/Btn_Title
// ทำงานก่อนสคริปต์อื่นเพื่ออ่านสถานะการต่อสู้ก่อนที่ Esc ของเฟรมเดียวกันจะเปลี่ยนมัน
[DefaultExecutionOrder(-1000)]
public class GameExitMenu : MonoBehaviour
{
    private const string ResourcePath = "GameExitMenu";

    public static GameExitMenu Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.isOpen;

    public CanvasGroup group;
    public GameObject quitWindow;
    public GameObject endWindow;
    public Button resumeButton;
    public Button quitButton;
    public Button endQuitButton;
    public float fadeDuration = 0.2f;

    private bool isOpen;
    private bool buildEnded;
    private Button quitMainMenuButton;
    private string defaultEndTitle, defaultEndMessage;
    private float restoreTimeScale = 1f;
    private CursorLockMode restoreLockState;
    private bool restoreCursorVisible;

    // สร้างเองตอนเริ่มเกม ฉากต่าง ๆ จึงไม่ต้องตั้งค่าอะไรเพิ่ม
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (Instance != null) return;
        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab != null) DontDestroyOnLoad(Instantiate(prefab));
    }

    private void Awake()
    {
        Instance = this;
        resumeButton.onClick.AddListener(Close);
        quitButton.onClick.AddListener(QuitGame);
        endQuitButton.onClick.AddListener(QuitGame);

        // ปุ่ม SETTINGS ในหน้าต่างพัก (Prefab: QuitWindow/Btn_Settings ถ้ามี) เปิดหน้า Settings ซ้อนด้านบน
        Transform settings = quitWindow.transform.Find("Btn_Settings");
        Button settingsButton = settings != null ? settings.GetComponent<Button>() : null;
        if (settingsButton != null) settingsButton.onClick.AddListener(() => SettingsMenu.Open());

        // ปุ่มกลับหน้า Title: หน้าต่างพัก (ซ่อนตอนอยู่หน้า Title) และหน้าจบรัน
        quitMainMenuButton = EnsureButton(quitWindow.transform, "Btn_MainMenu", settingsButton != null ? settingsButton : quitButton, "MAIN MENU");
        if (quitMainMenuButton != null)
        {
            quitMainMenuButton.onClick.AddListener(GoToTitle);
            // จัดแถวกลาง: SETTINGS ซ้าย MAIN MENU ขวา (ข้อความขยับขึ้นเล็กน้อย)
            if (settingsButton != null)
            {
                Place(settingsButton.transform, new Vector2(-160f, -20f));
                Place(quitMainMenuButton.transform, new Vector2(160f, -20f));
                Transform message = quitWindow.transform.Find("Message");
                if (message != null) Place(message, new Vector2(0f, 45f));
            }
        }
        Button endTitle = EnsureButton(endWindow.transform, "Btn_Title", endQuitButton, "MAIN MENU");
        if (endTitle != null)
        {
            endTitle.onClick.AddListener(GoToTitle);
            Vector2 exitPos = ((RectTransform)endQuitButton.transform).anchoredPosition;
            Place(endTitle.transform, new Vector2(-190f, exitPos.y));
            Place(endQuitButton.transform, new Vector2(190f, exitPos.y));
        }
        defaultEndTitle = GetEndText("Title");
        defaultEndMessage = GetEndText("Message");

        quitWindow.SetActive(false);
        endWindow.SetActive(false);
        group.alpha = 0f;
        SetInteractive(false);
    }

    // หาปุ่มชื่อ name ถ้าไม่มีก๊อปจาก source แล้วเปลี่ยนป้าย
    private static Button EnsureButton(Transform parent, string name, Button source, string label)
    {
        Transform found = parent.Find(name);
        if (found == null)
        {
            if (source == null) return null;
            GameObject copy = Instantiate(source.gameObject, parent, false);
            copy.name = name;
            found = copy.transform;
            Button cloned = copy.GetComponent<Button>();
            cloned.onClick.RemoveAllListeners();
            TMPro.TMP_Text text = copy.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (text != null) text.text = label;
        }
        return found.GetComponent<Button>();
    }

    private static void Place(Transform target, Vector2 position)
    {
        if (target is RectTransform rect) rect.anchoredPosition = position;
    }

    private string GetEndText(string child)
    {
        Transform target = endWindow.transform.Find(child);
        TMPro.TMP_Text label = target != null ? target.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        return label != null ? label.text : null;
    }

    private void GoToTitle()
    {
        if (SceneTransition.IsBusy) return;
        GameFlow.ReturnToTitle();
    }

    // เริ่มรันใหม่ / กลับหน้า Title: ปิดหน้าต่าง ล้างสถานะจบรัน คืนข้อความหน้าจบเดิม
    public void ResetForNewRun()
    {
        buildEnded = false;
        Close();
        quitWindow.SetActive(false);
        endWindow.SetActive(false);
        SetEndText("Title", defaultEndTitle);
        SetEndText("Message", defaultEndMessage);
        Time.timeScale = 1f;
    }

    public void ShowBuildEnd()
    {
        buildEnded = true;
        Open(endWindow);
    }

    // จบเกม (เช่น ล้าตายตอนไม่มี Piton): ใช้หน้าจบ Build เดิม แค่เปลี่ยนหัวข้อและข้อความ (ฟอนต์ไม่มีภาษาไทย ใช้อังกฤษ)
    public void ShowGameOver(string title, string message)
    {
        SetEndText("Title", title);
        SetEndText("Message", message);
        ShowBuildEnd();
    }

    private void SetEndText(string child, string text)
    {
        Transform target = endWindow.transform.Find(child);
        TMPro.TMP_Text label = target != null ? target.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        if (label != null && !string.IsNullOrEmpty(text)) label.text = text;
    }

    private void Update()
    {
        // Esc ระหว่างเปิด Settings = ปิด Settings อย่างเดียว (เฟรมที่มันเพิ่งปิดก็ไม่นับ)
        bool settingsUsedEscape = SettingsMenu.IsOpen || SettingsMenu.LastClosedFrame == Time.frameCount;
        if (!buildEnded && !settingsUsedEscape && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isOpen) Close();
            else if (!IsEscapeUsedByBattle()) Open(quitWindow);
        }

        float target = isOpen ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration));
    }

    // ระบบอื่นตั้งเวลาและเมาส์กลับได้ระหว่างเปิดอยู่ (Hitstop จบ, กล้องล็อกเมาส์) จึงต้องย้ำทุกเฟรม
    private void LateUpdate()
    {
        if (!isOpen) return;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static bool IsEscapeUsedByBattle()
    {
        // หน้าย่อยของ Camp ชุดใหม่ใช้ Esc เป็นปุ่มกลับหน้าหลัก
        return (PlayerCombatController.Instance != null && PlayerCombatController.Instance.IsChoosingFromMenu) || CampReArtUI.IsSubPageOpen;
    }

    private void Open(GameObject window)
    {
        if (!isOpen)
        {
            restoreTimeScale = HitstopManager.IsFrozen ? HitstopManager.BaseTimeScale : Time.timeScale;
            restoreLockState = Cursor.lockState;
            restoreCursorVisible = Cursor.visible;
        }

        isOpen = true;
        // หน้า Title ไม่มีเมนูหลักให้กลับ
        if (quitMainMenuButton != null)
            quitMainMenuButton.gameObject.SetActive(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != GameFlow.TitleScene);
        quitWindow.SetActive(window == quitWindow);
        endWindow.SetActive(window == endWindow);
        SetInteractive(true);
    }

    private void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        SetInteractive(false);
        Cursor.lockState = restoreLockState;
        Cursor.visible = restoreCursorVisible;

        // ถ้า Hitstop ยังไม่จบ มันจะคืนเวลาเองตอนจบ
        if (!HitstopManager.IsFrozen) Time.timeScale = restoreTimeScale;
    }

    private void SetInteractive(bool interactive)
    {
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
