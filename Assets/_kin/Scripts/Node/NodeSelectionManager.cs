using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class NodeSelectionManager : MonoBehaviour
{
    public static NodeSelectionManager Instance;

    [Header("UI & Camera")]
    public GameObject mapUIPanel;
    public GameObject nodeCam;
    public Image fadeImage;

    [Header("Auto UI Generation")]
    public GameObject uiNodePrefab;
    public Transform uiNodeContainer;
    public GameObject uiLinePrefab;
    public Transform uiLineContainer;
    public float lineThickness = 4f;
    public Color normalLineColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
    public Color auraLineColor = Color.yellow;

    [Header("Map Progression UI")]
    public RectTransform playerUIIcon;

    [Header("Node Feedback")]
    public float nodeHoverScale = 1.15f;
    public float nodePressScale = 0.92f;
    public float nodeChosenScale = 1.25f;
    public float nodeRejectedScale = 0.85f;
    [Range(0f, 1f), Tooltip("ความสว่างของ Node ตอนยังไม่ได้ชี้ (ตอนชี้ = 1)")]
    public float nodeIdleBrightness = 0.75f;
    [Range(0f, 1f)] public float nodeRejectedBrightness = 0.3f;
    public Color nodeGlowColor = new Color(0.75f, 0.82f, 1f, 0.9f);

    [Header("หลังเลือก Node")]
    [Tooltip("เวลาแสดงผลการเลือกก่อนเริ่ม Fade (วินาที)")]
    public float selectFeedbackDuration = 0.35f;
    public float selectFadeDuration = 0.6f;

    [Header("Player Systems")]
    public PlayerStateManager stateManager;
    public PlayerCameraController camController;

    [Header("Mouse Camera Sway")]
    public float cameraSwayAmount = 5f;
    public Texture2D circleCursor;
    public Vector2 cursorHotspot = new Vector2(16, 16);

    private PathNode hoveredNode;
    private bool isSelecting = false;
    private Quaternion originalNodeCamRot;
    private Dictionary<PathNode, Image> nodeLines = new Dictionary<PathNode, Image>();
    private Dictionary<PathNode, Image> nodeFutureLines = new Dictionary<PathNode, Image>();
    private readonly List<NodeMapButton> nodeButtons = new List<NodeMapButton>();

    public bool IsSelecting => isSelecting;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (mapUIPanel == null) mapUIPanel = GameObject.Find("MapUIPanel");
        if (nodeCam == null) { GameObject cam = GameObject.Find("NodeCam"); if (cam) nodeCam = cam; }
        if (fadeImage == null) { GameObject fade = GameObject.Find("FadeImage"); if (fade) fadeImage = fade.GetComponent<Image>(); }

        // ตอนนี้ Fade เป็นทางเปลี่ยนหลักของการเลือก Node จึงต้องคลุมจอทุกสัดส่วน
        if (fadeImage != null)
        {
            fadeImage.rectTransform.anchorMin = Vector2.zero;
            fadeImage.rectTransform.anchorMax = Vector2.one;
            fadeImage.rectTransform.sizeDelta = Vector2.zero;
        }

        if (uiNodeContainer == null) { GameObject nc = GameObject.Find("UINodeContainer"); if (nc) uiNodeContainer = nc.transform; }
        if (uiLineContainer == null) { GameObject lc = GameObject.Find("UILineContainer"); if (lc) uiLineContainer = lc.transform; }
        if (playerUIIcon == null) { GameObject pi = GameObject.Find("PlayerUIIcon"); if (pi) playerUIIcon = pi.GetComponent<RectTransform>(); }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            stateManager = player.GetComponent<PlayerStateManager>();
            camController = player.GetComponent<PlayerCameraController>();
        }

        if (nodeCam != null)
        {
            originalNodeCamRot = nodeCam.transform.rotation;
            nodeCam.SetActive(false);
        }

        if (mapUIPanel != null) mapUIPanel.SetActive(false);
    }

    // ใช้ OnEnable แทน Start เพราะในโหมด Additive ฉากนี้ไม่ได้ถูกโหลดใหม่ แค่ถูกปิดแล้วเปิดกลับ
    private void OnEnable()
    {
        if (BattleSession.TryConsumeResult(out BattleResult battleResult))
        {
            StartCoroutine(ResumeAfterBattle(battleResult));
        }
    }

    public void StartSelection(Transform playerTransform)
    {
        if (isSelecting) return;
        isSelecting = true;

        if (playerTransform != null)
        {
            stateManager = playerTransform.GetComponent<PlayerStateManager>();
            camController = playerTransform.GetComponent<PlayerCameraController>();
        }

        if (stateManager) stateManager.ChangeState(PlayerStateManager.State.NodeSelection);
        if (mapUIPanel) mapUIPanel.SetActive(true);

        if (camController) camController.enabled = false;
        Cursor.SetCursor(circleCursor, cursorHotspot, CursorMode.Auto);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (nodeCam != null)
        {
            // แต่ละหน้าผาสูงไม่เท่ากัน: วางกล้องตาม Marker ของหน้าผาชั้นนี้
            ClimbableWall cliff = MapGenerator.CurrentCliff;
            if (cliff != null && cliff.nodeCamPoint != null)
            {
                nodeCam.transform.position = cliff.nodeCamPoint.position;
                originalNodeCamRot = cliff.nodeCamPoint.rotation;
            }
            nodeCam.transform.rotation = originalNodeCamRot;
            nodeCam.SetActive(true);
        }

        if (camController)
        {
            camController.walkCam.SetActive(false);
            camController.climbCam.SetActive(false);
        }

        // ต้องหลังตั้งกล้อง เพราะเรียงปุ่มซ้าย-ขวาตามมุมของ NodeCam
        StartCoroutine(GenerateMapUICoroutine(playerTransform.position));
    }

    private IEnumerator GenerateMapUICoroutine(Vector3 centerPosition)
    {
        PathNode[] allNodes = FindObjectsByType<PathNode>(FindObjectsSortMode.None);
        List<PathNode> currentNodes = new List<PathNode>();
        foreach (PathNode n in allNodes)
        {
            if (Vector3.Distance(centerPosition, n.transform.position) < 50f) currentNodes.Add(n);
        }
        // ปุ่มบนแผนที่เรียงซ้าย-ขวาตรงกับ Node ที่เห็นในฉาก
        Vector3 screenRight = nodeCam != null ? nodeCam.transform.right : Vector3.right;
        currentNodes.Sort((a, b) => Vector3.Dot(a.transform.position, screenRight).CompareTo(Vector3.Dot(b.transform.position, screenRight)));

        if (uiNodeContainer) foreach (Transform child in uiNodeContainer) Destroy(child.gameObject);
        if (uiLineContainer != null) foreach (Transform child in uiLineContainer) Destroy(child.gameObject);

        List<RectTransform> generatedUINodes = new List<RectTransform>();
        nodeLines.Clear(); nodeFutureLines.Clear(); nodeButtons.Clear();

        foreach (PathNode node in currentNodes)
        {
            if (uiNodePrefab == null || uiNodeContainer == null) break;

            GameObject newUI = Instantiate(uiNodePrefab, uiNodeContainer);
            newUI.SetActive(true);
            node.uiNodeTransform = newUI.GetComponent<RectTransform>();
            generatedUINodes.Add(node.uiNodeTransform);

            // ป้ายใต้ Node ของ UI อาร์ต: บอกว่า Node นี้คืออะไร
            Transform labelUI = newUI.transform.Find("Label");
            if (labelUI != null)
            {
                labelUI.GetComponent<TMPro.TMP_Text>().text = node.DisplayName;
            }

            Transform auraUI = newUI.transform.Find("Aura");
            if (auraUI != null) { node.uiMapAura = auraUI.gameObject; node.uiMapAura.SetActive(false); }
            Transform iconUI = newUI.transform.Find("Icon");
            if (iconUI != null && node.nodeIcon != null)
            {
                Image img = iconUI.GetComponent<Image>();
                if (img != null) img.sprite = node.nodeIcon;
            }

            NodeMapButton button = newUI.AddComponent<NodeMapButton>();
            button.Setup(node, this);
            nodeButtons.Add(button);
        }

        yield return null;

        if (uiLinePrefab != null && uiLineContainer != null && playerUIIcon != null)
        {
            RectTransform mapRect = mapUIPanel.GetComponent<RectTransform>();
            Vector3[] localCorners = new Vector3[4];
            mapRect.GetLocalCorners(localCorners);

            float bottomY = localCorners[0].y;
            float topY = localCorners[1].y;

            if (generatedUINodes.Count > 0)
            {
                float centerX = 0f;
                float nodeY = 0f;
                foreach (RectTransform rt in generatedUINodes)
                {
                    Vector3 localPos = mapRect.InverseTransformPoint(rt.position);
                    centerX += localPos.x;
                    nodeY = localPos.y;
                }
                centerX /= generatedUINodes.Count;
                float playerY = Mathf.Lerp(bottomY, nodeY, 0.4f);

                playerUIIcon.position = mapRect.TransformPoint(new Vector3(centerX, playerY, 0));
                playerUIIcon.SetAsLastSibling();

                Vector3 pastStartWorld = mapRect.TransformPoint(new Vector3(centerX, bottomY - 50f, 0));
                DrawUILine(pastStartWorld, playerUIIcon.position, auraLineColor);

                for (int i = 0; i < generatedUINodes.Count; i++)
                {
                    RectTransform targetNode = generatedUINodes[i];
                    PathNode logicNode = currentNodes[i];

                    Image branchLine = DrawUILine(playerUIIcon.position, targetNode.position, normalLineColor);
                    nodeLines.Add(logicNode, branchLine);

                    Vector3 localTarget = mapRect.InverseTransformPoint(targetNode.position);
                    Vector3 futureEndWorld = mapRect.TransformPoint(new Vector3(localTarget.x, topY + 50f, 0));
                    Image futureLine = DrawUILine(targetNode.position, futureEndWorld, normalLineColor);
                    nodeFutureLines.Add(logicNode, futureLine);
                }
            }
        }
    }

    private Image DrawUILine(Vector3 startWorldPos, Vector3 endWorldPos, Color lineColor)
    {
        if (uiLinePrefab == null || uiLineContainer == null) return null;
        GameObject lineObj = Instantiate(uiLinePrefab, uiLineContainer);
        RectTransform lineRect = lineObj.GetComponent<RectTransform>();
        Image lineImg = lineObj.GetComponent<Image>();

        Vector3 localStart = uiLineContainer.InverseTransformPoint(startWorldPos);
        Vector3 localEnd = uiLineContainer.InverseTransformPoint(endWorldPos);
        localStart.z = 0; localEnd.z = 0;

        lineRect.pivot = new Vector2(0f, 0.5f);
        lineRect.localPosition = localStart;

        Vector3 dir = localEnd - localStart;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);

        float dist = dir.magnitude;
        lineRect.sizeDelta = new Vector2(dist, lineThickness);

        lineImg.color = lineColor;
        lineRect.SetAsFirstSibling();

        return lineImg;
    }

    private void Update()
    {
        if (!isSelecting) return;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

        if (nodeCam != null)
        {
            float mouseX = (Input.mousePosition.x / Screen.width) - 0.5f;
            float mouseY = (Input.mousePosition.y / Screen.height) - 0.5f;
            Quaternion swayRot = Quaternion.Euler(-mouseY * cameraSwayAmount, mouseX * cameraSwayAmount, 0);
            nodeCam.transform.rotation = Quaternion.Lerp(nodeCam.transform.rotation, originalNodeCamRot * swayRot, Time.deltaTime * 5f);
        }
    }

    public void SetHoveredNode(PathNode node)
    {
        if (hoveredNode == node) return;
        ClearHover();
        hoveredNode = node;
        hoveredNode.SetHover(true);
        SetLineHighlight(nodeLines, hoveredNode, true);
        SetLineHighlight(nodeFutureLines, hoveredNode, true);
    }

    public void ClearHoveredNode(PathNode node)
    {
        if (hoveredNode == node) ClearHover();
    }

    public void ChooseNode(PathNode node)
    {
        if (isSelecting) StartCoroutine(ConfirmNode(node));
    }

    private void ClearHover()
    {
        if (!hoveredNode) return;
        hoveredNode.SetHover(false);
        SetLineHighlight(nodeLines, hoveredNode, false);
        SetLineHighlight(nodeFutureLines, hoveredNode, false);
        hoveredNode = null;
    }

    private void SetLineHighlight(Dictionary<PathNode, Image> lines, PathNode node, bool highlighted)
    {
        if (!lines.TryGetValue(node, out Image line) || line == null) return;
        line.color = highlighted ? auraLineColor : normalLineColor;
        RectTransform rect = line.rectTransform;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, highlighted ? lineThickness * 1.5f : lineThickness);
    }

    // เลือกจาก UI แล้วไม่ต้องเดินไปถึง Node: แสดงผลการเลือกสั้น ๆ แล้ว Fade ดำเข้าสู่ Node ทันที
    private IEnumerator ConfirmNode(PathNode selectedNode)
    {
        isSelecting = false;
        ClearHover();
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        foreach (NodeMapButton button in nodeButtons)
        {
            if (button != null) button.ShowResult(button.Node == selectedNode);
        }

        Color dimLineColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        foreach (PathNode n in FindObjectsByType<PathNode>(FindObjectsSortMode.None))
        {
            if (n == selectedNode) continue;
            if (nodeLines.TryGetValue(n, out Image line) && line != null) line.color = dimLineColor;
            if (nodeFutureLines.TryGetValue(n, out Image futureLine) && futureLine != null) futureLine.color = dimLineColor;
            if (n.nodeText != null) n.nodeText.gameObject.SetActive(false);
            StartCoroutine(ShrinkAndDestroy(n.gameObject));
        }
        SetLineHighlight(nodeFutureLines, selectedNode, true);

        if (playerUIIcon != null && selectedNode.uiNodeTransform != null)
        {
            Vector3 startWorld = playerUIIcon.position;
            Vector3 endWorld = selectedNode.uiNodeTransform.position;

            Vector3 localStart = uiLineContainer.InverseTransformPoint(startWorld);
            Vector3 localEnd = uiLineContainer.InverseTransformPoint(endWorld);
            localStart.z = 0; localEnd.z = 0;

            GameObject dynamicAuraObj = Instantiate(uiLinePrefab, uiLineContainer);
            RectTransform dynamicAuraRect = dynamicAuraObj.GetComponent<RectTransform>();
            Image dynamicAuraImg = dynamicAuraObj.GetComponent<Image>();
            dynamicAuraImg.color = auraLineColor;

            dynamicAuraRect.pivot = new Vector2(0f, 0.5f);
            Vector3 dir = localEnd - localStart;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            dynamicAuraRect.localRotation = Quaternion.Euler(0, 0, angle);
            dynamicAuraRect.localPosition = localStart;

            float t = 0;
            while (t < selectFeedbackDuration)
            {
                t += Time.deltaTime;
                float progress = Mathf.Clamp01(t / selectFeedbackDuration);

                playerUIIcon.position = Vector3.Lerp(startWorld, endWorld, progress);
                dynamicAuraRect.sizeDelta = new Vector2(Vector3.Distance(localStart, localEnd) * progress, lineThickness * 1.5f);

                yield return null;
            }
        }

        yield return StartCoroutine(FadeScreen(1f, selectFadeDuration));

        if (mapUIPanel) mapUIPanel.SetActive(false);

        PathNode.RoomType nextRoomType = selectedNode.roomType;
        string nextScene = selectedNode.sceneToLoad;
        bool isBattleNode = selectedNode.IsBattle;
        EncounterData encounter = isBattleNode ? selectedNode.PickEncounter() : null;

        if (GameManager.Instance != null && GameManager.Instance.playerData != null)
        {
            GameManager.Instance.playerData.currentFloor++;
        }

        TriggerNodeEncounter(selectedNode);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (isBattleNode)
        {
            // 🌟 Battle / MiniBoss / Boss -> เข้าฉากต่อสู้ผ่าน BattleSession แล้วจะถูกโหลดกลับมาที่ฉากนี้เอง
            string battleScene = nextScene;
            if (!Application.CanStreamedLevelBeLoaded(battleScene))
            {
                Debug.LogWarning($"⚠️ ไม่พบซีน '{battleScene}' ใน Build Settings ใช้ {BattleSession.DefaultBattleScene} แทน");
                battleScene = BattleSession.DefaultBattleScene;
            }

            Debug.Log($"⚔️ กำลังเข้าสู่การต่อสู้: {battleScene} (ประเภท: {nextRoomType}, Encounter: {(encounter != null ? encounter.encounterName : "ค่าในฉาก")})");
            BattleSession.Begin(new BattleRequest
            {
                encounter = encounter,
                weapons = GameManager.Instance != null ? GameManager.Instance.playerData.GetEquippedWeapons() : null,
                equippedSkills = GameManager.Instance != null ? GameManager.Instance.playerData.GetEquippedSkillMap() : null,
                battleSceneName = battleScene,
                defeatPenaltyText = "-1 Piton" // ต้องตรงกับกติกาใน ApplyBattleResult
            });
        }
        else if (nextRoomType == PathNode.RoomType.Camp && !string.IsNullOrEmpty(nextScene))
        {
            Debug.Log($"🚪 กำลังโหลดเข้าสู่ซีน: {nextScene} (ประเภท: {nextRoomType})");
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
        }
        else
        {
            if (RoomManager.Instance != null && stateManager != null)
            {
                RoomManager.Instance.LoadNewRoom(PathNode.RoomType.Normal, stateManager.transform);
            }

            ApplyFloorStartState();

            yield return StartCoroutine(FadeScreen(0f, 1f));
        }
    }

    // ตั้งกล้อง/สถานะผู้เล่นให้ตรงกับชั้นปัจจุบัน (ชั้น 1 = เดินบนพื้น, ชั้นอื่น = เกาะกำแพง)
    public void ApplyFloorStartState()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (camController)
        {
            camController.walkCam.SetActive(false);
            camController.climbCam.SetActive(true);
            camController.SwitchToClimbCam(stateManager.transform.eulerAngles.y);
            camController.enabled = true;
        }
        // หลังเลือก Node กล้องค้างที่ NodeCam จนกว่าจะเริ่มชั้นใหม่
        if (nodeCam) nodeCam.SetActive(false);

        if (stateManager) stateManager.ChangeState(PlayerStateManager.State.Climbing);
    }

    // ถูกเรียกเมื่อกลับมาจากฉากต่อสู้
    //  - โหมด Single: ฉากนี้ถูกโหลดใหม่ทั้งหมด RoomManager.Start() สร้างห้องของชั้นนี้ให้แล้ว
    //  - โหมด Additive: ฉากนี้แค่ถูกปิดไว้ ต้องสร้างห้องของชั้นถัดไปเอง (เหมือนเลือกโหนด Normal)
    private IEnumerator ResumeAfterBattle(BattleResult result)
    {
        // รอ 1 เฟรมให้ Start() ของกล้อง/ผู้เล่น/RoomManager ทำงานเสร็จก่อน แล้วค่อยตั้งสถานะทับ
        yield return null;

        if (result.callerSceneKept && RoomManager.Instance != null && stateManager != null)
        {
            RoomManager.Instance.LoadNewRoom(PathNode.RoomType.Normal, stateManager.transform);
        }

        ApplyBattleResult(result);
        ApplyFloorStartState();

        // ก่อนเข้าฉากต่อสู้จอถูก Fade เป็นสีดำไว้ (โหมด Additive ยังค้างอยู่) ค่อยๆ เปิดกลับ
        yield return StartCoroutine(FadeScreen(0f, 1f));
    }

    private void ApplyBattleResult(BattleResult result)
    {
        if (result.outcome == BattleOutcome.Victory)
        {
            if (result.encounter == null || InventoryManager.Instance == null) return;

            foreach (ItemReward reward in result.encounter.GetValidRewards())
            {
                bool added = InventoryManager.Instance.AddItem(reward.item, reward.amount);
                Debug.Log(added
                    ? $"🏆 ชนะการต่อสู้! ได้ {reward.item.itemName} x{reward.amount}"
                    : $"⚠️ ชนะการต่อสู้ แต่กระเป๋าเต็มหรือหนักเกินไป เก็บ {reward.item.itemName} x{reward.amount} ไม่ได้!");
            }
            return;
        }

        // แพ้การต่อสู้ = เสียหมุด (Piton) 1 อัน เหมือนตกจากกำแพง
        if (GameManager.Instance == null) return;

        GameManager.Instance.UsePiton();
        Debug.Log($"💀 แพ้การต่อสู้! เสียหมุด 1 อัน (เหลือ {GameManager.Instance.playerData.piton})");

        PitonUIController pitonUI = FindFirstObjectByType<PitonUIController>();
        if (pitonUI != null) pitonUI.ShowLostPitonAlert(GameManager.Instance.playerData.piton);
    }

    private void TriggerNodeEncounter(PathNode node)
    {
        if (node == null) return;

        switch (node.roomType)
        {
            case PathNode.RoomType.Normal:
                Debug.Log("🧗 [Trigger: Normal] เข้าสู่เส้นทางปีนเขาปกติ");
                break;

            case PathNode.RoomType.Battle:
                Debug.Log("⚔️ [Trigger: Battle] เข้าสู่เหตุการณ์ต่อสู้มอนสเตอร์ทั่วไป");
                break;

            case PathNode.RoomType.MiniBoss:
                Debug.Log("👹 [Trigger: Mini Boss] เข้าสู่เหตุการณ์ปะทะมินิบอสระหว่างทาง!");
                break;

            case PathNode.RoomType.Boss:
                Debug.Log("💀 [Trigger: BOSS] เข้าสู่ศึกตัดสินบอสใหญ่ประจำหอคอย!");
                break;

            case PathNode.RoomType.Ore:
                if (InventoryManager.Instance != null && node.oreRewardItem != null)
                {
                    bool success = InventoryManager.Instance.AddItem(node.oreRewardItem, node.oreAmount);
                    if (success)
                    {
                        Debug.Log($"💎 [Trigger: Ore] แจ็คพอต! ได้ {node.oreRewardItem.itemName} จำนวน {node.oreAmount} ชิ้น!");
                    }
                    else
                    {
                        Debug.LogWarning("⚠️ [Trigger: Ore] กระเป๋าเต็มหรือหนักเกินไป เก็บแร่แจ็คพอตไม่ได้!");
                    }
                }
                break;

            case PathNode.RoomType.Rest:
                if (GameManager.Instance != null && GameManager.Instance.playerData != null)
                {
                    PlayerData data = GameManager.Instance.playerData;
                    data.stamina = Mathf.Min(data.maxStamina, data.stamina + node.staminaRestore);
                    Debug.Log($"🔥 [Trigger: Rest] พักผ่อนฟื้นฟู Stamina +{node.staminaRestore}");
                }
                break;

            case PathNode.RoomType.Camp:
                Debug.Log("🏕️ [Trigger: Camp] เดินทางถึงแคมป์พักแรมสำเร็จ!");
                break;
        }
    }

    private IEnumerator ShrinkAndDestroy(GameObject targetObj)
    {
        float duration = 0.4f; float time = 0; Vector3 startScale = targetObj.transform.localScale;
        while (time < duration)
        {
            if (targetObj == null) yield break;
            time += Time.deltaTime; float progress = time / duration;
            targetObj.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
            targetObj.transform.Rotate(Vector3.up * 800f * Time.deltaTime);
            yield return null;
        }
        if (targetObj != null) Destroy(targetObj);
    }

    private IEnumerator FadeScreen(float targetAlpha, float duration)
    {
        if (fadeImage == null) yield break;
        Color color = fadeImage.color; float startAlpha = color.a; float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime; color.a = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            fadeImage.color = color; yield return null;
        }
    }
}