using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ดึงฉากอาร์ตล่าสุด (Assets/Scenes/ReArt) มาแทนฉากเกม: ENVI (แสง, Volume, พร็อพ, เอฟเฟกต์, ท้องฟ้า/หมอก) และ UI ของอาร์ต
// ย้าย GameObject จากฉากอาร์ตเข้าฉากเกมตรง ๆ (ไม่แก้ไฟล์ฉากอาร์ต) แทนที่ของเดิมชื่อเดียวกัน
// สคริปต์ของเกมที่ติดอยู่บน Root เดิม (เช่น BattleArtUI บน BattleUI) ถูกย้ายไปติดตัวใหม่พร้อมค่าที่ตั้งไว้
// กล้องเกมยังเป็นตัวเดิม (มี CinemachineBrain และ Reference จากระบบเกม) แค่รับท่า/เลนส์/ลูกของกล้องอาร์ต
public static class AscendiaSyncReArt
{
    private class Spec
    {
        public string gameScene, artScene;
        public string[] exclude = new string[0];      // Root ของอาร์ตที่ไม่เอามา
        public string[] removeFromGame = new string[0]; // Root ของเกมที่ลบทิ้งเพิ่ม (ถูกรวมเข้า UI ใหม่แล้ว)
        public string[] vcams = new string[0];         // กล้อง Cinemachine ที่ต้องได้ท่าของกล้องอาร์ต
        public string[] includeInactive = new string[0]; // Root ที่อาร์ตปิดไว้แต่ต้องเอามาด้วย (หน้า UI ที่สคริปต์เปิดเอง)
        public string envContainer;                   // ใส่ ENVI ไว้ใต้ Root นี้ (ฉาก Climb)
        public string[] containerExclude = new string[0]; // Root ที่ย้ายมาแต่ไม่ใส่ใน envContainer (เช่น UI)
        public string cameraChildContainer;           // ใส่ลูกของกล้องอาร์ตไว้ใต้ลูกชื่อนี้ของกล้องเกม
        public bool copyCameraPose = true;
        public Action<Scene, Dictionary<string, GameObject>> after;
    }

    private static readonly Spec Battle = new Spec
    {
        gameScene = "Assets/Scenes/TurnBase_Phase.unity",
        artScene = "Assets/Scenes/ReArt/ENVI_Battle2.unity",
        exclude = new[] { "Canvas", "POSE_ATTACK", "BladeGuardians", "AxeGuardian", "WandGuardians", "Boss-Guardians2" },
        removeFromGame = new[] { "BattleHUD" },
        vcams = new[] { "CinemachineCamera" },
        after = AfterBattle,
    };

    private static readonly Spec Camp = new Spec
    {
        gameScene = "Assets/_kin/Scenes/CampScene.unity",
        artScene = "Assets/Scenes/ReArt/ENVI_Camp3.unity",
        // หน้า UI ชุดใหม่ (*_RE_UI) ผูกด้วย CampReArtUI ซึ่งเปิด/ปิดหน้าเอง ส่วน CampUI_New ของเกมเก็บไว้ (CampReArtUI ปิดตอนเล่น)
        exclude = new[] { "Canvas", "CampUI_New", "CampUI" },
        includeInactive = new[] { "BaseCamp_RE_UI", "Loadout_RE_UI" },
        after = AfterCamp,
        vcams = new[] { "CinemachineCamera" },
    };

    private static readonly Spec Climb = new Spec
    {
        gameScene = "Assets/_kin/Scenes/ClimbScene.unity",
        artScene = "Assets/Scenes/ReArt/ENVI_Climb1.unity",
        exclude = new[] { "Cliff_Prefabs_1", "Cliff_Prefabs_2", "Cliff_Prefabs_3", "RIG-DEMO_V1" },
        envContainer = "ENVI_Climb",
        containerExclude = new[] { "ClimbUI", "NodePath_RE_UI" },
        cameraChildContainer = "ENVI_CameraFX",
        copyCameraPose = false, // กล้องปีนเป็น Ortho ตามตัวผู้เล่น ใช้มุมของ ClimbCam/WalkCam/NodeCam
    };

