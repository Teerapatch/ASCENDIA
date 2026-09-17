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

    [Header("Mouse Camera Sway")]
    public float cameraSwayAmount = 5f; 

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

        // *** เปลี่ยนมายิงเลเซอร์แบบทะลวง (RaycastAll) ***
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        
        PathNode foundNode = null;

        // ค้นหาว่าในบรรดาสิ่งที่เลเซอร์ยิงทะลุไป มีอันไหนเป็นโหนดบ้าง
        foreach (RaycastHit hit in hits)
        {
            PathNode node = hit.collider.GetComponent<PathNode>();
            if (node != null)
            {
                foundNode = node;
                break; // เจอโหนดแล้ว หยุดหา
            }
        }

        // จัดการสถานะ Hover และการคลิก
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
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (mapUIPanel) mapUIPanel.SetActive(false);
        if (selectedNode) selectedNode.SetHover(false);

        Transform player = stateManager.transform;
        
        // เช็คเผื่อลืมใส่เป้าหมาย
        if (selectedNode.climbTarget == null)
        {
            Debug.LogError("ลืมใส่ Climb Target ในก้อนโหนด!");
            yield break;
        }

        float distance = Vector3.Distance(player.position, selectedNode.climbTarget.position);
        
        while (distance > 0.1f)
        {
            player.position = Vector3.MoveTowards(player.position, selectedNode.climbTarget.position, autoClimbSpeed * Time.deltaTime);
            distance = Vector3.Distance(player.position, selectedNode.climbTarget.position);
            yield return null; 
        }

        yield return StartCoroutine(FadeScreen(1f, 1f));
        Debug.Log("✅ โหลดฉาก: " + selectedNode.sceneToLoad);
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