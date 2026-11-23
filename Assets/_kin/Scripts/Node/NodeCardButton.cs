using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// การ์ดเส้นทางหนึ่งใบใน NodePath_RE_UI (อาร์ต ReArt v2): ชื่อ/คำอธิบาย/ไอคอนตาม Node
// ชี้แล้วลอยขึ้นนิดหน่อย คลิกแล้วเลือก (เครื่องหมาย Selected + แสง Spot เบา ๆ ส่องการ์ด) การยืนยันอยู่ที่ปุ่ม Confirm ของ NodeSelectionManager
// Juice: ตอนโผล่การ์ดลอยขึ้นจากด้านล่างพร้อมหมุนเข้าที่และเด้งเกินนิดหน่อย ทุกอย่างใช้เวลาจริง (ไม่ขึ้นกับ timeScale)
// ขยับเฉพาะ CardFrame (ตำแหน่ง/มุม) ราก Card ถูก Layout Group ของ Card_Grid วางตำแหน่งอยู่
public class NodeCardButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public float hoverScale = 1.04f;
    public float selectedScale = 1.06f;
    public float chosenScale = 1.12f;

    [Header("โผล่")]
    public float introDuration = 0.55f;
    public float introRise = 320f;
    public float introSpin = 16f;

    [Header("ชี้ / เลือก")]
    public float hoverLift = 10f;
    public float selectedLift = 16f;
    [Range(0f, 1f), Tooltip("ใบที่เลือกตั้งตรงขึ้นเท่านี้ (1 = ตรงเลย)")]
    public float selectedStraighten = 0.25f;
    [Range(0f, 1f)] public float dimmedAlpha = 0.6f;

    [Header("แสง Spot (ใบที่เลือก)")]
    [Tooltip("วงแสงนุ่มด้านหลังการ์ด")]
    public Color spotPoolColor = new Color(0.82f, 0.9f, 1f, 0.32f);
    [Tooltip("ลำแสงจาง ๆ ส่องลงมาจากด้านบน")]
    public Color spotBeamColor = new Color(0.85f, 0.92f, 1f, 0.16f);
    public Vector2 spotPoolSize = new Vector2(620f, 700f);
    public Vector2 spotBeamSize = new Vector2(560f, 900f);
    [Tooltip("ความเร็วที่แสงค่อย ๆ สว่าง/ดับ")]
    public float spotFadeSpeed = 5f;

    public PathNode Node { get; private set; }

    private NodeSelectionManager manager;
    private GameObject selectedMark;
    private CanvasGroup group;
    private RectTransform frame;
    private Vector2 frameBasePos;
    private float frameTilt, side;
    private Vector3 baseScale;
    private float targetScale = 1f, shownScale = 1f, shownAlpha = 1f, alphaTarget = 1f;
    private bool hovered, selected, dimmed, locked, chosen;
    private float intro = 1f, introDelay;
    private float lift, straighten, light, punch;

    private RectTransform backLight;
    private Image poolImage, beamImage;

    private static Sprite poolSprite, beamSprite;

    public void Setup(PathNode node, NodeSelectionManager owner)
    {
        Node = node;
        manager = owner;
        baseScale = transform.localScale;
        group = gameObject.GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        frame = transform.Find("CardFrame") as RectTransform;
        Transform content = frame != null ? frame : transform;
        SetText(content, "Topic", node.DisplayName);
        SetText(content, "Desc", node.Description);
        Transform icon = content.Find("Image (1)/Image");
        if (icon != null && node.nodeIcon != null) icon.GetComponent<Image>().sprite = node.nodeIcon;

        // กราฟิกที่คัดจากอาร์ตปิด Raycast ไว้ เปิดเฉพาะกรอบการ์ดให้คลิกได้
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        Image hit = content.GetComponent<Image>();
        if (hit != null) hit.raycastTarget = true;

        if (frame != null)
        {
            frameBasePos = frame.anchoredPosition;
            frameTilt = Mathf.DeltaAngle(0f, frame.localEulerAngles.z); // มุมเอียงที่ Manager ตั้งไว้ตามอาร์ต
        }
        side = transform.GetSiblingIndex() % 2 == 0 ? 1f : -1f;
        BuildBackLight();

        Transform mark = transform.Find("Selected");
        selectedMark = mark != null ? mark.gameObject : null;
        SetSelected(false);
    }

    private static void SetText(Transform root, string child, string value)
    {
        Transform target = root.Find(child);
        TMP_Text text = target != null ? target.GetComponent<TMP_Text>() : null;
        if (text != null) text.text = value;
    }

    // แสง Spot ของใบที่เลือก: ลำแสงจาง ๆ ส่องลงมาจากด้านบน + วงแสงนุ่มด้านหลังการ์ด อยู่ใต้ CardFrame (ลูกตัวแรกของการ์ด)
    // ปกติมองไม่เห็น เลือกแล้วค่อย ๆ สว่างขึ้นเบา ๆ ไม่หมุน ไม่กระพริบ
    private void BuildBackLight()
    {
        if (poolSprite == null) poolSprite = MakeSprite(false);
        if (beamSprite == null) beamSprite = MakeSprite(true);

        GameObject root = new GameObject("BackLight", typeof(RectTransform));
        backLight = (RectTransform)root.transform;
        backLight.SetParent(transform, false);
        backLight.SetAsFirstSibling();
        backLight.anchorMin = backLight.anchorMax = backLight.pivot = new Vector2(0.5f, 0.5f);
        backLight.sizeDelta = Vector2.zero;

        // ลำแสง: ขอบล่างอยู่ที่กลางการ์ด ยื่นขึ้นไปด้านบน
        RectTransform beam = NewImage("Beam", backLight, beamSprite, out beamImage);
        beam.pivot = new Vector2(0.5f, 0f);
        beam.sizeDelta = spotBeamSize;
        RectTransform pool = NewImage("Pool", backLight, poolSprite, out poolImage);
        pool.sizeDelta = spotPoolSize;
        SetLightAlpha(0f);
    }

    private static RectTransform NewImage(string name, Transform parent, Sprite sprite, out Image image)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return rect;
    }

    // วาด Texture แสงเอง (ไม่ต้องมีไฟล์ภาพ): วงแสงจางจากกลาง / ลำแสงทรงกรวยที่บานลงด้านล่างและจางขึ้นด้านบน
    private static Sprite MakeSprite(bool asBeam)
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size; // v = 0 ล่าง, 1 บน
                float a;
                if (!asBeam)
                {
                    float dx = u * 2f - 1f, dy = v * 2f - 1f;
                    float fall = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = fall * fall * (3f - 2f * fall); // smoothstep: ขอบนุ่มมาก
                }
                else
                {
                    float halfWidth = Mathf.Lerp(0.48f, 0.16f, v); // กว้างที่ล่าง แคบที่บน
                    float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(u - 0.5f) / halfWidth);
                    float vertical = Mathf.SmoothStep(0f, 1f, v / 0.15f) * Mathf.Pow(1f - v, 1.4f); // ไล่เข้าตอนล่าง จางหายตอนบน
                    a = edge * vertical;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    public void PlayIntro(float delay)
    {
        intro = 0f;
        introDelay = delay;
        shownAlpha = 0f;
        if (group != null) group.alpha = 0f;
    }

    public void SetSelected(bool value)
    {
        if (value && !selected) punch = 0.5f;
        selected = value;
        if (selectedMark != null) selectedMark.SetActive(value);
        UpdateTarget();
    }

    public void SetDimmed(bool value)
    {
        dimmed = value;
        UpdateTarget();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        UpdateTarget();
        if (!locked && intro >= 1f) SoundManager.Play(Sfx.UIHover);
    }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; UpdateTarget(); }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (locked || manager == null || intro < 1f) return;
        SoundManager.Play(Sfx.UIClick);
        manager.SelectCard(Node);
    }

    private void UpdateTarget()
    {
        if (locked) return;
        targetScale = selected ? selectedScale : hovered ? hoverScale : 1f;
        alphaTarget = dimmed && !hovered ? dimmedAlpha : 1f;
    }

    // ยืนยันแล้ว: ใบที่เลือกขยายขึ้นเล็กน้อย แสงสว่างขึ้นอีกนิด ใบอื่นจางหาย
    public void ShowResult(bool isChosen)
    {
        locked = true;
        chosen = isChosen;
        targetScale = isChosen ? chosenScale : 0.92f;
        alphaTarget = isChosen ? 1f : 0.15f;
        if (isChosen)
        {
            light = 1.2f;
            punch = 0.5f;
        }
    }

    private void SetLightAlpha(float amount)
    {
        if (poolImage == null) return;
        float k = Mathf.Clamp(amount, 0f, 1.3f);
        Color pool = spotPoolColor;
        pool.a = Mathf.Clamp01(pool.a * k);
        poolImage.color = pool;
        Color beam = spotBeamColor;
        beam.a = Mathf.Clamp01(beam.a * k);
        beamImage.color = beam;
        backLight.gameObject.SetActive(amount > 0.003f);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float k = x - 1f;
        return 1f + c3 * k * k * k + c1 * k * k;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float blend = 1f - Mathf.Exp(-14f * dt);

        // ---- โผล่: ลอยขึ้นจากด้านล่าง หมุนเข้าที่ เด้งเกินเล็กน้อย
        float introAlpha = 1f, introScale = 1f, introRot = 0f;
        Vector2 introOffset = Vector2.zero;
        if (intro < 1f)
        {
            if (introDelay > 0f) introDelay -= dt;
            else
            {
                intro = Mathf.Min(1f, intro + dt / Mathf.Max(0.01f, introDuration));
                if (intro >= 1f) punch = 0.6f; // ลงจอดแล้วเด้ง
            }
            float e = EaseOutBack(intro);
            introOffset = new Vector2(0f, -introRise * (1f - e));
            introRot = introSpin * side * (1f - e);
            introScale = Mathf.LerpUnclamped(0.6f, 1f, e);
            introAlpha = Mathf.Clamp01(intro * 2.5f);
        }

        // ---- ชี้/เลือก: ลอยขึ้นเล็กน้อย ตั้งตรงขึ้นนิดหน่อย แสง Spot ค่อย ๆ สว่าง
        bool raised = locked ? chosen : selected;
        float liftGoal = raised ? selectedLift : hovered && !locked ? hoverLift : 0f;
        lift = Mathf.Lerp(lift, liftGoal, blend);
        straighten = Mathf.Lerp(straighten, raised ? selectedStraighten : 0f, blend);
        light = Mathf.Lerp(light, raised ? 1f : 0f, 1f - Mathf.Exp(-spotFadeSpeed * dt));
        punch = Mathf.MoveTowards(punch, 0f, dt * 4f);

        Vector2 framePos = frameBasePos + new Vector2(0f, lift) + introOffset;
        if (frame != null)
        {
            frame.anchoredPosition = framePos;
            frame.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(frameTilt, 0f, straighten) + introRot);
        }

        if (backLight != null)
        {
            backLight.anchoredPosition = framePos;
            SetLightAlpha(light * introAlpha);
        }

        // ---- ขนาด / ความโปร่งใส
        shownScale = Mathf.Lerp(shownScale, targetScale, blend);
        transform.localScale = baseScale * (shownScale * introScale + Mathf.Sin(punch * Mathf.PI) * 0.04f);

        shownAlpha = Mathf.Lerp(shownAlpha, alphaTarget, 1f - Mathf.Exp(-10f * dt));
        if (group != null) group.alpha = shownAlpha * introAlpha;
    }
}
