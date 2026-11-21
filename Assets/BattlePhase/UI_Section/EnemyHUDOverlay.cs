using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// หลอดเลือด/Stagger ของศัตรูชุด ReArt v2 (EnemyHUD บน BattleUI แบบ Overlay)
// คัดลอกแม่แบบหนึ่งชุดต่อศัตรูแต่ละตัว ลอยเหนือหัวตามตำแหน่งบนจอ แล้วซ่อนหลอดแบบ World Space เดิม (ไอคอนสถานะยังอยู่)
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
    }

    private readonly Dictionary<EnemyController, Entry> entries = new Dictionary<EnemyController, Entry>();
    private readonly List<EnemyController> stale = new List<EnemyController>();
    private Canvas canvas;

    private void Start()
    {
        canvas = GetComponent<Canvas>();
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
        }

        foreach (EnemyController enemy in stale)
        {
            Entry entry = entries[enemy];
            if (entry.root != null) Destroy(entry.root.gameObject);
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

    private Entry Create(EnemyController enemy)
    {
        RectTransform root = Instantiate(template, template.parent);
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

        // หลอดเดิมบนตัวศัตรู: ซ่อนภาพหลอด เหลือไอคอนสถานะ แล้วใช้ตำแหน่งของมันเป็นจุดยึด
        EnemyUI old = enemy.GetComponentInChildren<EnemyUI>(true);
        entry.anchor = old != null ? old.transform : enemy.transform;
        if (old != null)
        {
            foreach (Graphic graphic in old.GetComponentsInChildren<Graphic>(true))
            {
                if (old.statusContainer != null && graphic.transform.IsChildOf(old.statusContainer)) continue;
                graphic.enabled = false;
            }
        }
        return entry;
    }

    private static T Find<T>(Transform root, string path) where T : Component
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }
}
