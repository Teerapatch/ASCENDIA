using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class NodeSelectionManager : MonoBehaviour
{
    [Header("UI & Camera")]
    public GameObject mapUIPanel; 
    public GameObject nodeCam; 
    public Image fadeImage; 

    [Header("Auto UI Generation")]
    public GameObject uiNodePrefab; 
    public Transform uiNodeContainer; 
    
    [Header("Line Connections")]
    public GameObject uiLinePrefab; 
    public Transform uiLineContainer; 
    public float lineThickness = 4f; 
    public Color normalLineColor = new Color(0.8f, 0.8f, 0.8f, 0.5f); 
    public Color auraLineColor = Color.yellow; 

    [Header("Map Progression UI")]
    public RectTransform playerUIIcon; 
    [Tooltip("ระยะเวลาที่ไอคอนจะวิ่งไปหาโหนด (วินาที)")]
    public float uiPlayerMoveDuration = 1.5f; 

    [Header("Player Systems")]
    public PlayerStateManager stateManager; 
    public PlayerCameraController camController; 
    public float autoClimbSpeed = 4f; 
    public float cameraBlendWaitTime = 2f; 

    [Header("Mouse Camera Sway")]
    public float cameraSwayAmount = 5f; 

    [Header("Custom Cursor")]
    public Texture2D circleCursor; 
    public Vector2 cursorHotspot = new Vector2(16, 16); 

    private PathNode hoveredNode;
    private bool isSelecting = false;
    private Quaternion originalNodeCamRot; 
    private Dictionary<PathNode, Image> nodeLines = new Dictionary<PathNode, Image>();
    private Dictionary<PathNode, Image> nodeFutureLines = new Dictionary<PathNode, Image>();

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isSelecting)
        {
            isSelecting = true;
            stateManager.ChangeState(PlayerStateManager.State.NodeSelection); 
            
            if (mapUIPanel) mapUIPanel.SetActive(true); 

            StartCoroutine(GenerateMapUICoroutine());

            camController.enabled = false;
            Cursor.SetCursor(circleCursor, cursorHotspot, CursorMode.Auto);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (nodeCam != null) originalNodeCamRot = nodeCam.transform.rotation;

            camController.walkCam.SetActive(false);
            camController.climbCam.SetActive(false);
            nodeCam.SetActive(true); 
        }
    }

    private IEnumerator GenerateMapUICoroutine()
    {
        PathNode[] allNodes = FindObjectsByType<PathNode>(FindObjectsSortMode.None);
        List<PathNode> currentNodes = new List<PathNode>();
        foreach (PathNode n in allNodes)
        {
            if (Vector3.Distance(transform.position, n.transform.position) < 30f) currentNodes.Add(n);
        }

        currentNodes.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        foreach (Transform child in uiNodeContainer) Destroy(child.gameObject);
        if (uiLineContainer != null) 
        {
            foreach (Transform child in uiLineContainer) Destroy(child.gameObject);
        }

        List<RectTransform> generatedUINodes = new List<RectTransform>();
        nodeLines.Clear(); 
        nodeFutureLines.Clear(); 

        foreach (PathNode node in currentNodes)
        {
            GameObject newUI = Instantiate(uiNodePrefab, uiNodeContainer);
            node.uiNodeTransform = newUI.GetComponent<RectTransform>();
            generatedUINodes.Add(node.uiNodeTransform);
            
            Transform auraUI = newUI.transform.Find("Aura");
            if (auraUI != null) { node.uiMapAura = auraUI.gameObject; node.uiMapAura.SetActive(false); }

            Transform iconUI = newUI.transform.Find("Icon");
            if (iconUI != null && node.nodeIcon != null)
            {
                Image img = iconUI.GetComponent<Image>();
                if (img != null) img.sprite = node.nodeIcon;
            }
        }

        yield return null; 

        if (uiLinePrefab != null && uiLineContainer != null && playerUIIcon != null)
        {
            Vector3[] corners = new Vector3[4];
            mapUIPanel.GetComponent<RectTransform>().GetWorldCorners(corners);
            float bottomY = corners[0].y - 50f; 
            float topY = corners[1].y + 50f;    

            Vector3 pastStart = new Vector3(playerUIIcon.position.x, bottomY, 0);
            DrawUILine(pastStart, playerUIIcon.position, auraLineColor);

            for (int i = 0; i < generatedUINodes.Count; i++)
            {
                RectTransform targetNode = generatedUINodes[i];
                PathNode logicNode = currentNodes[i];

                Image branchLine = DrawUILine(playerUIIcon.position, targetNode.position, normalLineColor);
                nodeLines.Add(logicNode, branchLine); 

                Vector3 futureEnd = new Vector3(targetNode.position.x, topY, 0);
                Image futureLine = DrawUILine(targetNode.position, futureEnd, normalLineColor);
                nodeFutureLines.Add(logicNode, futureLine);
            }
        }
    }

    private Image DrawUILine(Vector3 startPos, Vector3 endPos, Color lineColor)
    {
        GameObject lineObj = Instantiate(uiLinePrefab, uiLineContainer);
        RectTransform lineRect = lineObj.GetComponent<RectTransform>();
        Image lineImg = lineObj.GetComponent<Image>();

        Vector3 dir = endPos - startPos;
        lineRect.position = startPos + (dir / 2f);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        lineRect.rotation = Quaternion.Euler(0, 0, angle);
        float dist = Vector3.Distance(startPos, endPos);
        lineRect.sizeDelta = new Vector2(dist / mapUIPanel.transform.lossyScale.x, lineThickness);

        lineImg.color = lineColor; 
        return lineImg;
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
            if (node != null) { foundNode = node; break; }
        }

        if (foundNode != null)
        {
            if (hoveredNode != foundNode)
            {
                if (hoveredNode) hoveredNode.SetHover(false); 
                hoveredNode = foundNode;
                hoveredNode.SetHover(true); 
            }
            if (Input.GetMouseButtonDown(0)) StartCoroutine(ConfirmNode(hoveredNode));
        }
        else { ClearHover(); }
    }

    private void ClearHover()
    {
        if (hoveredNode) { hoveredNode.SetHover(false); hoveredNode = null; }
    }

    private IEnumerator ConfirmNode(PathNode selectedNode)
    {
        isSelecting = false;
        stateManager.ChangeState(PlayerStateManager.State.AutoClimbing); 
        
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Color dimLineColor = new Color(0.2f, 0.2f, 0.2f, 0.3f); 
        Color dimIconColor = new Color(0.3f, 0.3f, 0.3f, 1f); 

        PathNode[] allCurrentNodes = FindObjectsByType<PathNode>(FindObjectsSortMode.None);
        foreach (PathNode n in allCurrentNodes)
        {
            if (n != selectedNode) 
            {
                if (nodeLines.ContainsKey(n)) nodeLines[n].color = dimLineColor;
                if (nodeFutureLines.ContainsKey(n)) nodeFutureLines[n].color = dimLineColor;

                if (n.uiNodeTransform != null)
                {
                    Image[] uiImages = n.uiNodeTransform.GetComponentsInChildren<Image>();
                    foreach (Image img in uiImages)
                    {
                        img.color = new Color(dimIconColor.r, dimIconColor.g, dimIconColor.b, img.color.a);
                    }
                }

                if (n.nodeText != null) n.nodeText.gameObject.SetActive(false);
                StartCoroutine(ShrinkAndDestroy(n.gameObject));
            }
        }

        if (playerUIIcon != null && selectedNode.uiNodeTransform != null)
        {
            Vector3 startPos = playerUIIcon.position;
            Vector3 endPos = selectedNode.uiNodeTransform.position;

            GameObject dynamicAuraObj = Instantiate(uiLinePrefab, uiLineContainer);
            RectTransform dynamicAuraRect = dynamicAuraObj.GetComponent<RectTransform>();
            Image dynamicAuraImg = dynamicAuraObj.GetComponent<Image>();
            dynamicAuraImg.color = auraLineColor;

            float t = 0;
            while (t < uiPlayerMoveDuration)
            {
                t += Time.deltaTime;
                float progress = t / uiPlayerMoveDuration;
                if (progress > 1f) progress = 1f;

                Vector3 currentPos = Vector3.Lerp(startPos, endPos, progress);
                playerUIIcon.position = currentPos;

                Vector3 dir = currentPos - startPos;
                dynamicAuraRect.position = startPos + (dir / 2f);
                
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                dynamicAuraRect.rotation = Quaternion.Euler(0, 0, angle);
                
                float trailDistance = Vector3.Distance(startPos, currentPos);
                dynamicAuraRect.sizeDelta = new Vector2(trailDistance / mapUIPanel.transform.lossyScale.x, lineThickness);

                yield return null;
            }
        }

        if (mapUIPanel) mapUIPanel.SetActive(false);
        if (nodeCam) nodeCam.SetActive(false);
        if (camController.climbCam) camController.climbCam.SetActive(true);

        // เซฟประเภทการกระทำของโหนดไว้ก่อนถูกลบ
        PathNode.NodeAction action = selectedNode.nodeAction;
        Vector3 finalDestination = selectedNode.climbTarget.position;
        Transform teleportDest = selectedNode.teleportDestination;
        string nextScene = selectedNode.sceneToLoad;

        if (selectedNode != null && selectedNode.gameObject != null)
        {
            if (selectedNode.nodeText != null) selectedNode.nodeText.gameObject.SetActive(false);
            StartCoroutine(ShrinkAndDestroy(selectedNode.gameObject));
        }

        yield return new WaitForSeconds(cameraBlendWaitTime);

        Transform player = stateManager.transform;
        float distance = Vector3.Distance(player.position, finalDestination);
        
        while (distance > 0.1f)
        {
            player.position = Vector3.MoveTowards(player.position, finalDestination, autoClimbSpeed * Time.deltaTime);
            distance = Vector3.Distance(player.position, finalDestination);
            yield return null; 
        }

        // *** จอจะเฟดมืดลง 100% ***
        yield return StartCoroutine(FadeScreen(1f, 1f));

        // *** เช็คว่าโหนดนี้คือการโหลดฉากใหม่ หรือวาร์ปในฉากเดิม ***
        if (action == PathNode.NodeAction.LoadNewScene)
        {
            Debug.Log("✅ โหลดฉากใหม่: " + nextScene);
            // โค้ดโหลดฉากจริงๆ จะอยู่ตรงนี้ เช่น UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
        }
        else 
        {
            Debug.Log("✨ วาร์ปไปจุดใหม่ในฉากเดิม");
            
            // 1. ย้ายตำแหน่งตัวละครไปยังจุดวาร์ป (ห้องใหม่)
            if (teleportDest != null)
            {
                player.position = teleportDest.position;
                player.rotation = teleportDest.rotation; // หมุนตัวให้ถูกทิศ
            }
            else
            {
                Debug.LogWarning("ลืมใส่ Teleport Destination ในโหนดที่เลือก!");
            }

            // 2. คืนค่ากล้องเป็นกล้องเดินปกติ
            if (camController.climbCam) camController.climbCam.SetActive(false);
            if (camController.walkCam) camController.walkCam.SetActive(true);
            camController.enabled = true; // เปิดสคริปต์กล้องกลับมา

            // 3. เปลี่ยน State กลับเป็นเดินปกติ 
            // ⚠️ ปล. หาก State เดินปกติของคุณชื่ออื่นที่ไม่ใช่ Idle ให้เปลี่ยนตรงนี้นะครับ
            stateManager.ChangeState(PlayerStateManager.State.Walking); 

            // 4. เฟดจอกลับมาสว่างเหมือนเดิม
            yield return StartCoroutine(FadeScreen(0f, 1f));
        }
    }

    private IEnumerator ShrinkAndDestroy(GameObject targetObj)
    {
        float duration = 0.4f;
        float time = 0;
        Vector3 startScale = targetObj.transform.localScale;
        while (time < duration)
        {
            if (targetObj == null) yield break;
            time += Time.deltaTime;
            float progress = time / duration;
            targetObj.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
            targetObj.transform.Rotate(Vector3.up * 800f * Time.deltaTime); 
            yield return null;
        }
        if (targetObj != null) Destroy(targetObj);
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