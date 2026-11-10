using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// อ่านตำแหน่งปลายดาบจากคลิป ใช้ทั้งตอนสร้างตารางและในหน้าต่าง Boss Parry Editor
public class BladeProbe : System.IDisposable
{
    const float SwingSampleOffset = 2f / 60f;

    readonly GameObject instance;
    readonly SkinnedMeshRenderer blade;
    readonly Transform hand;
    readonly Transform hips;
    readonly Mesh baked = new Mesh();
    int tipVertex = -1;

    public BladeProbe(GameObject modelInstance)
    {
        instance = modelInstance;
        Transform[] bones = instance.GetComponentsInChildren<Transform>(true);
        blade = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Blade");
        hand = bones.First(t => t.name == "DEF-hand.R");
        hips = bones.First(t => t.name == "DEF-spine1");
    }

    // ปลายดาบในพื้นที่โมเดลเทียบกับสะโพก (ตัดการขยับของทั้งตัวออก)
    public Vector3 Sample(AnimationClip clip, float seconds)
    {
        clip.SampleAnimation(instance, seconds);
        blade.BakeMesh(baked, true);
        Vector3[] vertices = baked.vertices;

        if (tipVertex < 0)
        {
            float farthest = -1f;
            for (int i = 0; i < vertices.Length; i++)
            {
                float distance = (blade.transform.TransformPoint(vertices[i]) - hand.position).sqrMagnitude;
                if (distance > farthest)
                {
                    farthest = distance;
                    tipVertex = i;
                }
            }
        }

        return instance.transform.InverseTransformPoint(blade.transform.TransformPoint(vertices[tipVertex]))
            - instance.transform.InverseTransformPoint(hips.position);
    }

    public BossStrike MakeStrike(AnimationClip clip, float normalizedTime)
    {
        float seconds = Mathf.Clamp01(normalizedTime) * clip.length;
        Vector3 before = Sample(clip, Mathf.Max(0f, seconds - SwingSampleOffset));
        Vector3 after = Sample(clip, Mathf.Min(clip.length, seconds + SwingSampleOffset));
        return new BossStrike { time = normalizedTime, swing = (after - before).normalized };
    }

    public void Dispose()
    {
        Object.DestroyImmediate(baked);
    }
}

// สร้าง Animator ของบอสและตาราง GuardianBossSet: หาจังหวะฟันและทิศของอาวุธจากคลิปโดยตรง
// ปรับจังหวะและทิศได้ใน Tools/ASCENDIA/Boss Parry Editor ท่าที่แก้แล้วจะไม่ถูกเขียนทับ
public static class AscendiaBuildBossAnimator
{
    const string ModelPath = "Assets/Animation/Guardian/BOSS/Boss-Guardians2.fbx";
    const string IdlePath = "Assets/Animation/Guardian/BOSS/Boss-IDLE.fbx";
    const string ControllerPath = "Assets/Animation/Guardian/BOSS/GuardianBoss.controller";
    const string SetPath = "Assets/Resources/GuardianBossSet.asset";
    const string IdleClip = "Boss-Idle";
    const string RageClip = "Boss_RageSkill";
    const string DanceClip = "Boss_DanceSkill";
    static readonly string[] BasicClips = { "Boss_1", "Boss_2", "Boss_3", "Boss_4", "Boss_5" };

    // จังหวะฟันที่กำหนดเอง (สัดส่วนของคลิป) ใส่เฉพาะท่าที่หาอัตโนมัติไม่ถูก
    static readonly Dictionary<string, float[]> Overrides = new Dictionary<string, float[]>
    {
    };

    [MenuItem("Tools/ASCENDIA/Build Boss Animator")]
    public static void BuildFromMenu() { Debug.Log(Build()); }

