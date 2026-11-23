using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// สร้างเลขดาเมจ / ข้อความลอย (DamagePopup) บนชั้น DamagePopup_Layer ของ BattleUI
// ลำดับวาด: เหนือหลอดศัตรู (EnemyHUD_Layer) ใต้ UI อื่นของ BattleUI ดู BattleUILayers
public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance;

    [Tooltip("Prefab ที่มี DamagePopup แบบ UI (DamagePopup_Prefab) ถ้ายังเป็นแบบ 3D เก่าจะสร้างหน้าตาพื้นฐานในโค้ดแทน")]
    public GameObject damagePopupPrefab;

    [Header("ชั้นวาด")]
    [Tooltip("ไม่มี BattleUI แบบ Overlay: สร้าง Canvas ของตัวเองที่ลำดับนี้")]
    public int fallbackSortingOrder = -1;

    [Header("กันเลขซ้อน (พิกเซลที่ 1080p)")]
    [Tooltip("ขยับจุดเกิดแบบสุ่ม")]
    public float jitter = 22f;
    [Tooltip("ป็อปอัปที่เกิดภายในเวลานี้ใกล้จุดเดิม จะถูกต่อแถวขึ้นไปแทนการซ้อนทับ")]
    public float stackWindow = 0.4f;
    public float stackRadius = 90f;
    public Vector2 stackStep = new Vector2(22f, 42f);
    [Tooltip("ข้อความ (Label) เกิดสูงกว่าตัวเลข")]
    public float labelLift = 55f;

    private RectTransform layer;
    private Canvas rootCanvas;
    private DamagePopup template;
    private readonly List<DamagePopup> active = new List<DamagePopup>();

    private void Awake()
    {
        Instance = this;
    }

    public void CreatePopup(Vector3 position, int damageAmount, bool isCritical, Color color)
    {
        Spawn(position, damageAmount.ToString(), isCritical ? DamagePopup.Style.Critical : DamagePopup.Style.Number, color);
    }

    // ข้อความลอย (เช่น "EXPOSED", "+2 AP", "BREAK!", ชื่อท่า)
    public void CreateTextPopup(Vector3 position, string text, Color color)
    {
        Spawn(position, text, DamagePopup.Style.Label, color);
    }

    private void Spawn(Vector3 position, string value, DamagePopup.Style style, Color color)
    {
        if (!EnsureLayer()) return;

        DamagePopup popup = Instantiate(template, layer, false);
        popup.name = "Popup_" + value;
        popup.Play(position, StackOffset(position, style), value, style, color, rootCanvas);
        active.Add(popup);
    }

    // หลายฮิตติดกันที่จุดเดียว: เลขใหม่ต่อแถวขึ้นไปสลับซ้าย/ขวา
    private Vector2 StackOffset(Vector3 position, DamagePopup.Style style)
    {
        Vector2 offset = Random.insideUnitCircle * jitter;
        if (style == DamagePopup.Style.Label) offset.y += labelLift;

        Camera cam = Camera.main;
        active.RemoveAll(p => p == null);
        if (cam == null) return offset;

        Vector2 screen = cam.WorldToScreenPoint(position);
        float factor = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        int count = 0;
        foreach (DamagePopup other in active)
        {
            if (other.Age > stackWindow || (other.CurrentStyle == DamagePopup.Style.Label) != (style == DamagePopup.Style.Label)) continue;
            Vector2 otherScreen = cam.WorldToScreenPoint(other.Anchor);
            if ((otherScreen - screen).magnitude <= stackRadius * factor) count++;
        }
        if (count > 0)
            offset += new Vector2((count % 2 == 1 ? 1f : -1f) * stackStep.x, stackStep.y * count);
        return offset;
    }

    private bool EnsureLayer()
    {
        if (layer != null && template != null) return true;

        if (layer == null)
        {
            Canvas battle = BattleUILayers.FindBattleCanvas();
            if (battle != null)
            {
                rootCanvas = battle;
                layer = BattleUILayers.Get(battle, BattleUILayers.DamagePopupLayer, BattleUILayers.DamagePopupOffset);
            }
            else
            {
                // ฉากเก่าที่ไม่มี BattleUI แบบ Overlay: Canvas ของตัวเอง
                var go = new GameObject("DamagePopupCanvas", typeof(RectTransform));
                rootCanvas = go.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                rootCanvas.sortingOrder = fallbackSortingOrder;
                CanvasScaler scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                layer = (RectTransform)go.transform;
            }
        }

        if (template == null)
        {
            DamagePopup fromPrefab = damagePopupPrefab != null ? damagePopupPrefab.GetComponent<DamagePopup>() : null;
            if (fromPrefab != null && fromPrefab.text != null && fromPrefab.content != null)
            {
                template = fromPrefab;
            }
            else
            {
                // Prefab ยังเป็นแบบ 3D เก่า: ตัวหนังสือล้วนพร้อมขอบเข้ม (ไม่มีภาพของอาร์ตตอนรัน)
                var go = new GameObject("DamagePopup_Default", typeof(RectTransform));
                go.SetActive(false);
                go.transform.SetParent(layer, false);
                TMP_FontAsset font = TMP_Settings.defaultFontAsset;
                template = DamagePopup.Build(go, null, null, null, font, DamagePopup.CreateTextMaterial(font));
            }
        }
        return layer != null && template != null;
    }
}
