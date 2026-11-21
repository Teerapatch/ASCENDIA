using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ผูก UI ของอาร์ต (ClimbUI คัดมาจาก ENVI_Climb 1) เข้ากับระบบปีนเดิม: ความสูง, Stamina, Piton และแผนที่ Node
[DefaultExecutionOrder(-500)]
public class ClimbArtUI : MonoBehaviour
{
    [Tooltip("ความสูงที่นับเพิ่มต่อ 1 ชั้น (เมตร)")]
    public float metersPerFloor = 100f;
    public float barSpeed = 10f;
    public Color weightColor = new Color(1f, 0.55f, 0.15f, 0.85f);
    public Color fatigueColor = new Color(.28f, .05f, .08f, .95f);

    private TextMeshProUGUI heightText;
    private Image staminaFill, weightFill, fatigueFill;
    private float shownFatigue;
    private RectTransform staminaRoot;
    private Vector3 staminaBaseScale = Vector3.one * 0.5f;
    private GameObject[] pitonLost;
    private Transform player;
    private float shownStamina = -1f, shownWeight, lastStamina;
    private int shownMeters = -1;
    private float heightPunch;
    private Vector3 heightBaseScale = Vector3.one;

    private void Awake()
    {
        // อาร์ต ReArt v2 เปิด Raycast Target ทุกภาพ HUD นี้ไม่มีอะไรให้กด ปิดหมดไม่ให้บังการคลิก
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

        // ชุด v2 ห่อ Height ไว้ใน "Height (1)" ตัวเลขอยู่ชั้นเดียวกับกรอบ
        Transform height = transform.Find("Height (1)");
        if (height == null) height = transform.Find("Height");
        heightText = height.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
        height.Find("Text (TMP) (1)").GetComponent<TextMeshProUGUI>().text = "M";
        heightBaseScale = heightText.transform.localScale;

        Transform stamina = transform.Find("Stamina");
        staminaRoot = (RectTransform)stamina;
        staminaBaseScale = staminaRoot.localScale;
        staminaFill = stamina.Find("Image (2)").GetComponent<Image>();

        // น้ำหนักกระเป๋าแสดงเป็นแถบสีส้มต่อจาก Stamina เหมือนเดิม จึงต้องอยู่ใต้แถบ Stamina
        GameObject weight = Instantiate(staminaFill.gameObject, stamina);
        weight.name = "WeightFill";
        weight.transform.SetSiblingIndex(staminaFill.transform.GetSiblingIndex());
        weightFill = weight.GetComponent<Image>();
        weightFill.color = weightColor;

        // ความเหนื่อยกินแถบจากปลายอีกด้าน ส่วนที่ถูกทับคือ Stamina สูงสุดที่เสียไป
        GameObject fatigue = Instantiate(staminaFill.gameObject, stamina);
        fatigue.name = "FatigueFill";
        fatigue.transform.SetSiblingIndex(staminaFill.transform.GetSiblingIndex() + 1);
        fatigueFill = fatigue.GetComponent<Image>();
        fatigueFill.color = fatigueColor;
        // แถบ Stamina เป็นแนวนอนเติมจากซ้าย ความเหนื่อยจึงเติมจากขวาเข้ามา
        fatigueFill.type = Image.Type.Filled;
        fatigueFill.fillMethod = Image.FillMethod.Horizontal;
        fatigueFill.fillOrigin = (int)Image.OriginHorizontal.Right;
        fatigueFill.fillAmount = 0f;

        BindPitons();
        BindMap();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
    }

    private void BindPitons()
    {
        PitonUIController controller = FindAnyObjectByType<PitonUIController>(FindObjectsInactive.Include);
        Transform grid = transform.Find("Piton_Grid");
        Image[] icons = new Image[grid.childCount];
        pitonLost = new GameObject[grid.childCount];
        for (int i = 0; i < grid.childCount; i++)
        {
            Transform cell = grid.GetChild(i);
            icons[i] = cell.Find("Image").GetComponent<Image>();
            pitonLost[i] = cell.Find("Image (1)").gameObject;
        }
        if (controller == null) return;

        controller.pitonIcons = icons;
        Transform oldCanvas = controller.transform.parent;
        foreach (string oldName in new[] { "WeightBar", "StaminaBar", "PitonContainer", "MapUIPanel" })
        {
            Transform old = oldCanvas.Find(oldName);
            if (old != null) old.gameObject.SetActive(false);
        }
    }

