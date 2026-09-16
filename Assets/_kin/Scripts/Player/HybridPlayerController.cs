using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HybridPlayerController : MonoBehaviour
{
    public enum PlayerState { Walking, Climbing }
    
    [Header("Current State")]
    public PlayerState currentState = PlayerState.Walking;

    [Header("Walking Settings")]
    public float walkSpeed = 5f;
    public float wallDetectDistance = 0.8f;

    [Header("Climbing Settings")]
    public float climbSpeed = 5f;
    public float horizontalClimbSpeed = 3f;
    public float minX = -4f;
    public float maxX = 4f;
    public float baseStaminaDrain = 10f;
    public float maxPlayerHeight = 5f; // ความสูงที่จะให้ตัวละครไต่ขึ้นไปถึง ก่อนที่ฉากจะเลื่อนแทน

    [Header("References")]
    public Transform environmentContainer; 
    public Image fadeImage; 

    private float distanceClimbed = 0f;
    private float savedDistance = 0f;
    private Vector3 savedContainerPosition;
    private Vector3 savedPlayerPosition; 
    private bool isHandlingDeath = false;

    private void Start()
    {
        SaveCheckpoint();
    }

    private void Update()
    {
        if (GameManager.Instance == null || isHandlingDeath) return;

        if (currentState == PlayerState.Walking)
        {
            HandleWalking();
        }
        else if (currentState == PlayerState.Climbing)
        {
            HandleClimbing();
            CheckStaminaPenalty();

            // ปุ่ม V ไว้เทสระบบปักหมุด
            if (Input.GetKeyDown(KeyCode.V))
            {
                SaveCheckpoint();
            }
        }
    }

    private void HandleWalking()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 movement = new Vector3(h, 0, v).normalized;
        
        if (movement.magnitude > 0.1f)
        {
            transform.Translate(movement * walkSpeed * Time.deltaTime, Space.World);
            transform.forward = Vector3.Slerp(transform.forward, movement, Time.deltaTime * 10f);
        }

        // ยิง Raycast ไปข้างหน้าเพื่อตรวจจับกำแพง
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, wallDetectDistance))
        {
            if (hit.collider.CompareTag("Wall"))
            {
                // ส่งข้อมูลจุดที่ชน (hit) เข้าไปด้วย เพื่อให้ดึงตัวละครติดกำแพงได้เนียนๆ
                StartClimbing(hit);
            }
        }
    }

    private void StartClimbing(RaycastHit hit)
    {
        currentState = PlayerState.Climbing;
        
        // 1. บังคับให้ตัวละครหันหน้าเข้าหากำแพงตามองศาของกำแพงจริงๆ
        transform.forward = -hit.normal; 
        
        // 2. ดึงตัวให้ชิดกำแพงในระยะที่พอดี (แกน X, Z) 
        // 3. **รักษาความสูงแกน Y ไว้เท่าเดิม (ไม่วาร์ปขึ้นไปแล้ว เริ่มไต่จากพื้นจริงๆ)**
        float capsuleRadius = 0.6f; // ระยะครึ่งนึงของตัวละครกันจมกำแพง
        Vector3 stickToWallPos = hit.point + (hit.normal * capsuleRadius);
        transform.position = new Vector3(stickToWallPos.x, transform.position.y, stickToWallPos.z);

        Debug.Log("💥 เกาะกำแพงแล้ว! กด W เพื่อเริ่มไต่ขึ้นจากพื้น");
    }

    private void HandleClimbing()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // ขยับซ้าย-ขวาบนกำแพง
        Vector3 newPos = transform.position + new Vector3(h, 0, 0) * horizontalClimbSpeed * Time.deltaTime;
        newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        transform.position = newPos;

        if (v > 0.1f)
        {
            // ถ้าความสูงตัวละครยังไม่ถึงเป้าหมาย ให้เลื่อน "ตัวละคร" ขึ้น
            if (transform.position.y < maxPlayerHeight)
            {
                transform.Translate(Vector3.up * climbSpeed * Time.deltaTime, Space.World);
            }
            // ถ้าความสูงถึงเป้าหมายแล้ว ให้สลับไปดึง "ฉาก" ลงมาแทนแบบ Subway Surfers
            else
            {
                environmentContainer.Translate(Vector3.down * climbSpeed * Time.deltaTime);
            }
            
            distanceClimbed += climbSpeed * Time.deltaTime;
            DrainStamina();
        }
        else if (v < -0.1f)
        {
            // การปีนลง: ให้เช็คว่าฉากอยู่ชิดพื้นหรือยัง ถ้ายังให้ดึงฉากขึ้น ถ้าชิดแล้วค่อยดึงตัวละครลง (แต่สำหรับ Prototype อาจจะให้ดึงฉากขึ้นอย่างเดียวก่อนได้)
            environmentContainer.Translate(Vector3.up * climbSpeed * Time.deltaTime);
            distanceClimbed -= climbSpeed * Time.deltaTime;
            DrainStamina();
        }
    }

    private void DrainStamina()
    {
        PlayerData data = GameManager.Instance.playerData;
        float weightModifier = data.weight * 0.1f;
        float drain = (baseStaminaDrain + weightModifier) * Time.deltaTime;
        
        data.stamina -= drain;
        if (data.stamina <= 0) data.stamina = 0;
    }

    public void SaveCheckpoint()
    {
        savedDistance = distanceClimbed;
        savedContainerPosition = environmentContainer.position;
        savedPlayerPosition = transform.position; 
        Debug.Log("📌 ปักหมุด Checkpoint เรียบร้อย!");
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
        Debug.Log("💀 Stamina หมด! ตัดจอดำลงโทษ...");

        if (fadeImage != null) fadeImage.color = new Color(0, 0, 0, 1);
        GameManager.Instance.UsePiton();
        
        yield return new WaitForSeconds(1f); 

        GameManager.Instance.playerData.stamina = GameManager.Instance.playerData.maxStamina;
        environmentContainer.position = savedContainerPosition;
        transform.position = savedPlayerPosition;
        distanceClimbed = savedDistance;

        if (fadeImage != null) fadeImage.color = new Color(0, 0, 0, 0);

        if (GameManager.Instance.playerData.piton <= 0)
        {
            Debug.Log("GAME OVER - หมุดหมด!");
            currentState = PlayerState.Walking; 
            transform.position = new Vector3(0, 0.5f, -5f); // ถอยหลังกลับไปตั้งหลักที่พื้น
        }

        isHandlingDeath = false;
    }
}