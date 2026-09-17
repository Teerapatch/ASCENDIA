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
    public float maxPlayerHeight = 5f; 

    [Header("Cinemachine & Camera Control")]
    public GameObject walkCam;  
    public GameObject climbCam; 
    public Transform cameraLookTarget; 
    public float mouseSensitivity = 3f; 

    [Header("References")]
    public Transform environmentContainer; 
    public Image fadeImage; 

    private float distanceClimbed = 0f;
    private float savedDistance = 0f;
    private Vector3 savedContainerPosition;
    private Vector3 savedPlayerPosition; 
    private bool isHandlingDeath = false;

    // --- ระบบล็อคกล้อง ---
    private Vector3 lookTargetOffset; // เก็บระยะห่างระหว่างคนกับเป้ากล้อง
    private float camPan = 0f; 
    private float camTilt = 0f; 
    private float lockedPan = 0f; // มุมแนวนอนที่ล็อคไว้
    private float lockedTilt = 0f; // มุมแนวตั้งที่ล็อคไว้

    private void Start()
    {
        walkCam.SetActive(true);
        climbCam.SetActive(false);

        // 1. จำระยะห่างและมุมตั้งต้นไว้เป็น "มุมล็อค"
        if (cameraLookTarget != null)
        {
            lookTargetOffset = cameraLookTarget.localPosition;
            lockedPan = cameraLookTarget.eulerAngles.y;
            lockedTilt = cameraLookTarget.eulerAngles.x;
            camPan = lockedPan;
            camTilt = lockedTilt;

            // 2. ปลดเป้ากล้องออกจากการเป็นลูกของ Player จะได้ไม่หมุนตามตอนกด W A S D
            cameraLookTarget.SetParent(null); 
        }

        SaveCheckpoint();
    }

    private void Update()
    {
        if (GameManager.Instance == null || isHandlingDeath) return;

        // อัปเดตให้เป้ากล้องเดินตามตัวละครตลอดเวลา (แต่ไม่หมุนตาม)
        if (cameraLookTarget != null)
        {
            cameraLookTarget.position = transform.position + lookTargetOffset;
        }

        HandleCameraOrbit();

        if (currentState == PlayerState.Walking)
        {
            HandleWalking();
        }
        else if (currentState == PlayerState.Climbing)
        {
            HandleClimbing();
            CheckStaminaPenalty();

            if (Input.GetKeyDown(KeyCode.V))
            {
                SaveCheckpoint();
            }
        }
    }

    private void HandleCameraOrbit()
    {
        if (cameraLookTarget == null) return;

        if (Input.GetMouseButton(1)) // คลิกขวาค้างเพื่อหมุนกล้อง
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            camPan += mouseX;
            camTilt -= mouseY;
            camTilt = Mathf.Clamp(camTilt, -30f, 70f); 

            // หมุนเป้ากล้องแบบ World Space
            cameraLookTarget.rotation = Quaternion.Euler(camTilt, camPan, 0f);
        }
        else 
        {
            // ถ้าปล่อยเมาส์ ให้ค่อยๆ สมูทกล้องกลับมาที่ "มุมล็อค" (ใช้ LerpAngle เพื่อกันบั๊กหมุน 360 องศา)
            camPan = Mathf.LerpAngle(camPan, lockedPan, Time.deltaTime * 10f);
            camTilt = Mathf.LerpAngle(camTilt, lockedTilt, Time.deltaTime * 10f);
            
            cameraLookTarget.rotation = Quaternion.Euler(camTilt, camPan, 0f);
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
            // หมุนเฉพาะตัวละครให้หันตามทิศที่เดิน (กล้องจะไม่หมุนตามแล้ว เพราะปลดลูกออกไปแล้ว)
            transform.forward = Vector3.Slerp(transform.forward, movement, Time.deltaTime * 10f);
        }

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, wallDetectDistance))
        {
            if (hit.collider.CompareTag("Wall"))
            {
                StartClimbing(hit);
            }
        }
    }

    private void StartClimbing(RaycastHit hit)
    {
        currentState = PlayerState.Climbing;
        transform.forward = -hit.normal; 
        
        float capsuleRadius = 0.6f; 
        Vector3 stickToWallPos = hit.point + (hit.normal * capsuleRadius);
        transform.position = new Vector3(stickToWallPos.x, transform.position.y, stickToWallPos.z);

        // --- เซ็ตมุมล็อคใหม่ให้กล้องมองเข้าหากำแพงตรงๆ ---
        lockedPan = transform.eulerAngles.y;
        lockedTilt = 0f; 

        walkCam.SetActive(false);
        climbCam.SetActive(true);

        Debug.Log("💥 เกาะกำแพงแล้ว! สลับเป็นกล้องปีนเขา");
    }

    private void HandleClimbing()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 newPos = transform.position + new Vector3(h, 0, 0) * horizontalClimbSpeed * Time.deltaTime;
        newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        transform.position = newPos;

        if (v > 0.1f)
        {
            if (transform.position.y < maxPlayerHeight)
            {
                transform.Translate(Vector3.up * climbSpeed * Time.deltaTime, Space.World);
            }
            else
            {
                environmentContainer.Translate(Vector3.down * climbSpeed * Time.deltaTime);
            }
            
            distanceClimbed += climbSpeed * Time.deltaTime;
            DrainStamina();
        }
        else if (v < -0.1f)
        {
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
            transform.position = new Vector3(0, 0.5f, -5f); 
            
            // รีเซ็ตมุมกล้องให้มองตรง
            lockedPan = 0f;
            lockedTilt = 0f;

            climbCam.SetActive(false);
            walkCam.SetActive(true);
        }

        isHandlingDeath = false;
    }
}