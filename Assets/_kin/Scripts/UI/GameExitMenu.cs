using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// หน้าต่างออกเกม (กด Esc ได้ทุกฉาก) และหน้าจบ Build ตกแต่งได้ที่ Assets/Resources/GameExitMenu.prefab
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

        quitWindow.SetActive(false);
        endWindow.SetActive(false);
        group.alpha = 0f;
        SetInteractive(false);
    }

    public void ShowBuildEnd()
    {
        buildEnded = true;
        Open(endWindow);
    }

    private void Update()
    {
        if (!buildEnded && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
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
        return PlayerCombatController.Instance != null && PlayerCombatController.Instance.IsChoosingFromMenu;
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
