using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// หลอดเลือด/Stagger ของศัตรูชุด ReArt v2 (EnemyHUD บน BattleUI แบบ Overlay)
// คัดลอกแม่แบบหนึ่งชุดต่อศัตรูแต่ละตัว ลอยเหนือหัวตามตำแหน่งบนจอ แล้วซ่อนหลอดแบบ World Space เดิม (ไอคอนสถานะยังอยู่)
// หลอดที่คัดลอกอยู่ในชั้น EnemyHUD_Layer (Canvas ซ้อน ลำดับต่ำกว่า BattleUI) จึงอยู่ใต้เมนู/การ์ด/ลำดับเทิร์น และใต้เลขดาเมจ (BattleUILayers)
public class EnemyHUDOverlay : MonoBehaviour
{
    public RectTransform template;
    [Tooltip("ขนาดหลอดบนจอเทียบกับแม่แบบของอาร์ต")]
    public float hudScale = 0.3f;
    [Tooltip("ยกขึ้นจากจุดหลอดเดิมของศัตรู (พิกเซลที่ 1080p)")]
    public float screenLift = 30f;
    public float smoothSpeed = 10f;

    [Header("Stagger")]
    [Tooltip("ป้ายเหนือชื่อตอนติด Stagger")]
    public string staggerText = "STAGGERED";
    public Color staggerColor = new Color(1f, 0.82f, 0.3f, 1f);
    [Tooltip("ป้ายลอยเหนือชื่อกี่เท่าของความสูงป้ายชื่อ")]
    public float staggerLabelLift = 1.3f;
    [Header("แถบสถานะใต้หลอด (StatusBadgeRow)")]
    public bool showStatusRow = true;
    public float statusRowScale = 0.8f;
    [Tooltip("ระยะจากเส้นกลางของหลอดล่างสุด (หลอด Stagger) ลงมาถึงขอบบนของแถบสถานะ (พิกเซลอ้างอิง 1920x1080)")]
    public float statusRowGap = 13f;

    private class Entry
    {
        public RectTransform root;
        public Image hp, stagger;
        public TextMeshProUGUI label;
        public Transform anchor;
        public CanvasGroup group;
        public TextMeshProUGUI staggerLabel;
        public Color staggerBarColor;
        public float staggerPop;
        public bool wasStaggered;
        public StatusBadgeRow status;
        public RectTransform[] bars; // หลอดที่มองเห็นจริง (ใช้ขอบล่างวางแถบสถานะ)
    }

    private readonly Dictionary<EnemyController, Entry> entries = new Dictionary<EnemyController, Entry>();
    private readonly List<EnemyController> stale = new List<EnemyController>();
    private Canvas canvas;
    private Transform hudParent;

    private void Start()
    {
        canvas = GetComponent<Canvas>();
    }

