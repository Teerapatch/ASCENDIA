using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(PlayerStateManager), typeof(PlayerCameraController))]
public class PlayerPenaltySystem : MonoBehaviour
{
    [Header("UI References")]
    public Image fadeImage;
    public PitonUIController pitonUI;

    [Header("Piton (ตัวช่วยตอน Stamina หมด)")]
    [Tooltip("Stamina หมดกลางกำแพงแล้วเสีย Piton 1 อัน ฟื้น Stamina เต็ม (เพดานที่ลดจากความล้ายังอยู่) / ไม่มี Piton เหลือ = ล้าตาย จบเกม")]
    public float pitonFadeDuration = 0.6f;
    public string exhaustedTitle = "EXHAUSTED";
    [TextArea] public string exhaustedMessage = "Your stamina ran out and no pitons were left.\nThe climb ends here.";

    private PlayerStateManager stateManager;
    private PlayerCameraController camController;
    // เวลาที่ออกแรงต่อเนื่อง พักแล้วค่อย ๆ ลด
    private float exertionTime;
    private bool handlingExhaustion;

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
            float v = PlayerStateManager.InputLocked ? 0f : Input.GetAxisRaw("Vertical");
            if (v != 0)
            {
                DrainStamina();

                // ปีนไม่หยุดนานเกิน fatigueDelay: เริ่มเหนื่อย Stamina สูงสุดค่อย ๆ ลด
                PlayerData climbing = GameManager.Instance.playerData;
                exertionTime += Time.deltaTime;
                if (exertionTime > climbing.fatigueDelay) climbing.AddFatigue(climbing.fatigueRate * Time.deltaTime);
            }
            else exertionTime = Mathf.Max(0f, exertionTime - Time.deltaTime * 2f);
        }
        else if (stateManager.currentState == PlayerStateManager.State.Walking)
        {
            // ยืน/เดินบนพื้นเรียบ: ฟื้น Stamina
            PlayerData data = GameManager.Instance.playerData;
            data.stamina = Mathf.Min(data.EffectiveMaxStamina, data.stamina + data.staminaRegenOnGround * Time.deltaTime);
            exertionTime = Mathf.Max(0f, exertionTime - Time.deltaTime * 2f);
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
            if (!handlingExhaustion) StartCoroutine(HandleExhaustion());
        }
    }

    // Stamina หมดกลางกำแพง: มี Piton -> ตอกหมุดพัก เสีย 1 อัน ฟื้น Stamina เต็มเพดานปัจจุบัน แล้วปีนต่อจากจุดเดิม
    // ไม่มี Piton เหลือ -> ล้าตาย จบเกม (Piton มีจำกัดทั้งเกม หาเพิ่มหรือฟื้นที่ Camp ไม่ได้)
    private IEnumerator HandleExhaustion()
    {
        handlingExhaustion = true;
        stateManager.ChangeState(PlayerStateManager.State.Penalty);
        PlayerData data = GameManager.Instance.playerData;

        if (data.piton <= 0)
        {
            Debug.Log("💀 GAME OVER - Stamina หมดและไม่มี Piton เหลือ ล้าตาย!");
            yield return StartCoroutine(FadeScreen(1f, 1.5f));
            SoundManager.Play(Sfx.Defeat);
            if (GameExitMenu.Instance != null) GameExitMenu.Instance.ShowGameOver(exhaustedTitle, exhaustedMessage);
            yield break; // ค้างอยู่ในสถานะ Penalty ผู้เล่นขยับไม่ได้อีก
        }

        yield return StartCoroutine(FadeScreen(1f, pitonFadeDuration));

        GameManager.Instance.UsePiton();
        SoundManager.Play(Sfx.PitonLost);
        data.stamina = data.EffectiveMaxStamina; // ฟื้นเต็ม แต่ไม่คืนส่วนที่หายไปจากความล้า (fatigue)
        exertionTime = 0f;
        Debug.Log($"🪝 Stamina หมด! ใช้ Piton 1 อัน ฟื้น Stamina เป็น {data.stamina:0} (เหลือ Piton {data.piton})");

        // UI ผิดพลาดต้องไม่ทำให้ Coroutine นี้หยุดกลางทาง (ไม่งั้นค้างอยู่ในสถานะ Penalty บังคับตัวละครต่อไม่ได้)
        try { if (pitonUI != null) pitonUI.ShowLostPitonAlert(data.piton); }
        catch (System.Exception e) { Debug.LogException(e); }

        yield return new WaitForSeconds(0.4f);
        yield return StartCoroutine(FadeScreen(0f, pitonFadeDuration));

        if (stateManager.currentState == PlayerStateManager.State.Penalty) stateManager.ChangeState(PlayerStateManager.State.Climbing);
        handlingExhaustion = false;
    }

    private IEnumerator FadeScreen(float targetAlpha, float duration)
    {
        // FadeImage เดิมอยู่ใน Canvas ของ HUD เก่าที่ถูกปิด (มองไม่เห็น) ใช้ภาพดำ Overlay ของ ScreenFader แทน
        if (fadeImage == null || !fadeImage.isActiveAndEnabled) fadeImage = ScreenFader.Image;

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
