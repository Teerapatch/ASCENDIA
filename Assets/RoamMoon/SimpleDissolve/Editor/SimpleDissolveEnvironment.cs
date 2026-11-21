using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RoamMoon.SimpleDissolve.EditorTools
{
    [InitializeOnLoad]
    public static class SimpleDissolveEnvironment
    {
        const string Root = "Assets/RoamMoon/SimpleDissolve/";
        const string DemoScene = Root + "Demo Scene/SimpleDissolveDemo.unity";
        const string LitScene = Root + "Demo Scene/SimpleDissolveDemoLit.unity";
        const string Pipeline = Root + "Settings/Simple Dissolve Pipeline.asset";
        const string PreviousKey = "RoamMoon.SimpleDissolve.PreviousPipeline";
        const string Menu = "Tools/Roam Moon/Simple Dissolve/";
        const string Label = "Simple Dissolve";

        static SimpleDissolveEnvironment()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorApplication.quitting += Restore;
            EditorApplication.delayCall += Sync;
        }

        static bool IsDemoOpen()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                string path = SceneManager.GetSceneAt(i).path;

                if (path == DemoScene || path == LitScene)
                {
                    return true;
                }
            }

            return false;
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall -= Sync;
            EditorApplication.delayCall += Sync;
        }

        static void OnSceneClosed(Scene scene)
        {
            EditorApplication.delayCall -= Sync;
            EditorApplication.delayCall += Sync;
        }

        static void Sync()
        {
            if (IsDemoOpen())
            {
                Apply(false);
            }
            else
            {
                Restore();
            }
        }

        [MenuItem(Menu + "Use The Simple Dissolve Render Pipeline")]
        static void UseDissolve()
        {
            Apply(true);
        }

        [MenuItem(Menu + "Restore The Previous Render Pipeline")]
        static void RestoreFromMenu()
        {
            if (!EditorPrefs.HasKey(PreviousKey))
            {
                Debug.Log($"{Label}: nothing to restore, the pipeline was never changed from here.");
                return;
            }

            Restore();
        }

        [MenuItem(Menu + "Restore The Previous Render Pipeline", true)]
        static bool CanRestore()
        {
            return EditorPrefs.HasKey(PreviousKey);
        }

        static RenderPipelineAsset Current()
        {
            return QualitySettings.renderPipeline != null
                ? QualitySettings.renderPipeline
                : GraphicsSettings.defaultRenderPipeline;
        }

        static void Apply(bool fromMenu)
        {
            var pack = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline);

            if (pack == null)
            {
                Debug.LogWarning($"{Label}: {Pipeline} is missing.");
                return;
            }

            RenderPipelineAsset current = Current();

            if (current == pack)
            {
                if (fromMenu)
                {
                    Debug.Log($"{Label}: already using {Pipeline}.");
                }

                return;
            }

            if (!EditorPrefs.HasKey(PreviousKey))
            {
                string currentPath = current != null ? AssetDatabase.GetAssetPath(current) : string.Empty;

                if (currentPath != Pipeline)
                {
                    EditorPrefs.SetString(PreviousKey, currentPath);
                }
            }

            Assign(pack);
            Debug.Log($"{Label}: render pipeline set to {Pipeline}.");
        }

        static void Restore()
        {
            if (!EditorPrefs.HasKey(PreviousKey))
            {
                return;
            }

            string previous = EditorPrefs.GetString(PreviousKey, string.Empty);
            var pack = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline);

            if (Current() != pack)
            {
                EditorPrefs.DeleteKey(PreviousKey);
                return;
            }

            RenderPipelineAsset back = string.IsNullOrEmpty(previous)
                ? null
                : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(previous);

            if (back == null && !string.IsNullOrEmpty(previous))
            {
                Debug.LogWarning($"{Label}: {previous} is gone, leaving the render pipeline as it is.");
                EditorPrefs.DeleteKey(PreviousKey);
                return;
            }

            Assign(back);
            EditorPrefs.DeleteKey(PreviousKey);
            Debug.Log($"{Label}: render pipeline restored to "
                + (string.IsNullOrEmpty(previous) ? "none" : previous) + ".");
        }

        static void Assign(RenderPipelineAsset asset)
        {
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;

            Object graphics = GraphicsSettings.GetGraphicsSettings();

            if (graphics != null)
            {
                EditorUtility.SetDirty(graphics);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