    [MenuItem("Tools/ASCENDIA/Sync ReArt/Battle (TurnBase_Phase)")] public static void SyncBattle() => Run(Battle);
    [MenuItem("Tools/ASCENDIA/Sync ReArt/Camp (CampScene)")] public static void SyncCamp() => Run(Camp);
    [MenuItem("Tools/ASCENDIA/Sync ReArt/Climb (ClimbScene)")] public static void SyncClimb() => Run(Climb);

    public static string RunByKey(string key)
    {
        switch (key)
        {
            case "battle": return Run(Battle);
            case "camp": return Run(Camp);
            case "climb": return Run(Climb);
        }
        return "unknown key " + key;
    }

    private static string Run(Spec spec)
    {
        var log = new System.Text.StringBuilder();
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "อยู่ใน Play Mode";
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isDirty) return $"ฉาก {open.path} ยังไม่ได้บันทึก กรุณาบันทึกก่อน";
        }

        Scene game = EditorSceneManager.OpenScene(spec.gameScene, OpenSceneMode.Single);
        Scene art = EditorSceneManager.OpenScene(spec.artScene, OpenSceneMode.Additive);
        try
        {
            GameObject[] artRoots = art.GetRootGameObjects();
            var gameRoots = game.GetRootGameObjects().ToList();
            GameObject gameCamera = gameRoots.FirstOrDefault(g => g.name == "Main Camera" && g.GetComponent<Camera>() != null);
            GameObject artCamera = artRoots.FirstOrDefault(g => g.name == "Main Camera");

            // ---------- เลือก Root ของอาร์ตที่จะย้าย: เปิดอยู่, มีของจริง (ไม่ใช่ตัวคั่น), ไม่อยู่ในรายการยกเว้น
            var moving = new List<GameObject>();
            var hiddenInArt = new HashSet<string>(); // อาร์ตปิดไว้แล้ว ของชื่อเดียวกันในเกมต้องหายไปด้วย
            foreach (GameObject root in artRoots)
            {
                if (root == artCamera || root.name == "EventSystem") continue;
                if (!root.activeSelf && !spec.includeInactive.Contains(root.name))
                {
                    if (!spec.exclude.Contains(root.name)) hiddenInArt.Add(root.name);
                    log.AppendLine($"ข้าม (ปิดอยู่ในอาร์ต): {root.name}");
                    continue;
                }
                if (spec.exclude.Contains(root.name)) { log.AppendLine($"ข้าม (ยกเว้น): {root.name}"); continue; }
                if (root.transform.childCount == 0 && root.GetComponents<Component>().Length == 1) continue; // ตัวคั่น ______
                moving.Add(root);
            }
            var movingNames = new HashSet<string>(moving.Select(g => g.name));

            // ---------- จำสคริปต์ของเกมบน Root ที่จะถูกแทน แล้วลบของเดิม
            var carried = new Dictionary<string, List<(Type type, string json, bool enabled)>>();
            Transform container = spec.envContainer != null ? gameRoots.FirstOrDefault(g => g.name == spec.envContainer)?.transform : null;
            var toDelete = new List<GameObject>();
            foreach (GameObject root in gameRoots)
            {
                if (root == gameCamera || root.name == "EventSystem") continue;
                if (movingNames.Contains(root.name) || spec.removeFromGame.Contains(root.name)) toDelete.Add(root);
                else if (hiddenInArt.Contains(root.name) && !spec.containerExclude.Contains(root.name)) toDelete.Add(root);
            }
            if (container != null) foreach (Transform child in container) toDelete.Add(child.gameObject);

            foreach (GameObject old in toDelete)
            {
                GameObject replacement = moving.FirstOrDefault(g => g.name == old.name);
                if (replacement != null)
                {
                    var list = new List<(Type, string, bool)>();
                    foreach (MonoBehaviour mb in old.GetComponents<MonoBehaviour>())
                    {
                        if (mb == null) continue;
                        Type type = mb.GetType();
                        if (replacement.GetComponent(type) != null) continue;
                        list.Add((type, EditorJsonUtility.ToJson(mb), mb.enabled));
                    }
                    if (list.Count > 0) carried[old.name] = list;
                }
                log.AppendLine($"ลบของเดิม: {old.name}");
                UnityEngine.Object.DestroyImmediate(old);
            }

            // ---------- ย้ายของอาร์ตเข้าฉากเกม
            var placed = new Dictionary<string, GameObject>();
            foreach (GameObject root in moving)
            {
                SceneManager.MoveGameObjectToScene(root, game);
                if (container != null && !spec.containerExclude.Contains(root.name)) root.transform.SetParent(container, true);
                placed[root.name] = root;
                log.AppendLine($"ย้ายจากอาร์ต: {root.name}");
                if (carried.TryGetValue(root.name, out var comps))
                {
                    foreach (var (type, json, enabled) in comps)
                    {
                        Component added = root.AddComponent(type);
                        EditorJsonUtility.FromJsonOverwrite(json, added);
                        if (added is Behaviour behaviour) behaviour.enabled = enabled;
                        log.AppendLine($"   ติดสคริปต์เดิม: {type.Name}");
                    }
                }
            }

            // ---------- UI ของอาร์ตทำที่ 1920x1080 แบบ Constant Pixel Size: ให้ย่อ/ขยายตามจอแทน (ที่ 1080p หน้าตาเท่าเดิม)
            foreach (GameObject root in moving)
            {
                Canvas canvas = root.GetComponent<Canvas>();
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                if (canvas == null || scaler == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize) continue;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                log.AppendLine($"   {root.name}: Canvas ย่อตามจอ 1920x1080");
            }

            // ---------- กล้อง: ท่า เลนส์ และลูกของกล้องอาร์ต (ท้องฟ้า/เมฆที่ติดกล้อง)
            if (gameCamera != null && artCamera != null)
            {
                Camera src = artCamera.GetComponent<Camera>(), dst = gameCamera.GetComponent<Camera>();
                if (spec.copyCameraPose)
                {
                    gameCamera.transform.SetPositionAndRotation(artCamera.transform.position, artCamera.transform.rotation);
                    dst.fieldOfView = src.fieldOfView;
                    foreach (string vcamName in spec.vcams)
                    {
                        GameObject vcamObject = game.GetRootGameObjects().FirstOrDefault(g => g.name == vcamName);
                        if (vcamObject == null) continue;
                        vcamObject.transform.SetPositionAndRotation(artCamera.transform.position, artCamera.transform.rotation);
                        CinemachineCamera vcam = vcamObject.GetComponent<CinemachineCamera>();
                        if (vcam != null)
                        {
                            LensSettings lens = vcam.Lens;
                            lens.FieldOfView = src.fieldOfView;
                            lens.NearClipPlane = src.nearClipPlane;
                            lens.FarClipPlane = src.farClipPlane;
                            vcam.Lens = lens;
                        }
                        log.AppendLine($"กล้อง {vcamName} ใช้ท่าของกล้องอาร์ต {artCamera.transform.position} {artCamera.transform.eulerAngles}");
                    }
                }
                dst.clearFlags = src.clearFlags;
                dst.backgroundColor = src.backgroundColor;

                Transform childParent = gameCamera.transform;
                if (spec.cameraChildContainer != null)
                {
                    childParent = gameCamera.transform.Find(spec.cameraChildContainer);
                    if (childParent == null)
                    {
                        childParent = new GameObject(spec.cameraChildContainer).transform;
                        childParent.SetParent(gameCamera.transform, false);
                    }
                }
                var artChildren = artCamera.transform.Cast<Transform>().ToList();
                foreach (Transform child in artChildren)
                {
                    Transform existing = childParent.Find(child.name);
                    if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                }
                if (spec.cameraChildContainer != null)
                    foreach (Transform stale in childParent.Cast<Transform>().ToList()) UnityEngine.Object.DestroyImmediate(stale.gameObject);
                foreach (Transform child in artChildren)
                {
                    // ต้องย้ายข้ามฉากผ่าน Root ก่อน แล้วค่อยใส่ใต้กล้อง รักษาตำแหน่งสัมพัทธ์กับกล้องของอาร์ต
                    Vector3 localPosition = child.localPosition;
                    Quaternion localRotation = child.localRotation;
                    Vector3 localScale = child.localScale;
                    child.SetParent(null, false);
                    SceneManager.MoveGameObjectToScene(child.gameObject, game);
                    child.SetParent(childParent, false);
                    child.localPosition = localPosition;
                    child.localRotation = localRotation;
                    child.localScale = localScale;
                    log.AppendLine($"ลูกของกล้องจากอาร์ต: {child.name}");
                }
            }

            // ---------- ท้องฟ้า หมอก แสงรอบด้าน ตามอาร์ต
            CopyRenderSettings(art, game, log);

            spec.after?.Invoke(game, placed);
        }
        finally
        {
            EditorSceneManager.CloseScene(art, true); // ไม่บันทึกฉากอาร์ต
        }

        EditorSceneManager.MarkSceneDirty(game);
        EditorSceneManager.SaveScene(game);
        log.AppendLine("บันทึก " + spec.gameScene);
        Debug.Log("[Sync ReArt]\n" + log);
        return log.ToString();
    }

    private static void CopyRenderSettings(Scene from, Scene to, System.Text.StringBuilder log)
    {
        SceneManager.SetActiveScene(from);
        var skybox = RenderSettings.skybox;
        bool fog = RenderSettings.fog;
        var fogColor = RenderSettings.fogColor;
        var fogMode = RenderSettings.fogMode;
        float fogDensity = RenderSettings.fogDensity, fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
        var ambientMode = RenderSettings.ambientMode;
        var sky = RenderSettings.ambientSkyColor;
        var equator = RenderSettings.ambientEquatorColor;
        var ground = RenderSettings.ambientGroundColor;
        float ambientIntensity = RenderSettings.ambientIntensity;
        var reflectionMode = RenderSettings.defaultReflectionMode;
        float reflectionIntensity = RenderSettings.reflectionIntensity;
        var subtractive = RenderSettings.subtractiveShadowColor;
        SceneManager.SetActiveScene(to);
        RenderSettings.skybox = skybox;
        RenderSettings.fog = fog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = sky;
        RenderSettings.ambientEquatorColor = equator;
        RenderSettings.ambientGroundColor = ground;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.defaultReflectionMode = reflectionMode;
        RenderSettings.reflectionIntensity = reflectionIntensity;
        RenderSettings.subtractiveShadowColor = subtractive;
        log.AppendLine($"RenderSettings: skybox {(skybox != null ? skybox.name : "none")}, fog {fog} {fogMode} {fogColor}");
    }

    // ---------- Camp: CampReArtUI ผูกหน้า UI ชุดใหม่ ติดไว้กับ CampController (Root ที่เปิดอยู่เสมอ)
    private static void AfterCamp(Scene game, Dictionary<string, GameObject> placed)
    {
        GameObject controller = game.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<CampManager>() != null);
        if (controller != null && controller.GetComponent<CampReArtUI>() == null) controller.AddComponent<CampReArtUI>();
    }

    // ---------- Battle: UI ใหม่เป็น Overlay ชุดเดียว (Turn Order/Passive/อาวุธรวมอยู่ใน BattleUI แล้ว) และจุดเกิดศัตรูตามที่อาร์ตจัด
    private static void AfterBattle(Scene game, Dictionary<string, GameObject> placed)
    {
        if (placed.TryGetValue("BattleUI", out GameObject ui))
        {
            CanvasScaler scaler = ui.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (ui.GetComponent<BattleArtUI>() == null) ui.AddComponent<BattleArtUI>();
        }

        // อาร์ตวาง Guardian เป็นแถวที่ x 5 (กลาง z -17.5, หน้า -10.5, หลัง -23.5) พื้นสูง 4.25
        // จุดเกิดอยู่เหนือพื้น groundBelowSpawn (2.1) เพราะโมเดลถูกวางต่ำกว่าจุดเกิด
        GameObject characters = game.GetRootGameObjects().FirstOrDefault(g => g.name == "Characters");
        if (characters != null)
        {
            Vector3[] spots = { new Vector3(5f, 4.25f + 2.1f, -17.5f), new Vector3(5f, 4.25f + 2.1f, -10.5f), new Vector3(5f, 4.25f + 2.1f, -23.5f) };
            for (int i = 0; i < spots.Length; i++)
            {
                Transform spot = characters.transform.Find("Pos_" + i);
                if (spot != null) spot.position = spots[i];
            }
        }
    }
}
