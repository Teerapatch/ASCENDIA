using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// สร้าง Animator ของแต่ละอาวุธจาก FBX ของอาร์ต และตาราง BattleAnimationSet (จับคู่สกิลกับคลิป + หาจังหวะโดนจากความเร็วมือ)
// เมื่อเพิ่ม/แก้คลิปใน FBX ให้สั่งเมนู Tools/ASCENDIA/Build Battle Animators อีกครั้ง
public static class AscendiaBuildBattleAnimators
{
    const string ControllerFolder = "Assets/Animation/Player/Battle";
    const string SetFolder = "Assets/Resources";
    const string SetPath = SetFolder + "/BattleAnimationSet.asset";
    const string IdleClip = "WepIdle";

    class Source
    {
        public WeaponType type;
        public string path;
        public string name;
        public bool melee;
        public string fallbackSelf;
        public (string skill, string clip, bool strike)[] map;
    }

    static readonly Source[] Sources =
    {
        new Source { type = WeaponType.Dagger, name = "Dagger", melee = true, fallbackSelf = "Parry1", path = "Assets/Animation/Player/Dagger_Anim/Dagger_Anim_V2.fbx",
            map = new[] { ("Quick Attack", "Attack", true), ("Dancing Knife", "DanceSkill", true), ("Flurry Strike", "FlurrySkill", true), ("Eviscerate", "Attack", true),
                          ("Expose Weakness", "Attack", true), ("Throwing Knife", "Attack", false), ("Vanish Blade", "Parry1", false) } },
        new Source { type = WeaponType.Gun, name = "Gun", melee = false, fallbackSelf = "Parry1", path = "Assets/Animation/Player/Gun_Anim/Gun_Anim_V2.fbx",
            map = new[] { ("Bullet Hell", "BulletSkill", false), ("Quick Draw", "QuickSkill2", false), ("Overdraw", "QuickSkill2", false), ("Piercer Shot", "Attack", false),
                          ("Trick Shot", "Attack", false), ("Tactical Reload", "Parry1", false) } },
        new Source { type = WeaponType.LongSword, name = "LongSword", melee = true, fallbackSelf = "Parry1", path = "Assets/Animation/Player/LongSword_anim/Longsword_Anim_v2.fbx",
            map = new[] { ("Executioner", "ExecutSkill", true), ("Heavy Impact", "HeavySkill", true), ("Mighty Cleave", "MightySkill", true), ("Pommel Bash", "Attack", true),
                          ("Unyielding", "Attack", true), ("Berserk", "Parry1", false) } },
        new Source { type = WeaponType.Spell, name = "Spell", melee = false, fallbackSelf = "Parry1", path = "Assets/Animation/Player/Wand_Anim/Wand_Anim2.fbx",
            map = new[] { ("Chaos Catalyst", "ChaosSkill", false), ("Elemental Discharge", "ElementalSkill", false), ("Aura Fusion", "ElementalSkill", false),
                          ("Spell Burn", "SpellSkill", false), ("Spell Break", "SpellSkill", false), ("Elemental Transmutation", "SpellSkill", false) } },
    };

    [MenuItem("Tools/ASCENDIA/Build Battle Animators")]
    public static void BuildFromMenu() { Debug.Log(Build()); }

