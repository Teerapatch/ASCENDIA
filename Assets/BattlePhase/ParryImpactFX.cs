using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// ความอลังการตอน Parry สำเร็จ (จุดขายของเกม): หยุดเวลา -> สโลว์แล้วเร่งกลับ, จอแฟลชขาว, วงแหวนกระแทก,
// เส้นความเร็วพุ่งออกจากจุดปะทะ, ตัวอักษร PARRY! (นับต่อเนื่อง), Post FX กระชาก (Chromatic/Lens/สี) และกล้องพุ่ง+เอียง
// สร้างตัวเองตอนใช้ครั้งแรก ถ้าอยากปรับค่าใน Inspector ให้วาง Component นี้ไว้ในฉากต่อสู้
public class ParryImpactFX : MonoBehaviour
{
    public static ParryImpactFX Instance;

    [Header("เวลา")]
    [Tooltip("หยุดเวลานิ่งสนิท (วินาทีจริง)")]
    public float freezeDuration = .2f;
    [Tooltip("หลังหยุดนิ่ง เวลาเริ่มที่ความเร็วนี้แล้วค่อย ๆ เร่งกลับเป็นปกติ")]
    [Range(.05f, 1f)] public float slowMoScale = .3f;
    public float slowMoDuration = .55f;

    [Header("กล้อง")]
    public float fovKick = 9f;
    public float cameraKickStrength = 1f;
    [Tooltip("Parry ต่อเนื่องแต่ละครั้งแรงขึ้นกี่ส่วน (สูงสุด 3 ครั้ง)")]
    public float streakBoost = .15f;

    [Header("จอแฟลช")]
    public Color flashColor = new Color(1f, .97f, .9f, 1f);
    [Range(0f, 1f)] public float flashAlpha = .5f;
    public float flashTime = .2f;

    [Header("วงแหวนกระแทก (พิกเซลที่ความสูงจอ 1080)")]
    public Color ringColor = new Color(1f, .93f, .75f, 1f);
    public float ringStartSize = 120f;
    public float ringEndSize = 1150f;
    public float ringTime = .42f;

    [Header("เส้นความเร็ว")]
    public int speedLineCount = 30;
    public Color speedLineColor = new Color(1f, 1f, 1f, .9f);
    public float speedLineTime = .35f;

    [Header("ตัวอักษร")]
    public bool showText = true;
    public string parryText = "PARRY!";
    public float fontSize = 104f;
    public Color textTop = Color.white;
    public Color textBottom = new Color(1f, .78f, .3f, 1f);
    public float textHoldTime = .45f;

    [Header("Post FX (URP Volume)")]
    public bool usePostFX = true;
    [Range(0f, 1f)] public float chromaticAberration = 1f;
    [Range(-1f, 1f)] public float lensDistortion = -.35f;
    [Range(-100f, 100f)] public float saturation = -45f;
    [Range(-100f, 100f)] public float contrast = 30f;
    public float postFXTime = .55f;

    public int Streak { get; private set; }

    private Canvas canvas;
    private RectTransform canvasRect;
    private Volume volume;
    private Coroutine slowRoutine, volumeRoutine;
    private float slowValue = -1f;
    private static Sprite ringSprite, lineSprite;

