using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class CampManager : MonoBehaviour
{
    [Header("Core References")]
    public PlayerData playerData;
    
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject loadoutPanel; 
    public GameObject forgePanel;

    [Header("Resting & Progression System")]
    public TextMeshProUGUI systemMessageText; 
    public string climbSceneName = "ClimbScene";
    public float delayBeforeLoadScene = 1.5f;

    private GameObject currentActivePanel;
    private bool isTransitioning = false; 

    private void Start()
    {
        if (playerData == null && GameManager.Instance != null)
        {
            playerData = GameManager.Instance.playerData;
        }

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (loadoutPanel != null) loadoutPanel.SetActive(false);
        if (forgePanel != null) forgePanel.SetActive(false);
        if (systemMessageText != null) systemMessageText.gameObject.SetActive(false);

        currentActivePanel = mainMenuPanel;
        if (currentActivePanel != null)
        {
            currentActivePanel.SetActive(true);
            CanvasGroup cg = GetOrAddCanvasGroup(currentActivePanel);
            cg.alpha = 1f;
            currentActivePanel.transform.localScale = Vector3.one;
        }

        if (playerData != null)
        {
            // 🌟 เช็คว่าพอกดออกจากแคมป์นี้แล้ว จะเข้าสู่ช่วงสุดท้ายที่ต้องเจอบอสใหญ่หรือไม่
            bool isLastCampBeforeBoss = (playerData.currentCampLevel + 1 >= playerData.finalCampLevel);

            if (isLastCampBeforeBoss)
            {
                ShowSystemMessage($"⚠️ FINAL CAMP (Camp {playerData.currentCampLevel}) - Prepare yourself! The BOSS awaits at the top of the next climb!");
            }
            else
            {
                ShowSystemMessage($"Welcome to Base Camp (Camp {playerData.currentCampLevel} / {playerData.finalCampLevel})");
            }
        }
    }

    public void ShowPanel(GameObject panelToShow)
    {
        if (isTransitioning || currentActivePanel == panelToShow) return;
        StartCoroutine(AnimatePanelTransition(currentActivePanel, panelToShow));
    }

    private IEnumerator AnimatePanelTransition(GameObject panelToHide, GameObject panelToShow)
    {
        isTransitioning = true;
        float duration = 0.2f; 

        if (panelToHide != null)
        {
            CanvasGroup hideCg = GetOrAddCanvasGroup(panelToHide);
            Vector3 startScale = Vector3.one * 1.01f; 
            Vector3 endScale = Vector3.one * 0.8f; 
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = 1f - Mathf.Pow(1f - t, 3f); 

                hideCg.alpha = Mathf.Lerp(1f, 0f, smoothT);
                panelToHide.transform.localScale = Vector3.Lerp(startScale, endScale, smoothT);
                yield return null;
            }
            
            panelToHide.SetActive(false);
        }

        currentActivePanel = panelToShow;

        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
            CanvasGroup showCg = GetOrAddCanvasGroup(panelToShow);
            Vector3 startScale = Vector3.one * 0.8f; 
            Vector3 endScale = Vector3.one * 1.01f; 
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = 1f - Mathf.Pow(1f - t, 3f);

                showCg.alpha = Mathf.Lerp(0f, 1f, smoothT);
                panelToShow.transform.localScale = Vector3.Lerp(startScale, endScale, smoothT);
                yield return null;
            }
            
            showCg.alpha = 1f;
            panelToShow.transform.localScale = endScale;
        }

        isTransitioning = false; 
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
    {
        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if (cg == null) cg = obj.AddComponent<CanvasGroup>();
        return cg;
    }

    public void CloseToMainMenu()
    {
        ShowPanel(mainMenuPanel);
    }

    public void OnClickRest()
    {
        if (isTransitioning || playerData == null) return;
        StartCoroutine(RestAndProceedRoutine());
    }

    private IEnumerator RestAndProceedRoutine()
    {
        isTransitioning = true;

        playerData.stamina = playerData.maxStamina;
        playerData.currentWeaponDUR = playerData.maxWeaponDUR;

        playerData.ReachNextCamp();
        playerData.currentFloor = 1;

        // เทกระเป๋าทิ้งที่แคมป์ เริ่มปีนช่วงใหม่ด้วยกระเป๋าว่าง (เหมือนพฤติกรรมเดิมก่อนย้ายกระเป๋าไป PlayerData)
        playerData.inventorySlots.Clear();

        if (playerData.IsFinalBossStage)
        {
            ShowSystemMessage("Resting Complete! Entering the FINAL CLIMB towards the BOSS...");
        }
        else
        {
            ShowSystemMessage($"Resting Complete! Moving to Stage {playerData.currentCampLevel}...");
        }

        yield return new WaitForSeconds(delayBeforeLoadScene);

        SceneManager.LoadScene(climbSceneName);
    }

    public void ShowSystemMessage(string msg)
    {
        if (systemMessageText != null) 
        {
            systemMessageText.gameObject.SetActive(true);
            systemMessageText.text = msg;
        }
        Debug.Log(msg);
    }
}