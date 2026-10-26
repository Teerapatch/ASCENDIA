using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// หน้าจอเริ่ม/จบการต่อสู้
//  - ถูกเรียกผ่าน BattleSession: ข้ามหน้า Start, ปุ่ม Restart กลายเป็น Continue, ซ่อน Quit
//    และกลับฉากที่เรียกเองอัตโนมัติหลัง autoReturnDelay วินาที
//  - เปิดฉากนี้ตรงๆ (ทดสอบใน Editor): ทำงานแบบเดิม มีหน้า Start และ Restart = โหลดฉากซ้ำ
public class TempGameFlowUI : MonoBehaviour
{
    public static TempGameFlowUI Instance;

    [Header("UI Panels")]
    public GameObject startPanel;
    public GameObject gameOverPanel;
    public GameObject victoryPanel;

    [Header("Result Reveal (หน่วงก่อนโชว์หน้าสรุปผล)")]
    [Tooltip("รอกี่วินาทีหลังศัตรูตัวสุดท้ายตาย ก่อนโชว์หน้า Victory/รางวัล (ให้เห็นศัตรูตาย/เอฟเฟกต์จบก่อน)")]
    public float victoryRevealDelay = 1.5f;
    [Tooltip("รอกี่วินาทีหลังอาวุธชิ้นสุดท้ายพัง ก่อนโชว์หน้า Game Over")]
    public float defeatRevealDelay = 1f;

    [Header("Session Mode (ถูกเรียกจากฉากอื่น เช่น ClimbScene)")]
    [Tooltip("กลับฉากที่เรียกอัตโนมัติหลังโชว์ผลกี่วินาที (0 = รอผู้เล่นกดปุ่ม) เผื่อเวลาให้อ่านหน้าสรุปผล")]
    public float autoReturnDelay = 6f;
    public string continueLabel = "Continue";

    private BattleOutcome lastOutcome = BattleOutcome.Defeat;
    private bool isReturning = false;
    private TMP_Text continueButtonText;

    private void Awake()
    {
        Instance = this;

        gameOverPanel.SetActive(false);
        victoryPanel.SetActive(false);

        if (BattleSession.IsActive)
        {
            startPanel.SetActive(false);
            Time.timeScale = 1f;
        }
        else
        {
            // หยุดเวลา (Pause) ไว้ก่อน จนกว่าจะกดเริ่ม
            startPanel.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    private void OnEnable()
    {
        CombatManager.OnBattleEnded += HandleBattleEnded;
    }

    private void OnDisable()
    {
        CombatManager.OnBattleEnded -= HandleBattleEnded;
    }

    private void HandleBattleEnded(BattleSummary summary)
    {
        StartCoroutine(ShowResultAfterDelay(summary));
    }

    // CombatManager เปลี่ยน State เป็น Victory/GameOver ไปแล้ว ระหว่างรอผู้เล่นจึงกดสั่งการอะไรไม่ได้
    private IEnumerator ShowResultAfterDelay(BattleSummary summary)
    {
        float delay = summary.outcome == BattleOutcome.Victory ? victoryRevealDelay : defeatRevealDelay;

        // ซ่อนเมนูสั่งการ/สกิลที่อาจค้างอยู่ ระหว่างรอ
        if (PlayerUIManager.Instance != null) PlayerUIManager.Instance.HideAllMenus();
        if (ControlPromptUI.Instance != null) ControlPromptUI.Instance.HidePrompt();

        // ใช้เวลาจริง เพราะ Hitstop อาจหยุด timeScale ไว้ตอนตีตัวสุดท้าย
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        ShowResult(summary);
    }

    // ==========================================
    // ปุ่มต่างๆ (เอาไปผูกกับปุ่มใน Unity)
    // ==========================================

    // ฟังก์ชันสำหรับปุ่ม "Start Game" บนหน้า Start Panel
    public void OnClickStartGame()
    {
        startPanel.SetActive(false);

        // เดินเวลาให้กลับมาเป็นปกติ
        Time.timeScale = 1f;
    }

    // ฟังก์ชันสำหรับปุ่มบนหน้า Game Over / Victory
    // (ในโหมด Session ปุ่มนี้ถูกเปลี่ยนชื่อเป็น "Continue" = กลับไปปีนต่อทันที)
    public void OnClickRestart()
    {
        Time.timeScale = 1f; // เผื่อเผลอหยุดเวลาไว้ ให้เดินปกติ

        if (BattleSession.IsActive)
        {
            ReturnToCaller();
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    // ส่งผลการต่อสู้กลับไปให้ฉากที่เรียก (เช่น ClimbScene) กันกดซ้ำ/นับถอยหลังชนกัน
    private void ReturnToCaller()
    {
        if (isReturning) return;
        isReturning = true;

        Time.timeScale = 1f;
        BattleSession.Finish(lastOutcome);
    }

    // ฟังก์ชันสำหรับปุ่ม "Quit"
    public void OnClickQuit()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }

    // ==========================================
    // ถูกเรียกเมื่อ CombatManager แจ้งว่าการต่อสู้จบ
    // ==========================================
    private void ShowResult(BattleSummary summary)
    {
        lastOutcome = summary.outcome;

        GameObject panel = summary.outcome == BattleOutcome.Victory ? victoryPanel : gameOverPanel;
        panel.SetActive(true);

        // หน้าสรุปผล (ของรางวัล + สถิติ) ถ้าไม่ได้วาง BattleSummaryUI ไว้ใน Panel ก็เพิ่มให้เลย
        BattleSummaryUI summaryUI = panel.GetComponent<BattleSummaryUI>();
        if (summaryUI == null) summaryUI = panel.AddComponent<BattleSummaryUI>();
        summaryUI.Show(summary, BattleSession.Current != null ? BattleSession.Current.defeatPenaltyText : null);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (BattleSession.IsActive)
        {
            SetupSessionButtons(panel);
            if (autoReturnDelay > 0f) StartCoroutine(AutoReturnRoutine());
        }
    }

    // ปุ่มในฉากถูกทำไว้สำหรับเล่นเดี่ยว (Play Again / Quit) ตอนถูกเรียกจากฉากอื่นให้เหลือแค่ปุ่ม Continue
    private void SetupSessionButtons(GameObject panel)
    {
        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                string method = button.onClick.GetPersistentMethodName(i);

                if (method == nameof(OnClickQuit))
                {
                    button.gameObject.SetActive(false);
                }
                else if (method == nameof(OnClickRestart))
                {
                    continueButtonText = button.GetComponentInChildren<TMP_Text>(true);
                    if (continueButtonText != null) continueButtonText.text = continueLabel;
                }
            }
        }
    }

    private IEnumerator AutoReturnRoutine()
    {
        // ใช้เวลาจริง เพราะ Hitstop อาจหยุด timeScale ไว้ตอนตีตัวสุดท้าย
        float remaining = autoReturnDelay;
        while (remaining > 0f && !isReturning)
        {
            if (continueButtonText != null) continueButtonText.text = $"{continueLabel} ({Mathf.CeilToInt(remaining)})";
            yield return new WaitForSecondsRealtime(0.1f);
            remaining -= 0.1f;
        }

        ReturnToCaller();
    }
}