    private void BindMap()
    {
        NodeSelectionManager nodes = FindAnyObjectByType<NodeSelectionManager>(FindObjectsInactive.Include);
        if (nodes == null) return;

        // ชุด v2: เลือกเส้นทางด้วยการ์ดบน Canvas NodePath_RE_UI (แผนที่ NodePath เดิมถูกปิดในอาร์ต)
        GameObject cards = GameObject.Find("NodePath_RE_UI");
        if (cards != null)
        {
            BindCards(nodes, cards.transform);
            return;
        }

        Transform path = transform.Find("NodePath");
        Image mock = path.GetComponent<Image>();
        if (mock != null) mock.enabled = false;

        Transform container = path.Find("Node");
        Transform template = container.GetChild(0).GetChild(0);
        template.SetParent(path, false);
        template.gameObject.SetActive(false);
        template.Find("Image (1)").name = "Icon";
        template.Find("Text (TMP) (4)").name = "Label";

        nodes.mapUIPanel = path.gameObject;
        nodes.uiNodePrefab = template.gameObject;
        nodes.uiNodeContainer = container;
        nodes.uiLineContainer = path.Find("LinesNode");
        nodes.playerUIIcon = (RectTransform)path.Find("CurrentPath");
        nodes.lineThickness = 10f;
        nodes.normalLineColor = new Color(0.04f, 0.04f, 0.07f, 1f);
        nodes.auraLineColor = new Color(0.82f, 0.82f, 0.95f, 1f);
    }

    private static void BindCards(NodeSelectionManager nodes, Transform root)
    {
        // กรอบเต็มจอ (bg, border, Frame) ของอาร์ตเปิด Raycast ไว้ จะกินคลิกการ์ด ปิดหมดแล้วเปิดเฉพาะการ์ดกับปุ่ม Confirm
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

        Transform grid = root.Find("Card_Grid");
        var tilts = new System.Collections.Generic.List<float>();
        foreach (Transform sample in grid)
        {
            Transform frame = sample.Find("CardFrame");
            if (frame != null) tilts.Add(frame.localEulerAngles.z);
        }
        nodes.cardTilts = tilts.ToArray();

        Transform template = grid.GetChild(0);
        template.SetParent(root, false);
        template.gameObject.SetActive(false);
        foreach (Transform sample in grid) Destroy(sample.gameObject);

        Transform confirm = root.Find("ConfirmButton");
        Image confirmImage = confirm.GetComponent<Image>();
        confirmImage.raycastTarget = true;
        Button button = confirm.GetComponent<Button>();
        if (button == null) button = confirm.gameObject.AddComponent<Button>();
        button.targetGraphic = confirmImage;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.95f, 0.8f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.85f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        button.colors = colors;

        nodes.mapUIPanel = root.gameObject;
        nodes.cardTemplate = template.gameObject;
        nodes.cardContainer = grid;
        nodes.confirmButton = button;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerData == null) return;
        PlayerData data = GameManager.Instance.playerData;

        if (shownStamina < 0f)
        {
            shownStamina = lastStamina = data.stamina;
            shownWeight = data.weight;
        }

        // ได้ Stamina ทีเดียวเยอะ ๆ (สมุนไพร/พัก) ให้แถบเด้ง
        if (data.stamina > lastStamina + 2f) staminaRoot.localScale = staminaBaseScale * 1.12f;
        lastStamina = data.stamina;
        staminaRoot.localScale = Vector3.Lerp(staminaRoot.localScale, staminaBaseScale, Time.deltaTime * 12f);

        float t = 1f - Mathf.Exp(-barSpeed * Time.deltaTime);
        shownStamina = Mathf.Lerp(shownStamina, data.stamina, t);
        shownWeight = Mathf.Lerp(shownWeight, data.weight, t);
        staminaFill.fillAmount = Mathf.Clamp01(shownStamina / data.maxStamina);
        weightFill.fillAmount = Mathf.Clamp01((shownStamina + shownWeight) / data.maxStamina);
        shownFatigue = Mathf.Lerp(shownFatigue, data.maxStamina - data.EffectiveMaxStamina, t);
        fatigueFill.fillAmount = Mathf.Clamp01(shownFatigue / data.maxStamina);

        for (int i = 0; i < pitonLost.Length; i++) pitonLost[i].SetActive(i >= data.piton);

        // ความสูงสะสมทั้งรัน: หน้าผาละ metersPerFloor (ปีนถึงยอด = ครบ 100 m แล้วได้เลือกเส้นทาง)
        // ใช้ floorsClimbed ที่ไม่รีเซ็ตตอนเข้า Camp ความสูงจึงขึ้นต่อเนื่อง และบันทึกไว้ใน PlayerData ทุกเฟรม
        ClimbableWall wall = MapGenerator.CurrentCliff;
        if (wall != null && player != null)
        {
            float progress = NodeSelectionManager.FloorProgress(wall, player.position.y);
            data.heightMeters = (data.floorsClimbed + progress) * metersPerFloor;
            data.bestHeightMeters = Mathf.Max(data.bestHeightMeters, data.heightMeters);
        }
        int meters = Mathf.RoundToInt(data.heightMeters);
        if (meters != shownMeters)
        {
            // ข้ามหลักร้อย (ครบอีก 100 m) ตัวเลขเด้ง
            if (shownMeters >= 0 && meters / 100 > shownMeters / 100) heightPunch = 1f;
            shownMeters = meters;
            heightText.text = meters.ToString("0000");
        }
        heightPunch = Mathf.MoveTowards(heightPunch, 0f, Time.deltaTime * 2.5f);
        heightText.transform.localScale = heightBaseScale * (1f + heightPunch * 0.35f);
    }
}
