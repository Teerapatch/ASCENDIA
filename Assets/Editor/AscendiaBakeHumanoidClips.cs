using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// FBX อนิเมชันของอาร์ตเป็น Rigify เต็ม กระดูกขา/แขนไม่ได้อยู่ใต้ DEF-spine จึงตั้งเป็น Humanoid ตรง ๆ ไม่ได้
// สคริปต์นี้เล่นคลิปบนโครงของ FBX แล้วคัดท่าลงโครงสะอาดของ Player_Clean และบันทึกเป็นคลิป Humanoid (.anim)
// เมื่อแก้อนิเมชันใน FBX ให้สั่งเมนู Tools/ASCENDIA/Bake Basic Animations To Humanoid อีกครั้ง
public static class AscendiaBakeHumanoidClips
{
    const string SourcePath = "Assets/Animation/Player/Basic/Player_BasicAnim_V2.fbx";
    const string TargetPath = "Assets/_kin/Prefab/Player/Player_Clean.fbx";
    const string OutputFolder = "Assets/Animation/Player/Basic/Baked";
    const string RestClip = "A-Pose.001";
    const float FrameRate = 30f;
    static readonly string[] ClipNames = { "Idle2", "Walk", "Climb", "Hurt" };

    [MenuItem("Tools/ASCENDIA/Bake Basic Animations To Humanoid")]
    public static void BakeFromMenu()
    {
        Debug.Log(Bake());
    }

    public static string Bake()
    {
        var report = new StringBuilder();
        var clips = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
        var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        var targetAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath);
        if (!clips.ContainsKey(RestClip) || sourceAsset == null || targetAsset == null) return "ไม่พบ FBX หรือคลิป " + RestClip;

        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject source = Object.Instantiate(sourceAsset);
        GameObject target = Object.Instantiate(targetAsset);
        SceneManager.MoveGameObjectToScene(source, scene);
        SceneManager.MoveGameObjectToScene(target, scene);
        try
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(OutputFolder).Replace('\\', '/'), Path.GetFileName(OutputFolder));

            Animator animator = target.GetComponent<Animator>();
            Dictionary<string, Transform> sourceBones = source.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("DEF-")).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            List<Transform> targetBones = target.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("DEF-") && sourceBones.ContainsKey(t.name))
                .OrderBy(Depth).ToList();
            Transform sourceHips = sourceBones["DEF-spine"];
            Transform targetHips = animator.GetBoneTransform(HumanBodyBones.Hips);

            // ท่า A-Pose ของสองโครงใช้เทียบความต่างของแกนกระดูกและสเกล
            clips[RestClip].SampleAnimation(source, 0f);
            var offsets = new Dictionary<Transform, Quaternion>();
            float worst = 0f;
            string worstBone = "";
            foreach (Transform bone in targetBones)
            {
                Quaternion restSource = sourceBones[bone.name].rotation;
                offsets[bone] = Quaternion.Inverse(restSource) * bone.rotation;
                float angle = Quaternion.Angle(restSource, bone.rotation);
                if (angle > worst) { worst = angle; worstBone = bone.name; }
            }
            Vector3 sourceRest = sourceHips.position;
            Vector3 targetRest = targetHips.position;
            float scale = targetRest.y / sourceRest.y;
            report.AppendLine($"bones {targetBones.Count} rest diff max {worst:0.0} deg at {worstBone}, hips scale {scale:0.###}");

            var handler = new HumanPoseHandler(animator.avatar, target.transform);
            HumanPose pose = default;
            string[] muscles = HumanTrait.MuscleName.Select(CurveName).ToArray();

            foreach (string clipName in ClipNames)
            {
                if (!clips.TryGetValue(clipName, out AnimationClip clip)) { report.AppendLine("missing " + clipName); continue; }

                int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * FrameRate) + 1);
                var body = new AnimationCurve[7];
                for (int i = 0; i < body.Length; i++) body[i] = new AnimationCurve();
                var muscleCurves = muscles.Select(_ => new AnimationCurve()).ToArray();
                Quaternion previous = Quaternion.identity;

                for (int f = 0; f < frames; f++)
                {
                    float time = clip.length * f / (frames - 1);
                    clip.SampleAnimation(source, time);
                    foreach (Transform bone in targetBones) bone.rotation = sourceBones[bone.name].rotation * offsets[bone];
                    targetHips.position = targetRest + (sourceHips.position - sourceRest) * scale;

                    handler.GetHumanPose(ref pose);
                    Quaternion q = pose.bodyRotation;
                    if (f > 0 && Quaternion.Dot(previous, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    previous = q;
                    body[0].AddKey(time, pose.bodyPosition.x);
                    body[1].AddKey(time, pose.bodyPosition.y);
                    body[2].AddKey(time, pose.bodyPosition.z);
                    body[3].AddKey(time, q.x);
                    body[4].AddKey(time, q.y);
                    body[5].AddKey(time, q.z);
                    body[6].AddKey(time, q.w);
                    for (int m = 0; m < muscleCurves.Length; m++) muscleCurves[m].AddKey(time, pose.muscles[m]);
                }

                var baked = new AnimationClip { name = clipName, frameRate = FrameRate };
                string[] bodyNames = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
                for (int i = 0; i < body.Length; i++) baked.SetCurve("", typeof(Animator), bodyNames[i], body[i]);
                for (int m = 0; m < muscleCurves.Length; m++) baked.SetCurve("", typeof(Animator), muscles[m], muscleCurves[m]);

                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(baked);
                settings.loopTime = clipName != "Hurt";
                settings.loopBlendOrientation = true;
                settings.loopBlendPositionY = true;
                settings.loopBlendPositionXZ = true;
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionY = true;
                settings.keepOriginalPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(baked, settings);

                string path = OutputFolder + "/" + clipName + ".anim";
                AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing != null) EditorUtility.CopySerialized(baked, existing);
                else AssetDatabase.CreateAsset(baked, path);
                report.AppendLine($"{clipName}: {clip.length:0.##}s {frames} frames loop={settings.loopTime}");
            }
            handler.Dispose();
            AssetDatabase.SaveAssets();
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        return report.ToString();
    }

    static int Depth(Transform t)
    {
        int depth = 0;
        while (t.parent != null) { t = t.parent; depth++; }
        return depth;
    }

    // ชื่อกล้ามเนื้อนิ้วในคลิปต่างจาก HumanTrait เช่น "Left Index 1 Stretched" -> "LeftHand.Index.1 Stretched"
    static string CurveName(string muscle)
    {
        Match m = Regex.Match(muscle, @"^(Left|Right) (Thumb|Index|Middle|Ring|Little) (.+)$");
        return m.Success ? $"{m.Groups[1].Value}Hand.{m.Groups[2].Value}.{m.Groups[3].Value}" : muscle;
    }
}
