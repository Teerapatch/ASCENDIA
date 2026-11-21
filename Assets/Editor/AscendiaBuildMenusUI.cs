using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools/ASCENDIA/UI/*: สร้างหน้า UI ชุดใหม่จากภาพของอาร์ต (UI_New) เป็นของจริงใน Hierarchy/Prefab แล้วแต่งต่อได้เลย
//  - Build Settings Prefab        -> Assets/Resources/SettingsMenu.prefab (SettingsMenu) + ปุ่ม SETTINGS ในหน้าต่าง Esc (GameExitMenu.prefab)
//  - Build Title Scene            -> Assets/_kin/Scenes/TitleScene.unity (TitleMenu) ใส่เป็นฉากแรกใน Build Settings
//  - Build Battle Result Screen   -> BattleResult_UI ใน TurnBase_Phase (BattleResultScreen)
// รันซ้ำได้: ของที่มีอยู่แล้วจะถูกสร้างใหม่ทับ (หน้า Settings / Title / Result) ถ้าแต่งเองแล้วอย่ารันซ้ำ
public static class AscendiaBuildMenusUI
{
    const string UI = "Assets/Assets (jmes)/UI_New/";
    const string FontBold = "Assets/Fonts/NotoSerifJP-Bold SDF.asset";
    const string FontRegular = "Assets/Fonts/NotoSerifJP-Regular SDF.asset";
    const string SettingsPrefab = "Assets/Resources/SettingsMenu.prefab";
    const string ExitMenuPrefab = "Assets/Resources/GameExitMenu.prefab";
    const string TitleScenePath = "Assets/_kin/Scenes/TitleScene.unity";
    const string BattleScene = "Assets/Scenes/TurnBase_Phase.unity";

    static readonly Color Ink = new Color(0.05f, 0.06f, 0.1f, 1f);
    static readonly Color Pale = new Color(0.85f, 0.88f, 1f, 1f);

    // ---------------------------------------------------------------- ตัวช่วย
    static Sprite S(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(UI + path);
    static TMP_FontAsset Bold => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBold);
    static TMP_FontAsset Regular => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRegular);

    static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        RectTransform rt = Rect(name, parent, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        return rt;
    }

    static Image Img(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
    {
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        RectTransform rt = Rect(name, parent, pos, box);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    static Canvas OverlayCanvas(GameObject go, int order)
    {
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ปุ่มแบบป้ายสีขาวของอาร์ต (TitleFrame) ตัวหนังสือสีเข้ม เหมือนปุ่ม BACK / CONFIRM ในฉากอาร์ต
    static Button PaleButton(string name, Transform parent, string label, Vector2 pos, Vector2 size)
    {
        RectTransform rt = Rect(name, parent, pos, size);
        Img(rt, null, new Color(1f, 1f, 1f, 0f), true); // พื้นรับคลิก
        // ภาพป้ายของอาร์ตมีขอบโปร่งใสเยอะ (แถบจริงสูงราว 13% ของภาพ) จึงขยายภาพให้แถบสูงเท่าปุ่ม
        RectTransform plate = Rect("Plate", rt, Vector2.zero, new Vector2(size.x * 1.08f, size.y * 7.2f));
        Image image = Img(plate, S("Camp 1/TitleFrame.png"), Color.white);
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.85f, 0.9f, 1f);
        colors.pressedColor = new Color(0.7f, 0.75f, 0.9f);
        button.colors = colors;
        TextMeshProUGUI t = Text("Label", rt, label, Bold, size.y * 0.38f, Ink, Vector2.zero, size);
        t.characterSpacing = 6f;
        UIHoverPop.Add(rt.gameObject, null, rt).hoverScale = 1.05f;
        return button;
    }

    // ปุ่มเมนูแบบพู่กันดำ (Actions_*_Frame) ตัวหนังสือขาว เหมือนเมนู ATTACK / SKILLS / SWITCH
    static Button BrushButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, float fontSize)
    {
        RectTransform rt = Rect(name, parent, pos, size);
        Image hit = Img(rt, null, new Color(1f, 1f, 1f, 0f), true);
        RectTransform frame = Rect("Frame", rt, Vector2.zero, size * 1.35f);
        Img(frame, S("Battle/Actions_Attack_Frame.png"), Color.white).preserveAspect = true;
        TextMeshProUGUI t = Text("Label", rt, label, Bold, fontSize, Color.white, new Vector2(10f, 0f), size);
        t.characterSpacing = 14f;
        t.fontStyle = FontStyles.Italic;
        Button button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        UIHoverPop pop = UIHoverPop.Add(rt.gameObject, null, rt);
        pop.hoverScale = 1.08f;
        return button;
    }

    static bool CanOpen(out string error)
    {
        error = null;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) { error = "ยังมีฉากที่ไม่ได้บันทึก: " + SceneManager.GetSceneAt(i).path; return false; }
        return true;
    }

    // ---------------------------------------------------------------- Settings
    [MenuItem("Tools/ASCENDIA/UI/Build Settings Prefab")]
    static void BuildSettingsMenu() => Debug.Log(BuildSettings());

    public static string BuildSettings()
    {
        var root = new GameObject("SettingsMenu");
        OverlayCanvas(root, 200);
        CanvasGroup group = root.AddComponent<CanvasGroup>();

        Img(Stretch("Dim", root.transform), null, new Color(0f, 0f, 0.02f, 0.78f), true);
        RectTransform window = Rect("Window", root.transform, Vector2.zero, new Vector2(1000f, 760f));
        Img(Rect("Back", window, Vector2.zero, new Vector2(1400f, 980f)), S("Battle/Actions_Cloak_BG.png"), new Color(0.28f, 0.32f, 0.5f, 0.95f)).preserveAspect = false;
        Img(Rect("TitleBack", window, new Vector2(0f, 300f), new Vector2(760f, 150f)), S("Camp 1/TitleBack.png"), new Color(0.75f, 0.82f, 1f, 0.35f));
        TextMeshProUGUI title = Text("Title", window, "SETTINGS", Bold, 64f, Color.white, new Vector2(0f, 300f), new Vector2(700f, 100f));
        title.characterSpacing = 14f;
        Img(Rect("Line", window, new Vector2(0f, 245f), new Vector2(520f, 28f)), S("Camp 1/Loadout_Title.png"), new Color(1f, 1f, 1f, 0.6f));

        RectTransform rows = Rect("Rows", window, new Vector2(0f, -20f), new Vector2(840f, 440f));
        VerticalLayoutGroup layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;

        SettingsRow(rows, "Row_Volume", "MASTER VOLUME", true);
        SettingsRow(rows, "Row_Shake", "CAMERA SHAKE", true);
        SettingsRow(rows, "Row_Screen", "SCREEN MODE", false);
        SettingsRow(rows, "Row_Resolution", "RESOLUTION", false);
        SettingsRow(rows, "Row_Quality", "GRAPHICS", false);
        SettingsRow(rows, "Row_VSync", "V-SYNC", false);

        PaleButton("Btn_Close", window, "BACK", new Vector2(0f, -320f), new Vector2(320f, 80f));

        SettingsMenu menu = root.AddComponent<SettingsMenu>();
        menu.group = group;
        menu.window = window;

        System.IO.Directory.CreateDirectory("Assets/Resources");
        PrefabUtility.SaveAsPrefabAsset(root, SettingsPrefab);
        Object.DestroyImmediate(root);
        return "Settings prefab: " + SettingsPrefab + "\n" + AddSettingsButtonToExitMenu();
    }

    static void SettingsRow(Transform parent, string name, string label, bool bar)
    {
        RectTransform row = Rect(name, parent, Vector2.zero, new Vector2(840f, 64f));
        Img(row, null, new Color(1f, 1f, 1f, 0.04f));
        Text("Label", row, label, Regular, 28f, Pale, new Vector2(-200f, 0f), new Vector2(420f, 60f), TextAlignmentOptions.Left).characterSpacing = 4f;
        Text("Value", row, "-", Bold, 28f, Color.white, new Vector2(230f, bar ? 8f : 0f), new Vector2(300f, 50f));
        Arrow(row, "Left", new Vector2(60f, 0f), true);
        Arrow(row, "Right", new Vector2(400f, 0f), false);
        if (bar)
        {
            RectTransform b = Rect("Bar", row, new Vector2(230f, -20f), new Vector2(240f, 8f));
            Img(b, S("Battle/Enemy_HPFrame.png"), new Color(1f, 1f, 1f, 0.6f));
            RectTransform fill = Rect("Fill", b, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            Image f = Img(fill, S("Battle/Enemy_HPBar3.png"), new Color(0.75f, 0.85f, 1f, 1f));
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.fillAmount = 1f;
        }
    }

    static void Arrow(Transform row, string name, Vector2 pos, bool left)
    {
        RectTransform rt = Rect(name, row, pos, new Vector2(44f, 44f));
        Image image = Img(rt, S("Camp 1/play_icon.png"), new Color(0.85f, 0.9f, 1f, 0.9f), true);
        image.preserveAspect = true;
        if (left) rt.localRotation = Quaternion.Euler(0f, 0f, 180f);
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UIHoverPop.Add(rt.gameObject, null, rt).hoverScale = 1.2f;
    }

    static string AddSettingsButtonToExitMenu()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ExitMenuPrefab);
        try
        {
            Transform window = root.transform.Find("QuitWindow");
            Transform resume = window != null ? window.Find("Btn_Resume") : null;
            if (resume == null) return "GameExitMenu: ไม่พบ QuitWindow/Btn_Resume";
            if (window.Find("Btn_Settings") != null) return "GameExitMenu: มี Btn_Settings อยู่แล้ว";
            GameObject settings = Object.Instantiate(resume.gameObject, window);
            settings.name = "Btn_Settings";
            RectTransform rt = (RectTransform)settings.transform;
            rt.anchoredPosition = new Vector2(0f, -10f);
            TMP_Text label = settings.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "SETTINGS";
            PrefabUtility.SaveAsPrefabAsset(root, ExitMenuPrefab);
            return "GameExitMenu: เพิ่ม QuitWindow/Btn_Settings";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------------------------------------------------------- Title
    [MenuItem("Tools/ASCENDIA/UI/Build Title Scene")]
    static void BuildTitleMenu() => Debug.Log(BuildTitle());

    public static string BuildTitle()
    {
        if (!CanOpen(out string error)) return error;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camObject.tag = "MainCamera";
        Camera cam = camObject.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.045f, 0.07f);
        cam.cullingMask = 0;
        cam.orthographic = true;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var root = new GameObject("TitleUI");
        OverlayCanvas(root, 0);
        RectTransform background = Rect("Background", root.transform, Vector2.zero, new Vector2(1920f, 1080f));
        Img(background, S("Camp/Camp_bg2.png"), Color.white);
        RectTransform shade = Stretch("Shade", root.transform);
        Img(shade, null, new Color(0f, 0f, 0.02f, 0.25f));
        RectTransform side = Rect("SideShade", root.transform, new Vector2(480f, 0f), new Vector2(1100f, 1300f));
        Img(side, S("Battle/Actions_Cloak_BG.png"), new Color(0.05f, 0.06f, 0.12f, 0.8f));

        RectTransform logo = Rect("Logo", root.transform, new Vector2(470f, 210f), new Vector2(900f, 300f));
        Img(Rect("Brush", logo, new Vector2(0f, -10f), new Vector2(980f, 230f)), S("Camp 1/TitleBack.png"), new Color(0.7f, 0.8f, 1f, 0.28f));
        TextMeshProUGUI name = Text("Name", logo, "ASCENDIA", Bold, 128f, Color.white, Vector2.zero, new Vector2(900f, 170f));
        name.characterSpacing = 18f;
        Img(Rect("Line", logo, new Vector2(0f, -95f), new Vector2(600f, 34f)), S("Camp 1/Loadout_Title.png"), new Color(1f, 1f, 1f, 0.75f));

        RectTransform menu = Rect("Menu", root.transform, new Vector2(470f, -170f), new Vector2(560f, 420f));
        BrushButton("Btn_Play", menu, "PLAY", new Vector2(0f, 120f), new Vector2(460f, 110f), 52f);
        BrushButton("Btn_Setting", menu, "SETTING", new Vector2(0f, 0f), new Vector2(460f, 110f), 44f);
        BrushButton("Btn_Credit", menu, "CREDIT", new Vector2(0f, -120f), new Vector2(460f, 110f), 44f);

        RectTransform credits = Stretch("Credits", root.transform);
        Img(credits, null, new Color(0f, 0f, 0.02f, 0.82f), true);
        RectTransform window = Rect("Window", credits, Vector2.zero, new Vector2(1000f, 700f));
        Img(Rect("Back", window, Vector2.zero, new Vector2(1300f, 900f)), S("Battle/Actions_Cloak_BG.png"), new Color(0.28f, 0.32f, 0.5f, 0.95f));
        TextMeshProUGUI ct = Text("Title", window, "CREDITS", Bold, 64f, Color.white, new Vector2(0f, 270f), new Vector2(700f, 100f));
        ct.characterSpacing = 14f;
        Img(Rect("Line", window, new Vector2(0f, 215f), new Vector2(520f, 28f)), S("Camp 1/Loadout_Title.png"), new Color(1f, 1f, 1f, 0.6f));
        TextMeshProUGUI body = Text("Body", window,
            "<size=30><b>ASCENDIA</b></size>\n\n<color=#AFC0FF>GAME DESIGN</color>\nYour Name\n\n<color=#AFC0FF>PROGRAMMING</color>\nYour Name\n\n<color=#AFC0FF>ART</color>\nYour Name",
            Regular, 28f, Color.white, new Vector2(0f, -20f), new Vector2(800f, 420f));
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 6f;
        PaleButton("Btn_Back", window, "BACK", new Vector2(0f, -290f), new Vector2(320f, 80f));

        Img(Stretch("Fade", root.transform), null, Color.black);
        root.AddComponent<TitleMenu>();

        EditorSceneManager.SaveScene(scene, TitleScenePath);

        // ฉากแรกของเกม
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == TitleScenePath);
        list.Insert(0, new EditorBuildSettingsScene(TitleScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        return "Title scene: " + TitleScenePath + " (first in Build Settings)";
    }

    // ---------------------------------------------------------------- Battle result
    [MenuItem("Tools/ASCENDIA/UI/Build Battle Result Screen")]
    static void BuildResultMenu() => Debug.Log(BuildBattleResult());

    public static string BuildBattleResult()
    {
        if (!CanOpen(out string error)) return error;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != BattleScene) scene = EditorSceneManager.OpenScene(BattleScene, OpenSceneMode.Single);
        foreach (GameObject old in scene.GetRootGameObjects())
            if (old.name == "BattleResult_UI") Object.DestroyImmediate(old);

        var root = new GameObject("BattleResult_UI");
        OverlayCanvas(root, 80);
        root.AddComponent<CanvasGroup>();

        Img(Stretch("Dim", root.transform), null, new Color(0f, 0f, 0.02f, 0.72f), true);

        RectTransform banner = Rect("Banner", root.transform, new Vector2(0f, 170f), new Vector2(1600f, 320f));
        Img(Rect("Stroke", banner, new Vector2(0f, 10f), new Vector2(1500f, 330f)), S("Camp 1/TitleBack.png"), Color.white);
        TextMeshProUGUI title = Text("Title", banner, "VICTORY", Bold, 140f, Color.white, new Vector2(0f, 10f), new Vector2(1400f, 200f));
        title.characterSpacing = 12f;
        Text("Subtitle", banner, "ENEMIES 0   |   WAVES 0/0   |   TURNS 0", Regular, 26f, Pale, new Vector2(0f, -120f), new Vector2(1400f, 50f)).characterSpacing = 4f;

        RectTransform rewards = Rect("Rewards", root.transform, new Vector2(0f, -110f), new Vector2(1300f, 260f));
        Text("Header", rewards, "REWARDS", Bold, 32f, Color.white, new Vector2(0f, 100f), new Vector2(600f, 50f)).characterSpacing = 10f;
        RectTransform list = Rect("List", rewards, new Vector2(0f, -20f), new Vector2(1300f, 180f));
        HorizontalLayoutGroup row = list.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 40f;
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;

        RectTransform item = Rect("RewardTemplate", rewards, Vector2.zero, new Vector2(230f, 170f));
        Img(Rect("Frame", item, new Vector2(0f, 22f), new Vector2(150f, 150f)), S("Camp 1/Loadout_SlotFrame.png"), new Color(1f, 1f, 1f, 0.9f));
        RectTransform icon = Rect("Icon", item, new Vector2(0f, 22f), new Vector2(90f, 90f));
        Img(icon, null, Color.white).preserveAspect = true;
        Text("Name", item, "Item", Regular, 24f, Color.white, new Vector2(0f, -55f), new Vector2(260f, 40f));
        Text("Amount", item, "x1", Bold, 32f, new Color(1f, 0.85f, 0.4f), new Vector2(78f, 55f), new Vector2(120f, 50f));
        item.gameObject.SetActive(false);

        PaleButton("Btn_Continue", root.transform, "CONTINUE", new Vector2(0f, -360f), new Vector2(500f, 92f));

        root.AddComponent<BattleResultScreen>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Battle result screen: BattleResult_UI in " + BattleScene;
    }
}
