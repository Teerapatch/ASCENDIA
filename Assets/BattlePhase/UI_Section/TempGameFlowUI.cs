using UnityEngine;
using UnityEngine.SceneManagement;

// หน้าจอเริ่ม/จบการต่อสู้
//  - ถูกเรียกผ่าน BattleSession: ข้ามหน้า Start และปุ่ม Restart จะพากลับฉากที่เรียกมา
//  - เปิดฉากนี้ตรงๆ (ทดสอบใน Editor): ทำงานแบบเดิม มีหน้า Start และ Restart = โหลดฉากซ้ำ
public class TempGameFlowUI : MonoBehaviour
{
    public static TempGameFlowUI Instance;

    [Header("UI Panels")]
    public GameObject startPanel;
    public GameObject gameOverPanel;
    public GameObject victoryPanel;

    private BattleOutcome lastOutcome = BattleOutcome.Defeat;

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
        CombatManager.OnBattleEnded += ShowResult;
    }

    private void OnDisable()
    {
        CombatManager.OnBattleEnded -= ShowResult;
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
    public void OnClickRestart()
    {
        Time.timeScale = 1f; // เผื่อเผลอหยุดเวลาไว้ ให้เดินปกติ

        if (BattleSession.IsActive)
        {
            // ส่งผลการต่อสู้กลับไปให้ฉากที่เรียก (เช่น ClimbScene)
            BattleSession.Finish(lastOutcome);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
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
    private void ShowResult(BattleOutcome outcome)
    {
        lastOutcome = outcome;

        GameObject panel = outcome == BattleOutcome.Victory ? victoryPanel : gameOverPanel;
        panel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
