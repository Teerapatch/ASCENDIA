using UnityEngine;

// ค่าตั้งค่าของเกม (หน้า Settings) เก็บใน PlayerPrefs และใช้ทันทีตอนเปิดเกม
// เสียง: ระดับเสียงรวม (AudioListener.volume) | จอ: เต็มจอ/หน้าต่าง, ความละเอียด, คุณภาพกราฟิก, VSync | ความแรงการสั่นของกล้อง
public static class GameSettings
{
    const string KeyVolume = "settings.masterVolume";
    const string KeyFullscreen = "settings.fullscreen";
    const string KeyResolution = "settings.resolution";
    const string KeyQuality = "settings.quality";
    const string KeyVSync = "settings.vsync";
    const string KeyShake = "settings.cameraShake";

    public static float MasterVolume { get; private set; } = 1f;
    public static bool Fullscreen { get; private set; } = true;
    public static int ResolutionIndex { get; private set; } = -1;
    public static int Quality { get; private set; }
    public static bool VSync { get; private set; } = true;
    public static float CameraShake { get; private set; } = 1f;

    // ความละเอียดที่เลือกได้ (ไม่ซ้ำ กว้าง x สูง เรียงจากเล็กไปใหญ่)
    public static Resolution[] Resolutions
    {
        get
        {
            var list = new System.Collections.Generic.List<Resolution>();
            foreach (Resolution r in Screen.resolutions)
            {
                if (list.Exists(x => x.width == r.width && x.height == r.height)) continue;
                list.Add(r);
            }
            if (list.Count == 0) list.Add(new Resolution { width = Screen.width, height = Screen.height });
            return list.ToArray();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStart()
    {
        MasterVolume = PlayerPrefs.GetFloat(KeyVolume, 1f);
        Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        ResolutionIndex = PlayerPrefs.GetInt(KeyResolution, -1);
        Quality = PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel());
        VSync = PlayerPrefs.GetInt(KeyVSync, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        CameraShake = PlayerPrefs.GetFloat(KeyShake, 1f);
        Apply(applyScreen: !Application.isEditor); // ใน Editor ไม่ไปยุ่งขนาดหน้าต่าง Game
    }

    public static void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); AudioListener.volume = MasterVolume; Save(); }
    public static void SetCameraShake(float value) { CameraShake = Mathf.Clamp01(value); Save(); }
    public static void SetQuality(int value) { Quality = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1); QualitySettings.SetQualityLevel(Quality, true); ApplyVSync(); Save(); }
    public static void SetVSync(bool value) { VSync = value; ApplyVSync(); Save(); }
    public static void SetFullscreen(bool value) { Fullscreen = value; ApplyScreen(); Save(); }
    public static void SetResolution(int index) { ResolutionIndex = index; ApplyScreen(); Save(); }

    // ความละเอียดที่ใช้อยู่ตอนนี้ในรายการ (ยังไม่เคยตั้ง = ตรงกับจอปัจจุบัน หรือใหญ่สุด)
    public static int CurrentResolutionIndex()
    {
        Resolution[] list = Resolutions;
        if (ResolutionIndex >= 0 && ResolutionIndex < list.Length) return ResolutionIndex;
        for (int i = 0; i < list.Length; i++)
            if (list[i].width == Screen.width && list[i].height == Screen.height) return i;
        return list.Length - 1;
    }

    private static void Apply(bool applyScreen)
    {
        AudioListener.volume = MasterVolume;
        if (Quality >= 0 && Quality < QualitySettings.names.Length) QualitySettings.SetQualityLevel(Quality, true);
        ApplyVSync();
        if (applyScreen) ApplyScreen();
    }

    private static void ApplyVSync() => QualitySettings.vSyncCount = VSync ? 1 : 0;

    private static void ApplyScreen()
    {
        Resolution[] list = Resolutions;
        int index = CurrentResolutionIndex();
        Resolution r = list[Mathf.Clamp(index, 0, list.Length - 1)];
        Screen.SetResolution(r.width, r.height, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    private static void Save()
    {
        PlayerPrefs.SetFloat(KeyVolume, MasterVolume);
        PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KeyResolution, ResolutionIndex);
        PlayerPrefs.SetInt(KeyQuality, Quality);
        PlayerPrefs.SetInt(KeyVSync, VSync ? 1 : 0);
        PlayerPrefs.SetFloat(KeyShake, CameraShake);
        PlayerPrefs.Save();
    }
}
