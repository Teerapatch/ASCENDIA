using UnityEngine;
using UnityEngine.UI;

// ภาพดำเต็มจอสำหรับ Fade ของฉากปีน สร้างเองตอนใช้ครั้งแรก (ไม่ต้องตั้งในฉาก)
// FadeImage เดิมอยู่ใน Canvas ของ HUD เก่าที่ถูกปิดไป จึงไม่เคยแสดง
// เป็น Canvas Overlay ลำดับ -1: ทับฉาก 3D ทั้งหมด แต่อยู่ใต้ HUD (ClimbUI) และการ์ดเลือกเส้นทาง (NodePath_RE_UI) ที่ลำดับ 0
// การ์ดจึงยังเห็นอยู่บนความมืดระหว่างโหลดหน้าผาใหม่
public static class ScreenFader
{
    public const int SortingOrder = -1;

    private static Image image;

    public static Image Image
    {
        get
        {
            if (image == null) Create(); // ฉากถูกโหลดใหม่ = ของเดิมถูกลบไปแล้ว สร้างใหม่ในฉากปัจจุบัน
            return image;
        }
    }

    public static void SetAlpha(float alpha)
    {
        Color color = Image.color;
        color.a = alpha;
        image.color = color;
    }

    private static void Create()
    {
        GameObject canvasObject = new GameObject("ScreenFadeCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        GameObject fade = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)fade.transform;
        rect.SetParent(canvasObject.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        image = fade.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false; // ไม่บังการคลิก (การ์ดยังกดได้)
    }
}
