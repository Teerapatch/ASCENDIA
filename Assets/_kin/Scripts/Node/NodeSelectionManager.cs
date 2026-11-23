using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
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

    [Header("เลือกเส้นทางแบบการ์ด (UI ReArt v2: NodePath_RE_UI)")]
    [Tooltip("ใส่แล้วใช้การ์ด + ปุ่ม Confirm แทนแผนที่ Node")]
    public GameObject cardTemplate;
    public Transform cardContainer;
    public Button confirmButton;
    [Tooltip("มุมเอียงของการ์ดแต่ละช่องตามที่อาร์ตวางไว้ (องศา)")]
    public float[] cardTilts = new float[0];

    [Header("เลือกเส้นทางทุก 100 m")]
    [Tooltip("เปิด = ปีนถึงยอดหน้าผาของชั้นนี้ (ความสูงครบอีก 100 m) แล้วการ์ดเลือกเส้นทางขึ้นเอง แทน Trigger (NodeSelectionZone) บนผา")]
    public bool selectByHeight = true;
    [Tooltip("ห่างจากยอดที่ปีนได้ (MaxClimbY) ไม่เกินเท่านี้ (หน่วยฉาก) ถือว่าถึงแล้ว")]
    public float reachTolerance = 0.3f;
    [Tooltip("ซ่อน Node 3D (ไอคอน/ป้ายบนหน้าผา) ใช้การ์ดอย่างเดียว")]
    public bool hideNodesInScene = true;

    [Header("หลังเลือก: เปลี่ยนหน้าผาแบบไร้รอยต่อ")]
    [Tooltip("ระหว่างจอดำ รอให้หน้าผาใหม่สร้างเสร็จ ผู้เล่นเกาะผนัง และกล้องเข้าที่ (วินาที)")]
    public float floorSettleTime = 0.35f;
    [Tooltip("เวลาค่อย ๆ เปิดจอหลังโหลดหน้าผาใหม่เสร็จ")]
    public float floorFadeInDuration = 0.6f;
    [Tooltip("เวลาที่การ์ดค่อย ๆ หายไปหลังเปิดจอแล้ว จากนั้นค่อยให้ผู้เล่นบังคับได้")]
    public float cardCloseDuration = 0.45f;

    [Header("Juice ของการ์ด")]
    public float panelFadeTime = 0.3f;
    public float cardIntroDelay = 0.15f;
    public float cardIntroStagger = 0.11f;

    private PathNode selectedCardNode;
    private readonly List<NodeCardButton> nodeCards = new List<NodeCardButton>();
    // ระหว่างเลือกแล้วกำลังเปลี่ยนชั้น (ผู้เล่นยังอยู่ที่ยอด) ห้ามเปิดการ์ดซ้ำ จนกว่า ApplyFloorStartState
    private bool floorFinished;
    private Coroutine panelRoutine;

    private bool CardMode => cardTemplate != null && cardContainer != null;

    // ความคืบหน้าของหน้าผาชั้นนี้ 0 (จุดเริ่ม) ถึง 1 (ยอดที่ปีนได้ = ครบ 100 m)
    public static float FloorProgress(ClimbableWall wall, float y)
    {
        return wall != null ? Mathf.InverseLerp(wall.MinClimbY, wall.MaxClimbY, y) : 0f;
    }

    private void Awake()
    {
        Instance = this;
        PlayerStateManager.InputLocked = false; // เผื่อฉากถูกโหลดใหม่ระหว่างล็อก
    }

    private void Start()
    {
        if (mapUIPanel == null) mapUIPanel = GameObject.Find("MapUIPanel");
        if (nodeCam == null) { GameObject cam = GameObject.Find("NodeCam"); if (cam) nodeCam = cam; }
        // FadeImage เดิมอยู่ใน Canvas ของ HUD เก่าที่ถูกปิด (มองไม่เห็น) ใช้ภาพดำ Overlay ของ ScreenFader แทน
        fadeImage = ScreenFader.Image;

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
            // กลับมาจากการต่อสู้: เริ่มจากจอดำ ตั้งหน้าผา/กล้องให้เสร็จก่อนค่อยเปิดจอ
            fadeImage = ScreenFader.Image;
            ScreenFader.SetAlpha(1f);
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

        // โหมดการ์ด: Node บนผาถูกซ่อนแล้ว คงกล้องปีนไว้ (หยุดตามผู้เล่น) ไม่ตัดไป NodeCam
        if (nodeCam != null && !CardMode)
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

        if (camController && !CardMode)
        {
            camController.walkCam.SetActive(false);
            camController.climbCam.SetActive(false);
        }

        // ต้องหลังตั้งกล้อง เพราะเรียงปุ่มซ้าย-ขวาตามมุมของ NodeCam
        if (CardMode)
        {
            BuildCards(playerTransform.position);
            if (panelRoutine != null) StopCoroutine(panelRoutine);
            panelRoutine = StartCoroutine(PanelIntro());
        }
        else StartCoroutine(GenerateMapUICoroutine(playerTransform.position));
    }

    private List<PathNode> NodesAround(Vector3 centerPosition)
    {
        List<PathNode> currentNodes = new List<PathNode>();
        foreach (PathNode n in FindObjectsByType<PathNode>(FindObjectsSortMode.None))
        {
            if (Vector3.Distance(centerPosition, n.transform.position) < 50f) currentNodes.Add(n);
        }
        Vector3 screenRight = nodeCam != null ? nodeCam.transform.right : Vector3.right;
        currentNodes.Sort((a, b) => Vector3.Dot(a.transform.position, screenRight).CompareTo(Vector3.Dot(b.transform.position, screenRight)));
        return currentNodes;
    }

    // การ์ดหนึ่งใบต่อทางเลือก: คลิกเพื่อเลือก (มีเครื่องหมาย Selected) แล้วกด Confirm เพื่อไปต่อ
    private void BuildCards(Vector3 centerPosition)
    {
        foreach (Transform child in cardContainer) Destroy(child.gameObject);
        nodeCards.Clear();
        nodeButtons.Clear();
        selectedCardNode = null;

        foreach (PathNode node in NodesAround(centerPosition))
        {
            GameObject card = Instantiate(cardTemplate, cardContainer);
            card.SetActive(true);
            Transform frame = card.transform.Find("CardFrame");
            if (frame != null && cardTilts.Length > 0) frame.localRotation = Quaternion.Euler(0f, 0f, cardTilts[nodeCards.Count % cardTilts.Length]);
            NodeCardButton button = card.AddComponent<NodeCardButton>();
            button.Setup(node, this);
            button.PlayIntro(cardIntroDelay + nodeCards.Count * cardIntroStagger);
            nodeCards.Add(button);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmSelectedCard);
            confirmButton.onClick.AddListener(ConfirmSelectedCard);
        }
        RefreshConfirm();
        UpdateCardHeader();
    }

    // หัวหน้าเลือกเส้นทาง: แก้คำผิดของอาร์ต ("SELECT YOU PATH") + บรรทัดบอกชั้น/ช่วงปัจจุบันใต้หัวข้อ (ก๊อปข้อความหัวข้อ สร้างครั้งเดียว)
    private void UpdateCardHeader()
    {
        if (mapUIPanel == null) return;
        Transform head = mapUIPanel.transform.Find("Head");
        TMPro.TMP_Text title = head != null ? head.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        if (title == null) return;

        PlayerData data = GameManager.Instance != null ? GameManager.Instance.playerData : null;
        bool lastFloor = data != null && data.currentFloor >= data.maxFloorBeforeCamp;
        title.text = lastFloor ? (data.IsFinalBossStage ? "THE SUMMIT AWAITS" : "A CAMP AHEAD") : "SELECT YOUR PATH";

        Transform infoTransform = head.Find("FloorInfo");
        TMPro.TMP_Text info = infoTransform != null ? infoTransform.GetComponent<TMPro.TMP_Text>() : null;
        if (info == null)
        {
            info = Instantiate(title, title.transform.parent);
            info.name = "FloorInfo";
            info.fontSize = title.fontSize * 0.45f;
            info.characterSpacing = 6f;
            info.color = new Color(0.78f, 0.84f, 1f, 0.85f);
            info.rectTransform.anchoredPosition = title.rectTransform.anchoredPosition + Vector2.down * (title.fontSize * 1.05f);
        }
        info.text = data == null ? "" : lastFloor
            ? $"STAGE {data.currentCampLevel} / {data.finalCampLevel}"
            : $"FLOOR {data.currentFloor} / {data.maxFloorBeforeCamp}      STAGE {data.currentCampLevel} / {data.finalCampLevel}";
    }

    public void SelectCard(PathNode node)
    {
        if (!isSelecting) return;
        selectedCardNode = node;
        foreach (NodeCardButton card in nodeCards)
        {
            if (card == null) continue;
            card.SetSelected(card.Node == node);
            card.SetDimmed(node != null && card.Node != node); // ใบที่ไม่ได้เลือกจางลง ใบที่เลือกเด่นขึ้น
        }
        SetHoveredNode(node); // ไฮไลต์ Node ในฉากด้วย
        RefreshConfirm();
    }

    private void RefreshConfirm()
    {
        if (confirmButton == null) return;
        bool ready = selectedCardNode != null;
        if (ready && !confirmButton.interactable) StartCoroutine(Punch(confirmButton.transform, 0.18f));
        confirmButton.interactable = ready;
        CanvasGroup group = confirmButton.GetComponent<CanvasGroup>();
        if (group == null) group = confirmButton.gameObject.AddComponent<CanvasGroup>();
        group.alpha = ready ? 1f : 0.45f;
    }

    private void ConfirmSelectedCard()
    {
        if (selectedCardNode != null) ChooseNode(selectedCardNode);
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
        if (!isSelecting) CheckHeightMilestone();
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

    // ปีนถึงยอดหน้าผาของชั้นนี้ = ความสูงครบอีก 100 m: เปิดการ์ดเลือกเส้นทาง
    private void CheckHeightMilestone()
    {
        if (!selectByHeight || floorFinished || stateManager == null) return;
        if (GameExitMenu.IsOpen) return;
        PlayerStateManager.State state = stateManager.currentState;
        if (state != PlayerStateManager.State.Climbing && state != PlayerStateManager.State.Walking && state != PlayerStateManager.State.Mantling) return;
        ClimbableWall wall = MapGenerator.CurrentCliff;
        if (wall == null || stateManager.transform.position.y < wall.MaxClimbY - reachTolerance) return;

        floorFinished = true;
        StartSelection(stateManager.transform);
    }

    // หน้าต่างเลือกเส้นทางโผล่: พื้นหลังค่อย ๆ ปรากฏ หัวข้อเลื่อนลงมา การ์ดลอยขึ้นทีละใบ (การ์ดเล่นเองใน NodeCardButton)
    private IEnumerator PanelIntro()
    {
        if (mapUIPanel == null) yield break;
        CanvasGroup group = mapUIPanel.GetComponent<CanvasGroup>();
        if (group == null) group = mapUIPanel.AddComponent<CanvasGroup>();
        RectTransform head = mapUIPanel.transform.Find("Head") as RectTransform;
        Vector2 headPos = head != null ? head.anchoredPosition : Vector2.zero;
        for (float t = 0f; t < panelFadeTime; t += Time.unscaledDeltaTime)
        {
            float k = t / panelFadeTime;
            group.alpha = k;
            if (head != null) head.anchoredPosition = headPos + Vector2.up * 80f * (1f - k) * (1f - k);
            yield return null;
        }
        group.alpha = 1f;
        if (head != null) head.anchoredPosition = headPos;
        panelRoutine = null;
    }

    private static IEnumerator Punch(Transform target, float amount)
    {
        Vector3 baseScale = target.localScale;
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.3f;
            target.localScale = baseScale * (1f + amount * Mathf.Sin(k * Mathf.PI) * (1f - k));
            yield return null;
        }
        target.localScale = baseScale;
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
        foreach (NodeCardButton card in nodeCards)
        {
            if (card != null) card.ShowResult(card.Node == selectedNode);
        }
        if (nodeCards.Count > 0) yield return new WaitForSeconds(selectFeedbackDuration);

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

        PathNode.RoomType nextRoomType = selectedNode.roomType;
        string nextScene = selectedNode.sceneToLoad;
        bool isBattleNode = selectedNode.IsBattle;
        bool staysInScene = !isBattleNode && !(nextRoomType == PathNode.RoomType.Camp && !string.IsNullOrEmpty(nextScene));

        // จอดำ: การ์ดอยู่บน Canvas Overlay จึงยังเห็นอยู่เหนือความมืด (FadeImage อยู่บน Canvas แบบ Camera)
        if (staysInScene && CardMode) PlayerStateManager.InputLocked = true;
        yield return StartCoroutine(FadeScreen(1f, selectFadeDuration));

        // ออกจากฉาก (ต่อสู้ / Camp) ปิดการ์ดเลย ส่วนหน้าผาถัดไปในฉากนี้ให้การ์ดค่อย ๆ หายหลังเปิดจอ
        if (mapUIPanel && !(staysInScene && CardMode)) mapUIPanel.SetActive(false);
        EncounterData encounter = isBattleNode ? selectedNode.PickEncounter() : null;

        if (GameManager.Instance != null && GameManager.Instance.playerData != null)
        {
            GameManager.Instance.playerData.currentFloor++;
            GameManager.Instance.playerData.floorsClimbed++; // ความสูงสะสม ไม่รีเซ็ตตอนเข้า Camp
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
                // ต้องตรงกับกติกาใน ApplyBattleResult: ไม่มี Piton เหลือแล้วแพ้ = จบรัน
                defeatPenaltyText = GameManager.Instance != null && GameManager.Instance.playerData.piton <= 0 ? "No pitons left" : "-1 Piton",
                finalBoss = nextRoomType == PathNode.RoomType.Boss
            });
        }
        else if (nextRoomType == PathNode.RoomType.Camp && !string.IsNullOrEmpty(nextScene))
        {
            Debug.Log($"🚪 กำลังโหลดเข้าสู่ซีน: {nextScene} (ประเภท: {nextRoomType})");
            SceneTransition.Load(nextScene);
        }
        else
        {
            // ไร้รอยต่อ: ระหว่างจอดำสร้างหน้าผาใหม่ วางผู้เล่นบนผนัง และตัดกล้องให้เข้าที่ให้เสร็จก่อน
            // แล้วค่อยเปิดจอ (การ์ดยังค้างอยู่) -> การ์ดค่อย ๆ หายไป -> ปลดล็อกให้เล่นต่อ
            PlayerStateManager.InputLocked = true;
            SoundManager.Play(Sfx.FloorWhoosh);
            if (RoomManager.Instance != null && stateManager != null)
            {
                RoomManager.Instance.LoadNewRoom(PathNode.RoomType.Normal, stateManager.transform);
            }

            ApplyFloorStartState();
            yield return StartCoroutine(SettleNewFloor());

            yield return StartCoroutine(FadeScreen(0f, floorFadeInDuration));
            yield return StartCoroutine(CloseCardPanel());
            PlayerStateManager.InputLocked = false;
        }
    }

    // รอให้หน้าผาใหม่พร้อม: ห้องเก่าถูกลบ Start ของห้องใหม่ทำงาน (Node, ของเก็บ, Collider) ผู้เล่นเกาะผิวหิน
    // กล้องตามผู้เล่นมีหน่วง ถ้าไม่ตัดจะเห็นกล้องเลื่อนลงมาจากยอดหน้าผาเก่าตอนเปิดจอ จึงตัดให้ถึงที่ทุกเฟรมระหว่างรอ
    private IEnumerator SettleNewFloor()
    {
        yield return null;
        Physics.SyncTransforms();
        for (float t = 0f; t < floorSettleTime; t += Time.unscaledDeltaTime)
        {
            SnapCameras();
            yield return null;
        }
        SnapCameras();
        yield return null;
    }

    private static void SnapCameras()
    {
        foreach (CinemachineVirtualCameraBase cam in FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsSortMode.None))
        {
            cam.PreviousStateIsValid = false;
        }
    }

    // การ์ดค่อย ๆ หายไปหลังเปิดจอ (ใบที่เลือกยังเด่นอยู่จนจางหมด) แล้วปิดหน้าต่าง
    private IEnumerator CloseCardPanel()
    {
        if (mapUIPanel == null || !mapUIPanel.activeSelf) yield break;
        CanvasGroup group = mapUIPanel.GetComponent<CanvasGroup>();
        if (group == null) group = mapUIPanel.AddComponent<CanvasGroup>();
        group.interactable = false;
        float from = group.alpha;
        for (float t = 0f; t < cardCloseDuration; t += Time.unscaledDeltaTime)
        {
            float k = t / cardCloseDuration;
            group.alpha = from * (1f - k * k * (3f - 2f * k));
            yield return null;
        }
        mapUIPanel.SetActive(false);
        group.alpha = 1f;
        group.interactable = true;
    }

    // ตั้งกล้อง/สถานะผู้เล่นให้ตรงกับชั้นปัจจุบัน (ชั้น 1 = เดินบนพื้น, ชั้นอื่น = เกาะกำแพง)
    public void ApplyFloorStartState()
    {
        floorFinished = false;
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

        bool runOver = ApplyBattleResult(result);
        PlayerStateManager.InputLocked = true;
        if (runOver) yield break; // หน้าจบ (ชนะบอสยอดเขา / แพ้ตอนไม่มี Piton) ขึ้นแล้ว จอดำค้างไว้ข้างหลัง
        ApplyFloorStartState();
        yield return StartCoroutine(SettleNewFloor()); // ตั้งหน้าผาและกล้องให้นิ่งก่อนเปิดจอ

        // ก่อนเข้าฉากต่อสู้จอถูก Fade เป็นสีดำไว้ (โหมด Additive ยังค้างอยู่) ค่อยๆ เปิดกลับ
        yield return StartCoroutine(FadeScreen(0f, 1f));
        PlayerStateManager.InputLocked = false;
    }

    // คืน true = รันจบแล้ว (ชนะบอสสุดท้าย หรือแพ้ตอนไม่มี Piton เหลือ)
    private bool ApplyBattleResult(BattleResult result)
    {
        if (result.outcome == BattleOutcome.Victory)
        {
            if (GameManager.Instance != null) GameManager.Instance.playerData.battlesWon++;
            if (result.finalBoss)
            {
                GameFlow.ShowEnding();
                return true;
            }
            if (result.encounter == null || InventoryManager.Instance == null) return false;

            foreach (ItemReward reward in result.encounter.GetValidRewards())
            {
                bool added = InventoryManager.Instance.AddItem(reward.item, reward.amount);
                Debug.Log(added
                    ? $"🏆 ชนะการต่อสู้! ได้ {reward.item.itemName} x{reward.amount}"
                    : $"⚠️ ชนะการต่อสู้ แต่กระเป๋าเต็มหรือหนักเกินไป เก็บ {reward.item.itemName} x{reward.amount} ไม่ได้!");
            }
            return false;
        }

        // แพ้การต่อสู้ = เสียหมุด (Piton) 1 อัน เหมือนตกจากกำแพง ไม่มีเหลือแล้ว = จบรัน
        if (GameManager.Instance == null) return false;
        if (GameManager.Instance.playerData.piton <= 0)
        {
            GameFlow.ShowGameOver("FALLEN", "You were defeated with no pitons left to save you.");
            return true;
        }

        GameManager.Instance.UsePiton();
        Debug.Log($"💀 แพ้การต่อสู้! เสียหมุด 1 อัน (เหลือ {GameManager.Instance.playerData.piton})");

        PitonUIController pitonUI = FindFirstObjectByType<PitonUIController>();
        try { if (pitonUI != null) pitonUI.ShowLostPitonAlert(GameManager.Instance.playerData.piton); }
        catch (System.Exception e) { Debug.LogException(e); }
        return false;
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
                    data.fatigue = 0f; // พักแล้วหายเหนื่อย
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
        if (fadeImage == null) fadeImage = ScreenFader.Image;
        Color color = fadeImage.color; float startAlpha = color.a; float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime; color.a = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            fadeImage.color = color; yield return null;
        }
    }
}