    public static ParryImpactFX Get()
    {
        if (Instance != null) return Instance;
        Instance = FindAnyObjectByType<ParryImpactFX>();
        if (Instance == null) Instance = new GameObject("ParryImpactFX").AddComponent<ParryImpactFX>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDisable()
    {
        // ฉากถูกปิดกลางสโลว์: คืนเวลาเป็นปกติ
        if (slowValue >= 0f && HitstopManager.BaseTimeScale == slowValue && !GameExitMenu.IsOpen) HitstopManager.SetBaseTimeScale(1f);
        slowValue = -1f;
    }

    public static void ResetStreak()
    {
        if (Instance != null) Instance.Streak = 0;
    }

    // worldPoint = จุดปะทะ (ตัวศัตรู), swipe = ทิศที่ผู้เล่นลากบนจอ
    public static void Play(Vector3 worldPoint, Vector2 swipe)
    {
        Get().PlayInternal(worldPoint, swipe);
    }

    private void PlayInternal(Vector3 worldPoint, Vector2 swipe)
    {
        Streak++;
        float strength = 1f + Mathf.Min(Streak - 1, 3) * streakBoost;
        if (swipe.sqrMagnitude < .0001f) swipe = Vector2.right;
        swipe.Normalize();

        // เวลา: หยุดนิ่ง แล้วสโลว์เร่งกลับ
        if (HitstopManager.Instance != null) HitstopManager.Instance.Freeze(freezeDuration * strength);
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(SlowMotion(freezeDuration * strength));

        BattleCameraController cam = BattleCameraController.Instance;
        if (cam != null)
        {
            cam.Punch(fovKick * strength);
            cam.ParryKick(swipe, cameraKickStrength * strength);
        }

        EnsureCanvas();
        Camera main = Camera.main;
        Vector2 screen = new Vector2(Screen.width * .5f, Screen.height * .5f);
        if (main != null)
        {
            Vector3 p = main.WorldToScreenPoint(worldPoint);
            if (p.z > 0f) screen = p;
        }
        float unit = Screen.height / 1080f;

        StartCoroutine(Flash(strength));
        StartCoroutine(Ring(screen, unit * strength, 0f, 1f));
        StartCoroutine(Ring(screen, unit * strength * .7f, .06f, .55f));
        for (int i = 0; i < speedLineCount; i++) StartCoroutine(SpeedLine(screen, unit, swipe, i));
        if (showText) StartCoroutine(Label(screen, unit, swipe, strength));
        if (usePostFX) PulseVolume(strength);
    }

    private IEnumerator SlowMotion(float freeze)
    {
        yield return new WaitForSecondsRealtime(freeze);
        slowValue = -1f;
        for (float t = 0f; t < slowMoDuration; t += Time.unscaledDeltaTime)
        {
            if (GameExitMenu.IsOpen) { yield return null; continue; }
            // ระบบอื่นเปลี่ยนความเร็วเวลาแล้ว (เช่น Hint ของการโจมตีครั้งถัดไปทำสโลว์ของมันเอง) -> ปล่อยให้มันคุม
            if (slowValue >= 0f && HitstopManager.BaseTimeScale != slowValue) { slowValue = -1f; slowRoutine = null; yield break; }
            slowValue = Mathf.Lerp(slowMoScale, 1f, Mathf.SmoothStep(0f, 1f, t / slowMoDuration));
            HitstopManager.SetBaseTimeScale(slowValue);
            yield return null;
        }
        while (GameExitMenu.IsOpen) yield return null;
        if (slowValue < 0f || HitstopManager.BaseTimeScale == slowValue) HitstopManager.SetBaseTimeScale(1f);
        slowValue = -1f;
        slowRoutine = null;
    }

    // ---------------- UI
    private void EnsureCanvas()
    {
        if (canvas != null) return;
        GameObject go = new GameObject("ParryImpactCanvas", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        canvasRect = (RectTransform)go.transform;
        if (ringSprite == null) ringSprite = MakeRingSprite();
        if (lineSprite == null) lineSprite = MakeLineSprite();
    }

    private Image NewImage(string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasRect, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        return image;
    }

    private Vector2 ToCanvas(Vector2 screen)
    {
        return screen / Mathf.Max(.0001f, canvas.scaleFactor);
    }

    private IEnumerator Flash(float strength)
    {
        Image image = NewImage("Flash", null, flashColor);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        float peak = Mathf.Clamp01(flashAlpha * strength);
        for (float t = 0f; t < flashTime; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / flashTime;
            image.color = new Color(flashColor.r, flashColor.g, flashColor.b, peak * k * k);
            yield return null;
        }
        Destroy(image.gameObject);
    }

    private IEnumerator Ring(Vector2 screen, float unit, float delay, float alpha)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        Image image = NewImage("Ring", ringSprite, ringColor);
        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = ToCanvas(screen);
        float scale = unit / Mathf.Max(.0001f, canvas.scaleFactor);
        for (float t = 0f; t < ringTime; t += Time.unscaledDeltaTime)
        {
            float k = t / ringTime;
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            float size = Mathf.Lerp(ringStartSize, ringEndSize, ease) * scale;
            rect.sizeDelta = new Vector2(size, size);
            image.color = new Color(ringColor.r, ringColor.g, ringColor.b, alpha * (1f - k));
            yield return null;
        }
        Destroy(image.gameObject);
    }

    // เส้นความเร็วแบบอนิเมะ: พุ่งออกจากจุดปะทะทุกทิศ หนาแน่นตามทิศที่ลาก
    private IEnumerator SpeedLine(Vector2 screen, float unit, Vector2 swipe, int index)
    {
        float baseAngle = Mathf.Atan2(swipe.y, swipe.x) * Mathf.Rad2Deg;
        float angle = index % 3 == 0
            ? baseAngle + Random.Range(-25f, 25f) + (index % 2 == 0 ? 0f : 180f)
            : Random.Range(0f, 360f);
        Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        float scale = unit / Mathf.Max(.0001f, canvas.scaleFactor);
        float startRadius = Random.Range(140f, 320f) * scale;
        float travel = Random.Range(160f, 380f) * scale;
        float length = Random.Range(260f, 720f) * scale;
        float width = Random.Range(3f, 10f) * scale;
        float delay = Random.Range(0f, .05f);
        float life = speedLineTime * Random.Range(.75f, 1.15f);

        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        Image image = NewImage("SpeedLine", lineSprite, speedLineColor);
        RectTransform rect = image.rectTransform;
        rect.pivot = new Vector2(0f, .5f);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        Vector2 origin = ToCanvas(screen);
        for (float t = 0f; t < life; t += Time.unscaledDeltaTime)
        {
            float k = t / life;
            float ease = 1f - (1f - k) * (1f - k);
            rect.anchoredPosition = origin + direction * (startRadius + travel * ease);
            rect.sizeDelta = new Vector2(length * (1f - .4f * k), width * (1f - k));
            image.color = new Color(speedLineColor.r, speedLineColor.g, speedLineColor.b, speedLineColor.a * (1f - k));
            yield return null;
        }
        Destroy(image.gameObject);
    }

    private IEnumerator Label(Vector2 screen, float unit, Vector2 swipe, float strength)
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null) yield break;

