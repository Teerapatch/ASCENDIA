using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class NodeSelectionManager : MonoBehaviour
{
    [Header("UI & Camera")]
    public GameObject mapUIPanel; 
    public GameObject nodeCam; 
    public Image fadeImage; 

    [Header("Player Systems")]
    public PlayerStateManager stateManager; 
    public PlayerCameraController camController; 
    public float autoClimbSpeed = 4f; 
    
    // *** เพิ่มตัวแปรเวลารอกล้องตรงนี้ ***
    [Tooltip("เวลารอให้กล้องสลับเสร็จ ก่อนที่ตัวละครจะปีนหนีไป")]
    public float cameraBlendWaitTime = 2f; 

    [Header("Mouse Camera Sway")]
    public float cameraSwayAmount = 5f; 

    [Header("Custom Cursor")]
    public Texture2D circleCursor; 
    public Vector2 cursorHotspot = new Vector2(16, 16); 

    private PathNode hoveredNode;
    private bool isSelecting = false;
    private Quaternion originalNodeCamRot; 

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isSelecting)
        {
            isSelecting = true;
            stateManager.ChangeState(PlayerStateManager.State.NodeSelection); 
            
            if (mapUIPanel) mapUIPanel.SetActive(true); 

            camController.enabled = false;

            Cursor.SetCursor(circleCursor, cursorHotspot, CursorMode.Auto);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (nodeCam != null)
            {
                originalNodeCamRot = nodeCam.transform.rotation;
            }

            camController.walkCam.SetActive(false);
            camController.climbCam.SetActive(false);
            nodeCam.SetActive(true); 
        }
    }

    private void Update()
    {
        if (!isSelecting) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (nodeCam != null)
        {
            float mouseX = (Input.mousePosition.x / Screen.width) - 0.5f;
            float mouseY = (Input.mousePosition.y / Screen.height) - 0.5f;
            
            Quaternion swayRot = Quaternion.Euler(-mouseY * cameraSwayAmount, mouseX * cameraSwayAmount, 0);
            nodeCam.transform.rotation = Quaternion.Lerp(nodeCam.transform.rotation, originalNodeCamRot * swayRot, Time.deltaTime * 5f);
        }

        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        
        PathNode foundNode = null;

        foreach (RaycastHit hit in hits)
        {
            PathNode node = hit.collider.GetComponent<PathNode>();
            if (node != null)
            {
                foundNode = node;
                break; 
            }
        }

        if (foundNode != null)
        {
            if (hoveredNode != foundNode)
            {
                if (hoveredNode) hoveredNode.SetHover(false); 
                hoveredNode = foundNode;
                hoveredNode.SetHover(true); 
            }

            if (Input.GetMouseButtonDown(0))
            {
                StartCoroutine(ConfirmNode(hoveredNode));
            }
        }
        else 
        {
            ClearHover(); 
        }
    }

    private void ClearHover()
    {
        if (hoveredNode) 
        { 
            hoveredNode.SetHover(false); 
            hoveredNode = null; 
        }
    }


private IEnumerator ConfirmNode(PathNode selectedNode)
    {
        isSelecting = false;
        stateManager.ChangeState(PlayerStateManager.State.AutoClimbing); 
        
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (mapUIPanel) mapUIPanel.SetActive(false);

        // 1. สลับกล้องกลับมาที่มุมปีน
        if (nodeCam) nodeCam.SetActive(false);
        if (camController.climbCam) camController.climbCam.SetActive(true);

        if (selectedNode.climbTarget == null)
        {
            Debug.LogError("ลืมใส่ Climb Target ในก้อนโหนด!");
            yield break;
        }

        // 2. จำพิกัดเป้าหมาย และชื่อฉากที่จะไป
        Vector3 finalDestination = selectedNode.climbTarget.position;
        string nextScene = selectedNode.sceneToLoad;

        // *** 3. กวาดหาโหนดทั้งหมด แล้วเรียกใช้ลูกเล่น "หดตัวสลายไป" ***
        PathNode[] allNodes = FindObjectsOfType<PathNode>();
        foreach (PathNode n in allNodes)
        {
            // ซ่อนข้อความ 3D Text ก่อน (ถ้ามี) จะได้ดูไม่รกตอนก้อนมันหมุน
            if (n.nodeText != null) 
            {
                n.nodeText.gameObject.SetActive(false);
            }
            
            // สั่งให้โหนดนี้เริ่มแสดงอนิเมชั่นหดตัว
            StartCoroutine(ShrinkAndDestroy(n.gameObject));
        }

        // 4. หยุดรอให้กล้องแพนเสร็จ (ระหว่างนี้โหนดก็จะกำลังหมุนหดตัวไปด้วยพอดี)
        yield return new WaitForSeconds(cameraBlendWaitTime);

        Transform player = stateManager.transform;
        float distance = Vector3.Distance(player.position, finalDestination);
        
        // 5. ตัวละครปีนออกนอกเฟรม
        while (distance > 0.1f)
        {
            player.position = Vector3.MoveTowards(player.position, finalDestination, autoClimbSpeed * Time.deltaTime);
            distance = Vector3.Distance(player.position, finalDestination);
            yield return null; 
        }

        yield return StartCoroutine(FadeScreen(1f, 1f));
        Debug.Log("✅ โหลดฉาก: " + nextScene);
    }

    // ========================================================
    // ฟังก์ชันลูกเล่น: หมุนควงสว่านแล้วหดตัวเล็กลงจนหายไป
    // ========================================================
    private IEnumerator ShrinkAndDestroy(GameObject targetObj)
    {
        float duration = 0.4f; // ความเร็วในการหดตัว (0.4 วินาที)
        float time = 0;
        Vector3 startScale = targetObj.transform.localScale;

        while (time < duration)
        {
            if (targetObj == null) yield break;

            time += Time.deltaTime;
            float progress = time / duration;

            // 1. ค่อยๆ ลดสเกลจากขนาดเดิมไปจนเหลือ 0 (Vector3.zero)
            targetObj.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
            
            // 2. สั่งให้ก้อนโหนดหมุนรอบตัวเองเร็วๆ ไปด้วย
            targetObj.transform.Rotate(Vector3.up * 800f * Time.deltaTime); 

            yield return null;
        }

        // พอหดจนเหลือ 0 ปุ๊บ ก็ค่อยลบโมเดลทิ้งจริงๆ
        if (targetObj != null)
        {
            Destroy(targetObj);
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