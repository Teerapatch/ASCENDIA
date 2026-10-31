using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AscendiaClimbSpeedAdjustment
{
    const string Folder = "RigBackup_20261030/ClimbSpeed";
    static AscendiaClimbSpeedAdjustment() { EditorApplication.update += Apply; }
    static void Apply()
    {
        if (File.Exists(Folder + "/applied-v2.txt")) { EditorApplication.update -= Apply; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "ClimbScene") return;
        foreach (var root in scene.GetRootGameObjects())
        {
            var climb = root.GetComponent<PlayerClimbController>();
            if (!climb) continue;
            Directory.CreateDirectory(Folder);
            if (!EditorSceneManager.SaveScene(scene, Folder + "/before_" + System.DateTime.Now.ToString("HHmmss") + ".unity", true)) return;
            float before = climb.verticalSpeed;
            Undo.RecordObject(climb, "Increase climbing speed");
            float baseline = before;
            if (File.Exists(Folder + "/applied.txt"))
            {
                var previous = File.ReadAllText(Folder + "/applied.txt");
                var original = previous.Split(new[] { ": ", " -> " }, System.StringSplitOptions.None);
                if (original.Length > 1 && float.TryParse(original[1], out float saved)) baseline = saved;
            }
            climb.verticalSpeed = Mathf.Max(1.1f, baseline * 1.4f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(climb);
            EditorSceneManager.MarkSceneDirty(scene);
            File.WriteAllText(Folder + "/applied-v2.txt", "Vertical speed: " + before + " -> " + climb.verticalSpeed + ". Save scene with Ctrl+S.");
            EditorApplication.update -= Apply;
            return;
        }
    }
}