    private Transform HudParent()
    {
        if (hudParent != null) return hudParent;
        if (canvas != null && canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            hudParent = BattleUILayers.Get(canvas, BattleUILayers.EnemyHUDLayer, BattleUILayers.EnemyHUDOffset);
        else
            hudParent = template.parent;
        return hudParent;
    }

    private void LateUpdate()
    {
        if (template == null || CombatManager.Instance == null) return;
        Camera cam = Camera.main;

        foreach (EnemyController enemy in CombatManager.Instance.enemies)
        {
            if (enemy == null || entries.ContainsKey(enemy)) continue;
            entries[enemy] = Create(enemy);
        }

        stale.Clear();
        foreach (var pair in entries)
        {
            EnemyController enemy = pair.Key;
            Entry entry = pair.Value;
            if (enemy == null || enemy.IsDead)
            {
                stale.Add(enemy);
                continue;
            }

            float hp = enemy.maxHP > 0 ? (float)enemy.currentHP / enemy.maxHP : 0f;
            float stagger = enemy.maxPosture > 0 ? (float)(enemy.maxPosture - enemy.currentPosture) / enemy.maxPosture : 0f;
            float k = 1f - Mathf.Exp(-smoothSpeed * Time.unscaledDeltaTime);
            if (entry.hp != null) entry.hp.fillAmount = Mathf.Lerp(entry.hp.fillAmount, hp, k);
            if (entry.stagger != null) entry.stagger.fillAmount = Mathf.Lerp(entry.stagger.fillAmount, enemy.isStaggered ? 1f : stagger, k);

            // ตำแหน่งบนจอเหนือหัว (จุดเดิมของหลอด World Space ที่ปรับตามความสูงโมเดลแล้ว)
            bool visible = false;
            if (cam != null && entry.anchor != null)
            {
                Vector3 screen = cam.WorldToScreenPoint(entry.anchor.position);
                visible = screen.z > 0f;
                float scale = canvas != null ? canvas.scaleFactor : 1f;
                entry.root.position = new Vector3(screen.x, screen.y + screenLift * scale, 0f);
            }
            entry.group.alpha = Mathf.MoveTowards(entry.group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            UpdateStagger(entry, enemy.isStaggered);
            PlaceStatusRow(entry);
        }

        foreach (EnemyController enemy in stale)
        {
            Entry entry = entries[enemy];
            if (entry.root != null) Destroy(entry.root.gameObject);
            if (entry.status != null) Destroy(entry.status.gameObject);
            entries.Remove(enemy);
        }
    }

    // ติด Stagger: ป้าย STAGGERED เด้งขึ้นเหนือชื่อแล้วกระพริบช้า ๆ หลอด Stagger เต็มและวาบสีทอง HUD สั่นเล็กน้อยตอนเริ่ม
    private void UpdateStagger(Entry entry, bool staggered)
    {
        if (staggered && !entry.wasStaggered) entry.staggerPop = 1f;
        entry.wasStaggered = staggered;
        entry.staggerPop = Mathf.MoveTowards(entry.staggerPop, 0f, Time.unscaledDeltaTime * 2.5f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);

        if (entry.staggerLabel != null)
        {
            float target = staggered ? Mathf.Lerp(0.75f, 1f, pulse) : 0f;
            entry.staggerLabel.alpha = Mathf.MoveTowards(entry.staggerLabel.alpha, target, Time.unscaledDeltaTime * 5f);
            entry.staggerLabel.transform.localScale = Vector3.one * (1f + entry.staggerPop * 0.6f);
        }
        if (entry.stagger != null)
            entry.stagger.color = staggered ? Color.Lerp(staggerColor, Color.white, pulse * 0.6f) : entry.staggerBarColor;
        if (entry.staggerPop > 0f)
            entry.root.position += (Vector3)(Random.insideUnitCircle * entry.staggerPop * 6f * (canvas != null ? canvas.scaleFactor : 1f));
    }

    // แถบสถานะอยู่ใต้ขอบล่างของหลอด (หลอดถูกย่อด้วย hudScale แถบไม่ย่อตาม จึงอ่านออก)
    private readonly Vector3[] corners = new Vector3[4];
    private void PlaceStatusRow(Entry entry)
    {
        if (entry.status == null) return;
        // ใช้เส้นกลางของหลอดล่างสุด: กรอบของภาพหลอดมีที่ว่างโปร่งใสด้านล่างมาก ถ้าใช้ขอบล่างแถบจะลอยห่างหลอด
        float bottom = float.MaxValue, left = float.MaxValue, right = float.MinValue;
        RectTransform[] targets = entry.bars != null && entry.bars.Length > 0 ? entry.bars : new[] { entry.root };
        foreach (RectTransform bar in targets)
        {
            if (bar == null) continue;
            bar.GetWorldCorners(corners);
            bottom = Mathf.Min(bottom, (corners[0].y + corners[1].y) * 0.5f);
            left = Mathf.Min(left, corners[0].x);
            right = Mathf.Max(right, corners[2].x);
        }
        if (bottom == float.MaxValue) return;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        entry.status.transform.position = new Vector3((left + right) * 0.5f, bottom - statusRowGap * scale, 0f);
        entry.status.alpha = entry.group.alpha;
        entry.status.badgeScale = statusRowScale;
    }

    private static List<StatusBadgeRow.Info> StatusesOf(EnemyController enemy, List<StatusBadgeRow.Info> list)
    {
        list.Clear();
        if (enemy == null) return list;
        StatusBadgeRow.Describe(enemy.Statuses, list);
        if (enemy.isMarkedByGun)
            list.Add(new StatusBadgeRow.Info
            {
                key = "GunMark", title = "MARKED", isBuff = false, glyph = StatusBadgeRow.StatusGlyph.Cross,
                color = new Color(1f, 0.32f, 0.3f), detail = "Free-aim shots deal +50% damage to it.", duration = "Until removed",
            });
        return list;
    }

    private Entry Create(EnemyController enemy)
    {
        RectTransform root = Instantiate(template, HudParent());
        root.gameObject.SetActive(true);
        root.name = "EnemyHUD_" + enemy.enemyName;
        root.anchorMin = root.anchorMax = Vector2.zero;
        root.localScale = Vector3.one * hudScale;
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

        var entry = new Entry
        {
            root = root,
            group = root.gameObject.AddComponent<CanvasGroup>(),
            hp = Find<Image>(root, "HPBar/MainBar"),
            stagger = Find<Image>(root, "Stagger/Image (2)"),
            label = root.GetComponentInChildren<TextMeshProUGUI>(true),
        };
        entry.group.alpha = 0f;
        if (entry.label != null) entry.label.text = enemy.enemyName;
        if (entry.hp != null) entry.hp.fillAmount = 1f;
        if (entry.stagger != null) { entry.stagger.fillAmount = 0f; entry.staggerBarColor = entry.stagger.color; }

        // ป้าย STAGGERED: ก๊อปป้ายชื่อ (ฟอนต์/สไตล์เดียวกัน) วางเหนือชื่อ ซ่อนไว้จนติด Stagger
        if (entry.label != null)
        {
            TextMeshProUGUI tag = Instantiate(entry.label, entry.label.transform.parent);
            tag.name = "StaggerLabel";
            tag.text = staggerText;
            tag.color = staggerColor;
            tag.fontStyle = FontStyles.Bold;
            tag.alpha = 0f;
            RectTransform rect = tag.rectTransform;
            rect.anchoredPosition = entry.label.rectTransform.anchoredPosition + Vector2.up * entry.label.rectTransform.rect.height * staggerLabelLift;
            entry.staggerLabel = tag;
        }

        // แถบสถานะแบบใหม่ (ชื่อเต็ม + เทิร์นที่เหลือ + คำอธิบายตอนชี้เมาส์) แทนไอคอนตัวย่อบนหัวศัตรู
        if (showStatusRow)
        {
            var bars = new List<RectTransform>();
            foreach (string path in new[] { "HPBar", "Stagger" })
                if (root.Find(path) is RectTransform bar) bars.Add(bar);
            entry.bars = bars.ToArray();
            entry.status = StatusBadgeRow.Create(HudParent(), "EnemyStatus_" + enemy.enemyName, entry.label != null ? entry.label.font : null, StatusBadgeRow.Align.Center);
            var list = new List<StatusBadgeRow.Info>();
            entry.status.source = () => StatusesOf(enemy, list);
        }

        // หลอดเดิมบนตัวศัตรู: ซ่อนภาพหลอด (และไอคอนสถานะตัวย่อเดิม ถ้าใช้แถบใหม่) แล้วใช้ตำแหน่งของมันเป็นจุดยึด
        EnemyUI old = enemy.GetComponentInChildren<EnemyUI>(true);
        entry.anchor = old != null ? old.transform : enemy.transform;
        if (old != null)
        {
            foreach (Graphic graphic in old.GetComponentsInChildren<Graphic>(true))
            {
                if (!showStatusRow && old.statusContainer != null && graphic.transform.IsChildOf(old.statusContainer)) continue;
                graphic.enabled = false;
            }
            if (showStatusRow && old.statusContainer != null) old.statusContainer.gameObject.SetActive(false);
        }
        return entry;
    }

    private static T Find<T>(Transform root, string path) where T : Component
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }
}
