using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(PlayerStateManager), typeof(PlayerCameraController))]
public class PlayerPenaltySystem : MonoBehaviour
{
    [Header("UI References")]
    public Image fadeImage;
    public PitonUIController pitonUI; 

    private PlayerStateManager stateManager;
    private PlayerCameraController camController;

    private void Awake()
    {
        stateManager = GetComponent<PlayerStateManager>();
        camController = GetComponent<PlayerCameraController>();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        
        if (stateManager.currentState == PlayerStateManager.State.Climbing)
        {
            float v = Input.GetAxisRaw("Vertical");
            if (v != 0)
            {
                DrainStamina();
            }
        }
    }

    private void DrainStamina()
    {
        PlayerData data = GameManager.Instance.playerData;
        
        // ดึงค่า baseStaminaDrain จากไฟล์ PlayerData แทนของเดิม
        float weightModifier = data.weight * 0.1f;
        float drain = (data.baseStaminaDrain + weightModifier) * Time.deltaTime;

        data.stamina -= drain;

        if (data.stamina <= 0)
        {
            data.stamina = 0;
            StartCoroutine(HandleDeathPenalty());
        }
    }

    private IEnumerator HandleDeathPenalty()
    {
        stateManager.ChangeState(PlayerStateManager.State.Penalty);

        yield return StartCoroutine(FadeScreen(1f, 1f));

        GameManager.Instance.UsePiton();
        GameManager.Instance.playerData.stamina = GameManager.Instance.playerData.maxStamina;
        
        if (pitonUI != null) 
        {
            pitonUI.ShowLostPitonAlert(GameManager.Instance.playerData.piton);
        }

        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(FadeScreen(0f, 1f));

        if (GameManager.Instance.playerData.piton <= 0)
        {
            Debug.Log("💀 GAME OVER - หมุดหมด!");
            stateManager.ChangeState(PlayerStateManager.State.Walking);
            transform.position = new Vector3(0, 0.5f, -5f); 
            camController.SwitchToWalkCam();
        }
        else
        {
            stateManager.ChangeState(PlayerStateManager.State.Climbing);
        }
    }

    private IEnumerator FadeScreen(float targetAlpha, float duration)
    {
        if (fadeImage == null) yield break;
        
        Color color = fadeImage.color;
        float startAlpha = color.a;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            fadeImage.color = color;
            yield return null;
        }
    }
}