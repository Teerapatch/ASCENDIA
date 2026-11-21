using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools/ASCENDIA/Bake Runtime UI: สร้างชิ้น UI ที่ปกติโค้ดสร้างตอนเล่น ให้อยู่ใน Hierarchy จะได้จัดตำแหน่ง/หน้าตาเองได้
// ตัวผูก UI (BattleArtUI / CampReArtUI) ใช้ของในฉากถ้ามี ไม่มีค่อยสร้างเหมือนเดิม จึงรันซ้ำได้ ของที่มีอยู่แล้วจะไม่ถูกแตะ
//  - Battle (TurnBase_Phase): BattleUI/Skills_List, BattleUI/Weapons_Ring, BattleUI/WeaponOption_Template
//  - Camp (CampScene): CampMessageCanvas/SystemMessage, Forge_RE_UI/Forge_Tab/BackButton
public static class AscendiaBakeRuntimeUI
{
    const string BattleScene = "Assets/Scenes/TurnBase_Phase.unity";
    const string CampScene = "Assets/_kin/Scenes/CampScene.unity";
    public const string WeaponOptionPrefab = "Assets/BattlePhase/UI_Section/Prefabs/WeaponOption_Template.prefab";

    [MenuItem("Tools/ASCENDIA/Bake Runtime UI/Battle")]
    private static void BakeBattleMenu() => Debug.Log(BakeBattle());

    [MenuItem("Tools/ASCENDIA/Bake Runtime UI/Camp")]
    private static void BakeCampMenu() => Debug.Log(BakeCamp());

    private static bool OpenScene(string path, out Scene scene, out string error)
    {
        scene = default;
        error = null;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isDirty && open.path != path) { error = "ยังมีฉากที่ไม่ได้บันทึก: " + open.path; return false; }
        }
        scene = SceneManager.GetActiveScene();
        if (scene.path != path) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return true;
    }

    public static string BakeBattle()
    {
        if (!OpenScene(BattleScene, out Scene scene, out string error)) return error;
        BattleArtUI art = Object.FindAnyObjectByType<BattleArtUI>(FindObjectsInactive.Include);
        if (art == null) return "ไม่พบ BattleArtUI ใน " + BattleScene;
        Transform canvas = art.transform;
        string log = "Battle:";

        if (canvas.Find("Skills_List") == null)
        {
            RectTransform list = BattleArtUI.CreateSkillList(canvas, art.skillCardsPosition, art.skillCardStep);
            Undo.RegisterCreatedObjectUndo(list.gameObject, "Bake Skills_List");
            log += " Skills_List";
        }
        if (canvas.Find("Weapons_Ring") == null)
        {
            RectTransform ring = BattleArtUI.CreateWeaponRing(canvas);
            Undo.RegisterCreatedObjectUndo(ring.gameObject, "Bake Weapons_Ring");
            log += " Weapons_Ring";
        }
        // ปุ่มอาวุธหนึ่งอันเป็น Prefab (WeaponOptionPrefab): แม่แบบในฉากเป็น Instance ของมัน แต่งที่ Prefab แล้วในฉากตามด้วย
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponOptionPrefab);
        Transform existing = canvas.Find("WeaponOption_Template");
        if (existing == null)
        {
            GameObject template;
            if (prefab != null)
            {
                template = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
                template.name = "WeaponOption_Template";
            }
            else
            {
                Transform styleObject = canvas.Find("Skills_Ex/Text (TMP) (4)");
                TMP_Text style = styleObject != null ? styleObject.GetComponent<TMP_Text>() : null;
                template = BattleArtUI.CreateWeaponOptionTemplate(canvas, style, BattleArtUI.GlowSprite(canvas),
                    art.weaponRingIconSize, art.weaponRingNameSize, art.weaponRingBackColor);
            }
            Undo.RegisterCreatedObjectUndo(template, "Bake WeaponOption_Template");
            existing = template.transform;
            log += " WeaponOption_Template";
        }
        if (prefab == null || !PrefabUtility.IsPartOfPrefabInstance(existing.gameObject))
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(WeaponOptionPrefab));
            // Prefab เก็บแบบเปิดอยู่ (เปิดดูใน Prefab Mode ได้ปกติ) ส่วนแม่แบบในฉากปิดไว้เป็นค่าทับ
            existing.gameObject.SetActive(true);
            prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(existing.gameObject, WeaponOptionPrefab, InteractionMode.AutomatedAction);
            existing.gameObject.SetActive(false);
            log += " prefab " + WeaponOptionPrefab;
        }
        if (prefab != null && art.weaponOptionPrefab != prefab)
        {
            Undo.RecordObject(art, "Assign weaponOptionPrefab");
            art.weaponOptionPrefab = prefab;
            EditorUtility.SetDirty(art);
            log += " (linked to BattleArtUI)";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return log == "Battle:" ? "Battle: มีครบแล้ว ไม่ได้เพิ่มอะไร" : log + " (saved)";
    }

    public static string BakeCamp()
    {
        if (!OpenScene(CampScene, out Scene scene, out string error)) return error;
        string log = "Camp:";
        GameObject forgePage = null, messageCanvas = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Forge_RE_UI") forgePage = root;
            if (root.name == "CampMessageCanvas") messageCanvas = root;
        }

        if (messageCanvas == null)
        {
            CampManager camp = Object.FindAnyObjectByType<CampManager>(FindObjectsInactive.Include);
            RectTransform canvas = CampReArtUI.CreateMessageCanvas();
            GameObject text;
            if (camp != null && camp.systemMessageText != null) text = Object.Instantiate(camp.systemMessageText.gameObject);
            else text = new GameObject("SystemMessage", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.name = "SystemMessage";
            text.SetActive(true);
            CampReArtUI.PlaceMessageText((RectTransform)text.transform, canvas);
            TextMeshProUGUI tmp = text.GetComponent<TextMeshProUGUI>();
            tmp.text = "System message";
            tmp.raycastTarget = false;
            Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Bake CampMessageCanvas");
            log += " CampMessageCanvas";
        }

        Transform tab = forgePage != null ? forgePage.transform.Find("Forge_Tab") : null;
        Transform upgrade = tab != null ? tab.Find("UpgradeButton") : null;
        if (tab != null && upgrade != null && tab.Find("BackButton") == null)
        {
            RectTransform back = CampReArtUI.CreateForgeBackButton(tab, (RectTransform)upgrade);
            Undo.RegisterCreatedObjectUndo(back.gameObject, "Bake Forge BackButton");
            log += " Forge_Tab/BackButton";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return log == "Camp:" ? "Camp: มีครบแล้ว ไม่ได้เพิ่มอะไร" : log + " (saved)";
    }
}
