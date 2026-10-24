using UnityEngine;
using UnityEngine.SceneManagement;

public class TempGameFlowUI : MonoBehaviour
{
    public static TempGameFlowUI Instance;

    [Header("UI Panels")]
    public GameObject startPanel;
    public GameObject gameOverPanel;
    public GameObject victoryPanel;

    private void Awake()
    {
        Instance = this;

        gameOverPanel.SetActive(false);
        victoryPanel.SetActive(false);

        // ถูกเรียกมาจากฉากอื่นผ่าน BattleSession -> เข้าสู้ทันที ไม่ต้องรอกด Start
        if (BattleSession.HasRequest)
        {
            startPanel.SetActive(false);
            Time.timeScale = 1f;
            return;
        }

        // เปิดฉากนี้ตรงๆ (Standalone) บังคับเปิดหน้า Start แล้วหยุดเวลาไว้ก่อน จนกว่าจะกดเริ่ม
        startPanel.SetActive(true);
        Time.timeScale = 0f;
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

    // ฟังก์ชันสำหรับปุ่ม "Play Again" บนหน้า Game Over / Victory
    public void OnClickRestart()
    {
        // โหลด Scene ปัจจุบันซ้ำอีกรอบ
        Time.timeScale = 1f; // เผื่อเผลอหยุดเวลาไว้ ให้เดินปกติ
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ฟังก์ชันสำหรับปุ่ม "Quit"
    public void OnClickQuit()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }

    // ==========================================
    // ถูกเรียกจาก CombatManager
    // ==========================================

    public void ShowGameOver()
    {
        gameOverPanel.SetActive(true);
        // (Optional) อาจจะปลดเคอร์เซอร์เมาส์เผื่อโดนซ่อนอยู่
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowVictory()
    {
        victoryPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}