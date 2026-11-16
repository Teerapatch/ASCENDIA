using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// หมอกดำที่ปลายล่างและปลายบนของหน้าผา: แถบไล่สีบนจอที่ยึดกับความสูงจริงของหน้าผาชั้นปัจจุบัน
// ปลายทั้งสองจึงจมหายไปในความมืด ดูเหมือนหน้าผายาวต่อไปไม่สิ้นสุด ค่าปรับอยู่บน PlayerCameraController
public class ClimbEdgeFog : MonoBehaviour
{
    private const float PlaneDistance = 5f;
    private const int SortingOrder = -100; // อยู่หลัง HUD ทั้งหมด แต่ทับฉากและตัวละคร

    private PlayerCameraController settings;
    private Canvas canvas;
    private RectTransform bottomSolid, bottomFade, topSolid, topFade;
    private Graphic[] graphics;

    public static ClimbEdgeFog Create(PlayerCameraController owner)
    {
        var go = new GameObject("ClimbEdgeFog", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(go, owner.gameObject.scene);
        ClimbEdgeFog fog = go.AddComponent<ClimbEdgeFog>();
        fog.settings = owner;
        fog.Build();
        return fog;
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = PlaneDistance;
        canvas.sortingOrder = SortingOrder;

        // ไล่ความทึบแบบโค้งนุ่ม: ทึบที่ขอบล่างของรูป จางหายที่ขอบบน
        var gradient = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
        {
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, y / 63f);
            gradient.SetPixel(0, y, new Color(1f, 1f, 1f, alpha * alpha));
        }
        gradient.Apply();

        bottomSolid = MakeBand("BottomSolid", null, false);
        bottomFade = MakeBand("BottomFade", gradient, false);
        topSolid = MakeBand("TopSolid", null, false);
        topFade = MakeBand("TopFade", gradient, true);
        graphics = GetComponentsInChildren<Graphic>();
    }

    private RectTransform MakeBand(string bandName, Texture2D texture, bool flip)
    {
        var go = new GameObject(bandName, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        RawImage image = go.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        if (flip) image.uvRect = new Rect(0f, 1f, 1f, -1f);
        var rect = (RectTransform)go.transform;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        ClimbableWall cliff = MapGenerator.CurrentCliff;
        bool show = settings != null && settings.edgeFog && cam != null && cliff != null && cliff.climbStart != null
            && !float.IsInfinity(cliff.MinClimbY) && !float.IsInfinity(cliff.MaxClimbY);
        if (canvas.enabled != show) canvas.enabled = show;
        if (!show) return;
        if (canvas.worldCamera != cam) canvas.worldCamera = cam;

        foreach (Graphic graphic in graphics) graphic.color = settings.edgeFogColor;

        // ความสูงบนจอของระดับต่าง ๆ วัดที่ผิวหน้าผา (จุดเกิดต่ำสุด) กล้องเป็น Orthographic มุมตายตัว ระดับเดียวกันจึงเป็นเส้นนอนบนจอ
        Vector3 anchor = cliff.climbStart.position;
        float fade = Mathf.Max(.5f, settings.fogFade);
        float bottomClear = ViewportY(cam, anchor, cliff.MinClimbY - settings.fogBottomStart);
        float bottomDark = ViewportY(cam, anchor, cliff.MinClimbY - settings.fogBottomStart - fade);
        float topClear = ViewportY(cam, anchor, cliff.MaxClimbY + settings.fogTopStart);
        float topDark = ViewportY(cam, anchor, cliff.MaxClimbY + settings.fogTopStart + fade);

        SetBand(bottomSolid, -2f, bottomDark);
        SetBand(bottomFade, bottomDark, bottomClear);
        SetBand(topFade, topClear, topDark);
        SetBand(topSolid, topDark, 3f);
    }

    private static float ViewportY(Camera cam, Vector3 anchor, float worldY)
    {
        return cam.WorldToViewportPoint(new Vector3(anchor.x, worldY, anchor.z)).y;
    }

    private static void SetBand(RectTransform rect, float fromY, float toY)
    {
        rect.anchorMin = new Vector2(-.1f, Mathf.Min(fromY, toY));
        rect.anchorMax = new Vector2(1.1f, Mathf.Max(fromY, toY));
    }
}
