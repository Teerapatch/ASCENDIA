using UnityEngine;

// ชั้นวาดย่อยใน Canvas BattleUI (Overlay) ที่มีลำดับวาดของตัวเอง: Canvas ซ้อน + Override Sorting
// หลอดศัตรู (EnemyHUD) = ลำดับของ BattleUI - 2, เลขดาเมจ = - 1 ส่วนอื่นของ BattleUI (เมนู การ์ดสกิล ลำดับเทิร์น ฯลฯ) จึงวาดทับทั้งสองชั้น
// Overlay วาดหลังฉาก 3D เสมอ ของในชั้นเหล่านี้จึงไม่จมเข้าโมเดล และยังอยู่ใต้ ParryImpactFX (90) กับหน้าผลการต่อสู้ (80)
public static class BattleUILayers
{
    public const string EnemyHUDLayer = "EnemyHUD_Layer";
    public const string DamagePopupLayer = "DamagePopup_Layer";
    public const int EnemyHUDOffset = -2;
    public const int DamagePopupOffset = -1;

    // Canvas หลักของ UI ต่อสู้ (BattleUI ที่มี BattleArtUI) ถ้าเป็น Overlay; ฉากแบบเก่า (World Space) = null
    public static Canvas FindBattleCanvas()
    {
        BattleArtUI art = Object.FindAnyObjectByType<BattleArtUI>();
        if (art == null) return null;
        Canvas canvas = art.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? canvas : null;
    }

    // หา/สร้างชั้นชื่อ name ใต้ root (ยืดเต็มจอ หน่วยเป็นพิกเซลอ้างอิง 1920x1080 ตาม CanvasScaler ของ root)
    public static RectTransform Get(Canvas root, string name, int orderOffset)
    {
        root = root.rootCanvas;
        RectTransform rt = root.transform.Find(name) as RectTransform;
        if (rt == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = root.gameObject.layer;
            rt = (RectTransform)go.transform;
            rt.SetParent(root.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        Canvas canvas = rt.GetComponent<Canvas>();
        if (canvas == null) canvas = rt.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerID = root.sortingLayerID;
        canvas.sortingOrder = root.sortingOrder + orderOffset;
        return rt;
    }
}
