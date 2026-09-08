using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ClimbController : MonoBehaviour
{
    [Header("Climbing Settings (Subway Surfers Style)")]
    public float climbSpeed = 5f;
    public float horizontalSpeed = 3f;
    public float minX = -4f; // ขอบซ้าย
    public float maxX = 4f;  // ขอบขวา

    [Header("Environment References")]
    public Transform environmentContainer; // ตัว Parent ที่จะรวมฉากหน้าผาแล้วเลื่อนลง
    public Image fadeImage; // ภาพสีดำใน Canvas เอาไว้ทำฉากตัด
    
    [Header("Stamina Drain")]
    public float baseStaminaDrain = 10f; 

    private float distanceClimbed = 0f;
    private float savedDistance = 0f;
    private Vector3 savedContainerPosition;
    private bool isHandlingDeath = false;

    private void Start()
    {
        // เซฟจุดเริ่มต้นไว้กันเหนียว
        SaveCheckpoint();
    }

    private void Update()
    {
        if (GameManager.Instance == null || isHandlingDeath) return;

        HandleMovement();
        CheckStaminaPenalty();

        // กด C เพื่อทดสอบการปักหมุดด้วยคีย์บอร์ด (หรือผูกปุ่ม UI เรียกฟังก์ชัน SaveCheckpoint ก็ได้)
        if (Input.GetKeyDown(KeyCode.C))
        {
            SaveCheckpoint();
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // 1. ผู้เล่นขยับซ้าย-ขวาได้ (แกน X)
        Vector3 newPos = transform.position + new Vector3(horizontal, 0, 0) * horizontalSpeed * Time.deltaTime;
        newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        transform.position = newPos;

        // 2. กดขึ้น (W) = เลื่อนฉากหน้าผา "ลง" มาหาตัวละคร
        if (vertical > 0.1f)
        {
            environmentContainer.Translate(Vector3.down * climbSpeed * Time.deltaTime);
            distanceClimbed += climbSpeed * Time.deltaTime;
            DrainStamina();
        }
        // กดลง (S) = เลื่อนฉากหน้าผา "ขึ้น" (ปีนลง)
        else if (vertical < -0.1f)
        {
            environmentContainer.Translate(Vector3.up * climbSpeed * Time.deltaTime);
            distanceClimbed -= climbSpeed * Time.deltaTime;
            DrainStamina();
        }
    }

    private void DrainStamina()
    {
        PlayerData data = GameManager.Instance.playerData;
        float weightModifier = data.weight * 0.1f; // ยิ่งของหนักยิ่งลดไว
        float drain = (baseStaminaDrain + weightModifier) * Time.deltaTime;
        
        data.stamina -= drain;
        if (data.stamina <= 0) data.stamina = 0;
    }

    // ฟังก์ชันปักหมุด
    public void SaveCheckpoint()
    {
        // ในดีไซน์เพื่อนบอกเสียหมุดตอนเชือกหมด ตอนเซฟเลยแค่เก็บตำแหน่งไว้
        savedDistance = distanceClimbed;
        savedContainerPosition = environmentContainer.position;
        Debug.Log("📌 ปักหมุด Checkpoint แล้ว!");
    }

    private void CheckStaminaPenalty()
    {
        if (GameManager.Instance.playerData.stamina <= 0)
        {
            StartCoroutine(PenaltyRoutine());
        }
    }

    private IEnumerator PenaltyRoutine()
    {
        isHandlingDeath = true;
        Debug.Log("💀 Stamina หมด! กำลังลงโทษ...");

        // ตัดเฟสดำ
        if (fadeImage != null) fadeImage.color = new Color(0, 0, 0, 1);

        // หักหมุด (ชีวิต)
        GameManager.Instance.UsePiton();
        
        yield return new WaitForSeconds(1f); // แช่จอดำ 1 วินาที

        // Re-Stamina & กลับจุด Checkpoint ล่าสุด
        GameManager.Instance.playerData.stamina = GameManager.Instance.playerData.maxStamina;
        environmentContainer.position = savedContainerPosition;
        distanceClimbed = savedDistance;

        // เอาจอดำออก
        if (fadeImage != null) fadeImage.color = new Color(0, 0, 0, 0);

        if (GameManager.Instance.playerData.piton <= 0)
        {
            Debug.Log("GAME OVER - หมุดหมดแล้ว!");
            // ถ้าหมุดหมด ค่อยสั่ง SceneManager โหลดฉากแพ้ หรือเด้งกลับแคมป์
        }

        isHandlingDeath = false;
    }
}