        GameObject go = new GameObject("ParryText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvasRect, false);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = Streak > 1 ? $"{parryText.TrimEnd('!')} x{Streak}" : parryText;
        label.fontSize = fontSize * strength;
        label.fontStyle = FontStyles.Bold | FontStyles.Italic;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableVertexGradient = true;
        label.colorGradient = new VertexGradient(textTop, textTop, textBottom, textBottom);
        label.outlineWidth = .22f;
        label.outlineColor = new Color32(40, 18, 0, 255);

        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.sizeDelta = new Vector2(1200f, 200f);
        float scale = unit / Mathf.Max(.0001f, canvas.scaleFactor);
        Vector2 size = new Vector2(Screen.width, Screen.height) / Mathf.Max(.0001f, canvas.scaleFactor);
        Vector2 at = ToCanvas(screen) + new Vector2(0f, 170f * scale);
        at.x = Mathf.Clamp(at.x, 320f * scale, size.x - 320f * scale);
        at.y = Mathf.Clamp(at.y, 120f * scale, size.y - 120f * scale);
        float tilt = Mathf.Clamp(-swipe.x * 8f, -8f, 8f) + 4f;
        rect.localRotation = Quaternion.Euler(0f, 0f, tilt);

        // เด้งใหญ่ -> ขนาดจริง, ค้าง, แล้วลอยขึ้นจางหาย
        const float pop = .12f, fade = .3f;
        float total = pop + textHoldTime + fade;
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            float s, a = 1f, rise = 0f;
            if (t < pop) s = Mathf.Lerp(2.4f, 1f, 1f - Mathf.Pow(1f - t / pop, 3f));
            else if (t < pop + textHoldTime) s = 1f + .04f * Mathf.Sin((t - pop) * 30f) * (1f - (t - pop) / textHoldTime);
            else
            {
                float k = (t - pop - textHoldTime) / fade;
                s = 1f + .1f * k;
                a = 1f - k;
                rise = 40f * k;
            }
            rect.localScale = Vector3.one * s;
            rect.anchoredPosition = at + new Vector2(0f, rise * scale);
            label.alpha = a;
            yield return null;
        }
        Destroy(go);
    }

    // ---------------- Post FX
    private void PulseVolume(float strength)
    {
        if (volume == null)
        {
            GameObject go = new GameObject("ParryImpactVolume");
            go.transform.SetParent(transform, false);
            // ใช้ Layer เดียวกับ Volume ของอาร์ต กล้องจึงอ่าน Volume นี้แน่นอน
            Volume art = FindAnyObjectByType<Volume>();
            if (art != null) go.layer = art.gameObject.layer;
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 500f;
            volume.weight = 0f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<ChromaticAberration>(true).intensity.Override(chromaticAberration);
            LensDistortion lens = profile.Add<LensDistortion>(true);
            lens.intensity.Override(lensDistortion);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(saturation);
            color.contrast.Override(contrast);
            volume.sharedProfile = profile;
        }
        if (volumeRoutine != null) StopCoroutine(volumeRoutine);
        volumeRoutine = StartCoroutine(VolumePulse(Mathf.Clamp01(strength)));
    }

    private IEnumerator VolumePulse(float peak)
    {
        const float attack = .03f;
        for (float t = 0f; t < attack + postFXTime; t += Time.unscaledDeltaTime)
        {
            volume.weight = t < attack ? peak * t / attack : peak * (1f - Mathf.SmoothStep(0f, 1f, (t - attack) / postFXTime));
            yield return null;
        }
        volume.weight = 0f;
        volumeRoutine = null;
    }

    private void OnDestroy()
    {
        if (volume != null && volume.sharedProfile != null) Destroy(volume.sharedProfile);
        if (Instance == this) Instance = null;
    }

    // ---------------- Sprites (สร้างในโค้ด ไม่ต้องมีไฟล์ภาพ)
    private static Sprite MakeRingSprite()
    {
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = new Vector2(x - size * .5f + .5f, y - size * .5f + .5f).magnitude / (size * .5f);
            float band = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.74f, .9f, d)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.9f, 1f, d)));
            float glow = .18f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, .9f, d)) * (d < 1f ? 1f : 0f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(Mathf.Max(band, glow)) * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
    }

    private static Sprite MakeLineSprite()
    {
        const int w = 128, h = 8;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            // หัวแหลมด้านใน ปลายหนาด้านนอก ขอบนุ่ม
            float along = x / (float)(w - 1);
            float across = 1f - Mathf.Abs((y + .5f) / h - .5f) * 2f;
            float a = Mathf.SmoothStep(0f, 1f, along * 1.6f) * Mathf.SmoothStep(0f, 1f, across * (0.3f + along)) * (1f - Mathf.SmoothStep(.85f, 1f, along));
            pixels[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0f, .5f));
    }
}
