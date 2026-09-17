using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(PlayerStateManager), typeof(PlayerCameraController))]
public class PlayerPenaltySystem : MonoBehaviour
{
    [Header("Settings")]
    public float baseStaminaDrain = 10f;

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
        
        // ลด Stamina เฉพาะตอนขยับขึ้นลงในโหมดปีน
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
        float weightModifier = data.weight * 0.1f;
        float drain = (baseStaminaDrain + weightModifier) * Time.deltaTime;

        data.stamina -= drain;

        if (data.stamina <= 0)
        {
            data.stamina = 0;
            StartCoroutine(HandleDeathPenalty());
        }
    }

    private IEnumerator HandleDeathPenalty()
    {
        // 1. เปลี่ยน State เป็น Penalty เพื่อล็อคไม่ให้ขยับได้
        stateManager.ChangeState(PlayerStateManager.State.Penalty);

        // 2. Fade Out จอดำ
        yield return StartCoroutine(FadeScreen(1f, 1f));

        // 3. หักหมุดและเติม Stamina คืน (ไม่มีการขยับตำแหน่งใดๆ ทั้งสิ้น)
        GameManager.Instance.UsePiton();
        GameManager.Instance.playerData.stamina = GameManager.Instance.playerData.maxStamina;
        Debug.Log("💔 เสียหมุด 1 อัน!");

        if (pitonUI != null) 
        {
            pitonUI.ShowLostPitonAlert(GameManager.Instance.playerData.piton);
        }

        yield return new WaitForSeconds(0.5f);

        // 4. Fade In จอสว่าง
        yield return StartCoroutine(FadeScreen(0f, 1f));

        // 5. ตรวจสอบว่าหมุดหมดหรือยัง
        if (GameManager.Instance.playerData.piton <= 0)
        {
            Debug.Log("💀 GAME OVER - หมุดหมด!");
            
            // ส่งกลับไปที่พื้น เริ่มใหม่
            stateManager.ChangeState(PlayerStateManager.State.Walking);
            transform.position = new Vector3(0, 0.5f, -5f); 
            camController.SwitchToWalkCam();
        }
        else
        {
            // ถ้ายังมีหมุดเหลือ ให้เปลี่ยน State กลับเป็น Climbing เพื่อลุยต่อจากจุดเดิมเลย
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