    public static string Build()
    {
        var report = new StringBuilder();
        EnsureFolder(ControllerFolder);
        EnsureFolder(SetFolder);

        BattleAnimationSet set = AssetDatabase.LoadAssetAtPath<BattleAnimationSet>(SetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<BattleAnimationSet>();
            AssetDatabase.CreateAsset(set, SetPath);
        }
        set.weapons.Clear();

        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (Source source in Sources)
            {
                MakeIdleLoop(source.path);
                Dictionary<string, AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(source.path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview") && !c.name.StartsWith("A-Pose") && !c.name.Contains("Temp"))
                    .ToDictionary(c => c.name);
                if (!clips.ContainsKey(IdleClip)) { report.AppendLine("no idle in " + source.path); continue; }

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(source.path);
                GameObject instance = Object.Instantiate(model);
                SceneManager.MoveGameObjectToScene(instance, scene);

                var impacts = new Dictionary<string, float>();
                report.AppendLine($"== {source.name} ({clips.Count} clips)");
                foreach (var pair in clips)
                {
                    impacts[pair.Key] = FindImpact(instance, pair.Value, report);
                }
                Object.DestroyImmediate(instance);

                string controllerPath = $"{ControllerFolder}/{source.name}.controller";
                var data = new BattleWeaponAnimation
                {
                    weapon = source.type,
                    model = model,
                    controller = BuildController(controllerPath, clips),
                    idleState = IdleClip,
                    attack = MakeClip("Attack", impacts, source.melee),
                    parries = clips.Keys.Where(k => k.StartsWith("Parry")).OrderBy(k => k).ToList()
                };
                foreach (var entry in source.map)
                {
                    string clip = clips.ContainsKey(entry.clip) ? entry.clip : "Attack";
                    if (clip != entry.clip) report.AppendLine($"  skill {entry.skill}: missing {entry.clip}, using Attack");
                    data.skills.Add(new BattleSkillClip { skillName = entry.skill, clip = MakeClip(clip, impacts, source.melee && entry.strike) });
                }
                set.weapons.Add(data);
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    // ท่าที่โดนหลายครั้ง: ดาเมจเริ่มที่จังหวะแรกแล้วทยอยตลอดคลิป และเร่งท่าที่ยาวเกินไป
    static readonly Dictionary<string, (float impact, float speed)> Overrides = new Dictionary<string, (float, float)>
    {
        { "BulletSkill", (0.14f, 1.5f) },
        { "FlurrySkill", (0.10f, 1.2f) },
    };

    static BattleClip MakeClip(string state, Dictionary<string, float> impacts, bool melee)
    {
        var clip = new BattleClip { state = state, impact = impacts.TryGetValue(state, out float impact) ? impact : 0.5f, speed = 1f, melee = melee };
        if (Overrides.TryGetValue(state, out var value))
        {
            clip.impact = value.impact;
            clip.speed = value.speed;
        }
        return clip;
    }

    // จังหวะโดน = ตอนมือข้างที่เร็วกว่าเคลื่อนที่เร็วที่สุด (เทียบกับลำตัว) ในช่วงกลางคลิป
    static float FindImpact(GameObject instance, AnimationClip clip, StringBuilder report)
    {
        Transform hips = Find(instance, "DEF-spine");
        Transform left = Find(instance, "DEF-hand.L");
        Transform right = Find(instance, "DEF-hand.R");
        int frames = Mathf.Max(8, Mathf.RoundToInt(clip.length * 60f));
        var speedL = new float[frames + 1];
        var speedR = new float[frames + 1];
        Vector3 prevL = default, prevR = default, firstHips = default;
        float minHipsZ = float.MaxValue, maxHipsZ = float.MinValue;

        for (int i = 0; i <= frames; i++)
        {
            clip.SampleAnimation(instance, clip.length * i / frames);
            Vector3 l = instance.transform.InverseTransformPoint(left.position) - instance.transform.InverseTransformPoint(hips.position);
            Vector3 r = instance.transform.InverseTransformPoint(right.position) - instance.transform.InverseTransformPoint(hips.position);
            Vector3 h = instance.transform.InverseTransformPoint(hips.position);
            if (i == 0) firstHips = h;
            minHipsZ = Mathf.Min(minHipsZ, h.z - firstHips.z);
            maxHipsZ = Mathf.Max(maxHipsZ, h.z - firstHips.z);
            if (i > 0)
            {
                speedL[i] = (l - prevL).magnitude;
                speedR[i] = (r - prevR).magnitude;
            }
            prevL = l;
            prevR = r;
        }

        float[] speed = speedR.Sum() >= speedL.Sum() ? speedR : speedL;
        float[] smooth = new float[frames + 1];
        for (int i = 1; i < frames; i++) smooth[i] = (speed[i - 1] + speed[i] + speed[i + 1]) / 3f;

        int start = Mathf.RoundToInt(frames * 0.15f), end = Mathf.RoundToInt(frames * 0.85f), best = start;
        for (int i = start; i <= end; i++) if (smooth[i] > smooth[best]) best = i;

        // รายงานจุดสูงสุดทั้งหมดไว้ตรวจสอบท่าที่โดนหลายครั้ง
        var peaks = new List<string>();
        for (int i = 2; i < frames - 1; i++)
        {
            if (smooth[i] >= smooth[best] * 0.55f && smooth[i] >= smooth[i - 1] && smooth[i] > smooth[i + 1]) peaks.Add(((float)i / frames).ToString("0.00"));
        }
        float impact = (float)best / frames;
        report.AppendLine($"  {clip.name}: {clip.length:0.00}s loop={clip.isLooping} impact {impact:0.00} peaks [{string.Join(" ", peaks)}] hipsZ {minHipsZ:0.00}..{maxHipsZ:0.00}");
        return impact;
    }

    static Transform Find(GameObject root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    }

    static AnimatorController BuildController(string path, Dictionary<string, AnimationClip> clips)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState idle = machine.AddState(IdleClip);
        idle.motion = clips[IdleClip];
        machine.defaultState = idle;

        foreach (var pair in clips)
        {
            if (pair.Key == IdleClip) continue;
            AnimatorState state = machine.AddState(pair.Key);
            state.motion = pair.Value;
            // จบท่าแล้วกลับ WepIdle เอง กันค้างถ้าโค้ดไม่ได้สั่ง
            AnimatorStateTransition back = state.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.hasFixedDuration = true;
            back.duration = 0.15f;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void MakeIdleLoop(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip.name == IdleClip && !clip.loopTime) { clip.loopTime = true; changed = true; }
        }
        if (!changed) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
    }
}
