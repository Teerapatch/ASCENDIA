using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;

[CustomEditor(typeof(PlayerVisualSize))]
public class PlayerVisualSizeInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var size = (PlayerVisualSize)target;
        serializedObject.Update();
        EditorGUILayout.HelpBox("Visual size only. 1 = original, 1.5 = 50% larger. Feet stay at the original ground anchor. Colliders and cameras keep their current size.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("visualScale"));
        bool reset = GUILayout.Button("Reset to original size (1)");
        bool changed = EditorGUI.EndChangeCheck() || reset;
        if (changed)
        {
            Undo.RecordObject(size, "Resize character visual");
            if (size.visual) Undo.RecordObject(size.visual, "Resize character visual");
            serializedObject.ApplyModifiedProperties();
            if (reset) size.visualScale = 1f;
            size.ApplySize();
            EditorUtility.SetDirty(size);
            PrefabUtility.RecordPrefabInstancePropertyModifications(size);
            if (size.visual) PrefabUtility.RecordPrefabInstancePropertyModifications(size.visual);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(size.gameObject.scene);
        }
    }
}

[InitializeOnLoad]
public static class AscendiaVisualSizeSetup
{
    const string Done = "RigBackup_20261030/visual-size-installed.txt";
    static AscendiaVisualSizeSetup() { EditorApplication.update += Pending; }
    static void Pending()
    {
        if (File.Exists(Done)) { EditorApplication.update -= Pending; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "ClimbScene") return;
        EditorApplication.update -= Pending;
        Install();
    }
    [MenuItem("Tools/ASCENDIA/Add Visual Size Control")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var player = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "Player");
        var visual = player ? player.transform.Find("PlayerVisual") : null;
        if (!visual) { Debug.LogWarning("ASCENDIA: Player/PlayerVisual was not found."); return; }
        Directory.CreateDirectory("RigBackup_20261030");
        string backup = "RigBackup_20261030/ClimbScene_live_before_scale_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) { Debug.LogError("Could not back up scene; no changes made."); return; }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add player visual size control");
        var size = player.GetComponent<PlayerVisualSize>();
        if (!size) size = Undo.AddComponent<PlayerVisualSize>(player);
        Undo.RecordObject(size, "Initialize visual size");
        Undo.RecordObject(visual, "Preserve visual ground anchor");
        if (!size.Initialize(visual)) return;
        EditorUtility.SetDirty(size);
        PrefabUtility.RecordPrefabInstancePropertyModifications(size);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        File.WriteAllText(Done, "Installed PlayerVisualSize on Player. Scale remains 1. Scene must be saved.");
        Debug.Log("ASCENDIA: Visual Scale installed on Player. 1 = original, 1.5 = 50% larger. Save scene after adjusting.");
    }
}
