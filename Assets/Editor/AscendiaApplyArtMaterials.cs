using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools/ASCENDIA/Apply Art Materials: เอาวัสดุใหม่ของอาร์ต (Assets/Anim_NewMat: Toon + Emissive HDR ที่ทำให้ตา/อาวุธเรืองแสงกับ Bloom)
// มาใส่ให้โมเดลที่เกมใช้จริง (Assets/Animation/...) โดยตั้ง Remap วัสดุใน Import Settings ของ FBX (ทุก Prefab/ฉากที่ใช้ FBX นั้นได้ด้วย)
//  - Guardian: อาร์ตใส่วัสดุทับไว้ที่ตัวละครในฉาก Assets/Scenes/ReArt/ENVI_Battle2.unity -> อ่านจากฉากนั้น (เปิดแบบ Additive แล้วปิดทิ้ง ไม่บันทึก)
//  - ผู้เล่น: อาร์ตทำ Prefab (Variant ของ FBX เดียวกัน) ใน Assets/Anim_NewMat/Player -> ตั้งเป็นโมเดลของอาวุธใน BattleAnimationSet
// วัสดุแต่ละช่องของ Renderer ถูกแปลงกลับเป็นชื่อวัสดุต้นทางใน FBX (คีย์ของ Remap) ถ้าชื่อเดียวกันได้วัสดุต่างกันจะใช้ตัวแรกและรายงาน
// อาร์ตเปลี่ยนวัสดุแล้วรันซ้ำได้
public static class AscendiaApplyArtMaterials
{
    const string ArtBattleScene = "Assets/Scenes/ReArt/ENVI_Battle2.unity";
    const string GuardianFolder = "Assets/Animation/Guardian";
    const string GamePlayerFolder = "Assets/Animation/Player";

    [MenuItem("Tools/ASCENDIA/Apply Art Materials")]
    static void ApplyMenu() => Debug.Log(Apply());

    public static string Apply()
    {
        var report = new StringBuilder();
        // FBX path -> (ชื่อวัสดุต้นทาง -> วัสดุใหม่)
        var remaps = new Dictionary<string, Dictionary<string, Material>>();

        // ---------------- Guardian จากฉากของอาร์ต
        Scene art = default;
        bool openedHere = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).path == ArtBattleScene) art = SceneManager.GetSceneAt(i);
        if (!art.IsValid())
        {
            art = EditorSceneManager.OpenScene(ArtBattleScene, OpenSceneMode.Additive);
            openedHere = true;
        }
        try
        {
            foreach (GameObject root in art.GetRootGameObjects())
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
                string fbx = source != null ? AssetDatabase.GetAssetPath(source) : null;
                if (fbx == null || !fbx.StartsWith(GuardianFolder) || !fbx.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                Collect(root, fbx, remaps, report);
            }
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(art, true); }

        // ---------------- ผู้เล่น: ไม่ Remap ที่ FBX (Body กับ Skin ชี้วัสดุเดียวกัน แยกช่องไม่ได้ และอาวุธบางชิ้นไม่มีชื่อวัสดุต้นทาง)
        // ใช้ Prefab ของอาร์ต (Anim_NewMat/Player, Variant ของ FBX เดียวกัน) เป็นโมเดลใน BattleAnimationSet แทน
        var set = AssetDatabase.LoadAssetAtPath<BattleAnimationSet>("Assets/Resources/BattleAnimationSet.asset");
        if (set != null)
        {
            bool changedSet = false;
            foreach (BattleWeaponAnimation weapon in set.weapons)
            {
                string fbx = weapon.model != null ? AssetDatabase.GetAssetPath(weapon.model) : null;
                if (fbx == null || !fbx.StartsWith(GamePlayerFolder)) continue;
                GameObject artModel = AscendiaBuildBattleAnimators.ArtModel(fbx);
                if (artModel == null || artModel == weapon.model) continue;
                weapon.model = artModel;
                changedSet = true;
                report.AppendLine($"BattleAnimationSet {weapon.weapon}: model -> {AssetDatabase.GetAssetPath(artModel)}");
            }
            if (changedSet) { EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set); }
        }

        // ---------------- ตั้ง Remap แล้ว Import ใหม่
        foreach (var pair in remaps)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(pair.Key);
            int changed = 0;
            var current = importer.GetExternalObjectMap();
            foreach (var remap in pair.Value)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), remap.Key);
                if (current.TryGetValue(id, out Object existing) && existing == remap.Value) continue;
                importer.RemoveRemap(id);
                importer.AddRemap(id, remap.Value);
                changed++;
            }
            if (changed > 0) importer.SaveAndReimport();
            report.AppendLine($"{pair.Key}: {changed} changed / {pair.Value.Count} -> " + string.Join(", ", pair.Value.Select(r => r.Key + "=" + r.Value.name)));
        }
        return report.ToString();
    }

    // ทุก Renderer ที่ชื่อตรงกับใน FBX: วัสดุแต่ละช่อง -> ชื่อวัสดุต้นทางของช่องนั้นใน FBX
    static void Collect(GameObject artRoot, string fbx, Dictionary<string, Dictionary<string, Material>> remaps, StringBuilder report)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
        if (model == null || importer == null) return;
        Dictionary<Object, string> sourceNames = importer.GetExternalObjectMap()
            .Where(p => p.Key.type == typeof(Material) && p.Value != null)
            .GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.First().Key.name);
        if (!remaps.TryGetValue(fbx, out var map)) remaps[fbx] = map = new Dictionary<string, Material>();

        foreach (Renderer artRenderer in artRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (artRenderer is ParticleSystemRenderer) continue;
            Renderer original = model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == artRenderer.name && r.GetType() == artRenderer.GetType());
            if (original == null) continue;
            Material[] artMaterials = artRenderer.sharedMaterials, defaults = original.sharedMaterials;
            for (int slot = 0; slot < artMaterials.Length && slot < defaults.Length; slot++)
            {
                Material wanted = artMaterials[slot], current = defaults[slot];
                if (wanted == null || current == null || wanted == current) continue;
                // ชื่อต้นทาง: คีย์ของ Remap ที่ชี้มาวัสดุนี้ หรือชื่อวัสดุที่ฝังใน FBX
                string source = sourceNames.TryGetValue(current, out string named) ? named
                    : AssetDatabase.GetAssetPath(current) == fbx ? current.name : null;
                if (source == null) { report.AppendLine($"  {fbx} {artRenderer.name}[{slot}]: unknown source for {current.name}"); continue; }
                if (map.TryGetValue(source, out Material already) && already != wanted)
                {
                    report.AppendLine($"  {fbx} {source}: conflict {already.name} vs {wanted.name} (keep first)");
                    continue;
                }
                map[source] = wanted;
            }
        }
    }
}
