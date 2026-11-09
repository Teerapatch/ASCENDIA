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
        public string weaponMesh;
        public bool melee;
        public string fallbackSelf;
        public (string skill, string clip, bool strike)[] map;
    }

    static readonly Source[] Sources =
    {
        new Source { type = WeaponType.Dagger, name = "Dagger", weaponMesh = "Dagger", melee = true, fallbackSelf = "Parry1", path = "Assets/Animation/Player/Dagger_Anim/Dagger_Anim_V2.fbx",
            map = new[] { ("Quick Attack", "Attack", true), ("Dancing Knife", "DanceSkill", true), ("Flurry Strike", "FlurrySkill", true), ("Eviscerate", "Attack", true),
                          ("Expose Weakness", "Attack", true), ("Throwing Knife", "Attack", false), ("Vanish Blade", "Parry1", false) } },
        new Source { type = WeaponType.Gun, name = "Gun", weaponMesh = "Gun", melee = false, fallbackSelf = "Parry1", path = "Assets/Animation/Player/Gun_Anim/Gun_Anim_V2.fbx",
            map = new[] { ("Bullet Hell", "BulletSkill", false), ("Quick Draw", "QuickSkill2", false), ("Overdraw", "QuickSkill2", false), ("Piercer Shot", "Attack", false),
                          ("Trick Shot", "Attack", false), ("Tactical Reload", "Parry1", false) } },
        new Source { type = WeaponType.LongSword, name = "LongSword", weaponMesh = "Sword", melee = true, fallbackSelf = "Parry1", path = "Assets/Animation/Player/LongSword_anim/Longsword_Anim_v2.fbx",
            map = new[] { ("Executioner", "ExecutSkill", true), ("Heavy Impact", "HeavySkill", true), ("Mighty Cleave", "MightySkill", true), ("Pommel Bash", "Attack", true),
                          ("Unyielding", "Attack", true), ("Berserk", "Parry1", false) } },
        new Source { type = WeaponType.Spell, name = "Spell", weaponMesh = "Wand", melee = false, fallbackSelf = "Parry1", path = "Assets/Animation/Player/Wand_Anim/Wand_Anim2.fbx",
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

                var impacts = new Dictionary<string, List<float>>();
                report.AppendLine($"== {source.name} ({clips.Count} clips)");
                foreach (var pair in clips)
                {
                    impacts[pair.Key] = FindHits(instance, pair.Value, source.weaponMesh, report);
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

    // จังหวะโดนที่หาจากข้อมูลไม่ได้ถูก (เช่นท่ายิงรัว) กำหนดเองที่นี่ ใช้สเกลเวลา 0-1 ของคลิป
    static readonly Dictionary<string, (float[] hits, float speed)> Overrides = new Dictionary<string, (float[], float)>
    {
        // ยิงรัว: จังหวะสะบัดจากแรงถีบของปืนแต่ละนัด และเร่งท่าที่ยาว 7.5 วินาที
        { "BulletSkill", (new[] { 0.14f, 0.19f, 0.24f, 0.28f, 0.33f, 0.38f, 0.42f, 0.47f, 0.52f, 0.57f, 0.61f, 0.66f, 0.71f, 0.76f, 0.80f, 0.85f }, 1.5f) },
        { "FlurrySkill", (null, 1.2f) },
        // ท่าที่มีจังหวะง้าง/ชักอาวุธเร็ว ๆ ซึ่งไม่ใช่จังหวะโดน
        { "QuickSkill2", (new[] { 0.55f }, 1f) },
        { "SpellSkill", (new[] { 0.71f }, 1f) },
        { "ExecutSkill", (new[] { 0.47f }, 1f) },
    };

    static BattleClip MakeClip(string state, Dictionary<string, List<float>> found, bool melee)
    {
        List<float> hits = found.TryGetValue(state, out List<float> list) ? new List<float>(list) : new List<float> { 0.5f };
        float speed = 1f;
        if (Overrides.TryGetValue(state, out var value))
        {
            if (value.hits != null) hits = value.hits.ToList();
            speed = value.speed;
        }
        return new BattleClip { state = state, impact = hits[0], hits = hits, speed = speed, melee = melee };
    }

    // จังหวะฟันแต่ละครั้ง = ช่วงที่ปลายอาวุธเคลื่อนเร็วสุด โดยต้องมีช่วงช้าลงคั่นจึงนับเป็นคนละครั้ง
    static List<float> FindHits(GameObject instance, AnimationClip clip, string weaponMesh, StringBuilder report)
    {
        Transform hips = Find(instance, "DEF-spine");
        Transform handR = Find(instance, "DEF-hand.R");
        SkinnedMeshRenderer weapon = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == weaponMesh);
        var baked = new Mesh();
        int tip = -1;
        int frames = Mathf.Max(8, Mathf.RoundToInt(clip.length * 60f));
        var position = new Vector3[frames + 1];

        for (int i = 0; i <= frames; i++)
        {
            clip.SampleAnimation(instance, clip.length * i / frames);
            Vector3 point = handR.position;
            if (weapon != null)
            {
                weapon.BakeMesh(baked, true);
                Vector3[] vertices = baked.vertices;
                if (tip < 0)
                {
                    float far = -1f;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        float d = (weapon.transform.TransformPoint(vertices[v]) - handR.position).sqrMagnitude;
                        if (d > far) { far = d; tip = v; }
                    }
                }
                point = weapon.transform.TransformPoint(vertices[tip]);
            }
            position[i] = point - hips.position;
        }
        Object.DestroyImmediate(baked);

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

        // ตัดช่วงต้น (ชักอาวุธ) และช่วงท้าย (ดึงอาวุธกลับ) ออก
        int start = Mathf.RoundToInt(frames * 0.08f), end = Mathf.RoundToInt(frames * 0.85f);
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
        if (peaks.Count == 0) peaks.Add(Mathf.RoundToInt(frames * 0.5f));

        report.AppendLine($"  {clip.name}: {clip.length:0.00}s");
        foreach (int i in peaks)
        {
            Vector3 v = (position[Mathf.Min(frames, i + 1)] - position[Mathf.Max(0, i - 1)]).normalized;
            report.AppendLine($"     t {(float)i / frames:0.00}  speed {smooth[i] / max:0.00}  tip ({position[i].x:0.00},{position[i].y:0.00},{position[i].z:0.00})  dir ({v.x:0.0},{v.y:0.0},{v.z:0.0})");
        }
        return peaks.Select(i => (float)i / frames).ToList();
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
