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

    private TextMeshProUGUI heightText;
    private Image staminaFill, weightFill;
    private RectTransform staminaRoot;
    private GameObject[] pitonLost;
    private Transform player;
    private float shownStamina = -1f, shownWeight, lastStamina;

    private void Awake()
    {
        Transform height = transform.Find("Height");
        heightText = height.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
        height.Find("Text (TMP) (1)").GetComponent<TextMeshProUGUI>().text = "M";

        Transform stamina = transform.Find("Stamina");
        staminaRoot = (RectTransform)stamina;
        staminaFill = stamina.Find("Image (2)").GetComponent<Image>();

        // น้ำหนักกระเป๋าแสดงเป็นแถบสีส้มต่อจาก Stamina เหมือนเดิม จึงต้องอยู่ใต้แถบ Stamina
        GameObject weight = Instantiate(staminaFill.gameObject, stamina);
        weight.name = "WeightFill";
        weight.transform.SetSiblingIndex(staminaFill.transform.GetSiblingIndex());
        weightFill = weight.GetComponent<Image>();
        weightFill.color = weightColor;

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
        if (data.stamina > lastStamina + 2f) staminaRoot.localScale = Vector3.one * 0.56f;
        lastStamina = data.stamina;
        staminaRoot.localScale = Vector3.Lerp(staminaRoot.localScale, Vector3.one * 0.5f, Time.deltaTime * 12f);

        float t = 1f - Mathf.Exp(-barSpeed * Time.deltaTime);
        shownStamina = Mathf.Lerp(shownStamina, data.stamina, t);
        shownWeight = Mathf.Lerp(shownWeight, data.weight, t);
        staminaFill.fillAmount = Mathf.Clamp01(shownStamina / data.maxStamina);
        weightFill.fillAmount = Mathf.Clamp01((shownStamina + shownWeight) / data.maxStamina);

        for (int i = 0; i < pitonLost.Length; i++) pitonLost[i].SetActive(i >= data.piton);

        ClimbableWall wall = MapGenerator.CurrentCliff;
        if (wall != null && player != null)
        {
            float progress = Mathf.InverseLerp(wall.MinClimbY, wall.MaxClimbY, player.position.y);
            int meters = Mathf.RoundToInt((Mathf.Max(1, data.currentFloor) - 1 + progress) * metersPerFloor);
            heightText.text = meters.ToString("0000");
        }
    }
}