    public static string Build()
    {
        var report = new StringBuilder();
        MakeLoop(IdlePath, IdleClip);

        Dictionary<string, AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview") && c.name != "A-POSE").ToDictionary(c => c.name);
        AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(IdlePath).OfType<AnimationClip>().First(c => c.name == IdleClip);
        clips[IdleClip] = idle;

        BossAnimationSet set = AssetDatabase.LoadAssetAtPath<BossAnimationSet>(SetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<BossAnimationSet>();
            AssetDatabase.CreateAsset(set, SetPath);
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject instance = Object.Instantiate(asset);
        SceneManager.MoveGameObjectToScene(instance, scene);
        try
        {
            using (var probe = new BladeProbe(instance))
            {
                idle.SampleAnimation(instance, 0f);
                set.modelHeight = MeasureHeight(instance, report);
                set.model = asset;
                set.controller = BuildController(clips);
                set.idleState = IdleClip;

                set.basicAttacks = BasicClips.Where(clips.ContainsKey).Select(name =>
                    KeepEdited(set.basicAttacks.FirstOrDefault(m => m.state == name), MakeMove(probe, clips[name], report))).ToList();
                set.dance = KeepEdited(set.dance, MakeMove(probe, clips[DanceClip], report));

                set.rageState = RageClip;
                set.rageLength = clips[RageClip].length;
                BossMove rage = MakeMove(probe, clips[RageClip], report);
                if (rage.strikes.Count > 0) set.rageSummonTime = rage.strikes.OrderBy(s => s.time).First().time;
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
            EditorSceneManager.ClosePreviewScene(scene);
        }

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    static BossMove KeepEdited(BossMove previous, BossMove fresh)
    {
        if (previous == null || !previous.edited) return fresh;
        previous.length = fresh.length;
        return previous;
    }

    static float MeasureHeight(GameObject instance, StringBuilder report)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name == "Blade") continue;
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);
            foreach (Vector3 v in mesh.vertices)
            {
                float y = smr.transform.TransformPoint(v).y;
                min = Mathf.Min(min, y);
                max = Mathf.Max(max, y);
            }
            Object.DestroyImmediate(mesh);
        }
        report.AppendLine($"height {max - min:0.00} (y {min:0.00}..{max:0.00})");
        return max - min;
    }

    // หาจังหวะฟันจากความเร็วปลายดาบ
    public static BossMove MakeMove(BladeProbe probe, AnimationClip clip, StringBuilder report = null)
    {
        int frames = Mathf.Max(8, Mathf.RoundToInt(clip.length * 60f));
        var position = new Vector3[frames + 1];
        for (int i = 0; i <= frames; i++) position[i] = probe.Sample(clip, clip.length * i / frames);

        var speed = new float[frames + 1];
        for (int i = 1; i <= frames; i++) speed[i] = (position[i] - position[i - 1]).magnitude * 60f;
        var smooth = new float[frames + 1];
        for (int i = 0; i <= frames; i++)
        {
            int from = Mathf.Max(0, i - 2), to = Mathf.Min(frames, i + 2);
            float sum = 0f;
            for (int k = from; k <= to; k++) sum += speed[k];
            smooth[i] = sum / (to - from + 1);
        }

        int start = Mathf.RoundToInt(frames * 0.08f), end = Mathf.RoundToInt(frames * 0.9f);
        float max = 0f;
        for (int i = start; i <= end; i++) max = Mathf.Max(max, smooth[i]);

        var peaks = new List<int>();
        for (int i = start + 1; i < end; i++)
        {
            if (smooth[i] < max * 0.45f || smooth[i] < smooth[i - 1] || smooth[i] <= smooth[i + 1]) continue;
            if (peaks.Count > 0)
            {
                int last = peaks[peaks.Count - 1];
                float valley = float.MaxValue;
                for (int k = last; k <= i; k++) valley = Mathf.Min(valley, smooth[k]);
                if (valley >= 0.6f * Mathf.Min(smooth[last], smooth[i]))
                {
                    if (smooth[i] > smooth[last]) peaks[peaks.Count - 1] = i;
                    continue;
                }
            }
            peaks.Add(i);
        }

        IEnumerable<float> times = Overrides.TryGetValue(clip.name, out float[] manual)
            ? manual
            : peaks.Select(i => (float)i / frames);

        var move = new BossMove { state = clip.name, length = clip.length };
        report?.AppendLine($"== {clip.name} {clip.length:0.00}s");
        foreach (float time in times)
        {
            BossStrike strike = probe.MakeStrike(clip, time);
            move.strikes.Add(strike);
            report?.AppendLine($"   t {time:0.00} swing ({strike.swing.x:0.0},{strike.swing.y:0.0},{strike.swing.z:0.0})");
        }
        return move;
    }

    static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState idle = machine.AddState(IdleClip);
        idle.motion = clips[IdleClip];
        machine.defaultState = idle;

        foreach (var pair in clips)
        {
            if (pair.Key == IdleClip) continue;
            AnimatorState state = machine.AddState(pair.Key);
            state.motion = pair.Value;
            AnimatorStateTransition back = state.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.hasFixedDuration = true;
            back.duration = 0.2f;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void MakeLoop(string path, string clipName)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip.name == clipName && !clip.loopTime) { clip.loopTime = true; changed = true; }
        }
        if (!changed) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
}
