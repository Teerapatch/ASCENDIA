using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// อ่านตำแหน่งปลายอาวุธจากคลิป ใช้ทั้งตอนสร้างตารางศัตรูและใน Enemy Parry Editor
public class WeaponTipProbe : System.IDisposable
{
    const float SwingSampleOffset = 2f / 60f;

    readonly GameObject instance;
    readonly SkinnedMeshRenderer weapon;
    readonly Transform hand;
    readonly Transform hips;
    readonly Mesh baked = new Mesh();
    int tipVertex = -1;

    public WeaponTipProbe(GameObject modelInstance, string weaponMesh, string handBone, string hipsBone)
    {
        instance = modelInstance;
        Transform[] bones = instance.GetComponentsInChildren<Transform>(true);
        weapon = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == weaponMesh);
        hand = bones.FirstOrDefault(t => t.name == handBone) ?? instance.transform;
        hips = bones.FirstOrDefault(t => t.name == hipsBone) ?? instance.transform;
    }

    // ปลายอาวุธในพื้นที่โมเดลเทียบกับสะโพก (ตัดการขยับของทั้งตัวออก) ไม่มี Mesh อาวุธ = ใช้มือ
    public Vector3 Sample(AnimationClip clip, float seconds)
    {
        clip.SampleAnimation(instance, seconds);
        Vector3 point = hand.position;
        if (weapon != null)
        {
            weapon.BakeMesh(baked, true);
            Vector3[] vertices = baked.vertices;
            if (tipVertex < 0)
            {
                float farthest = -1f;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float distance = (weapon.transform.TransformPoint(vertices[i]) - hand.position).sqrMagnitude;
                    if (distance > farthest)
                    {
                        farthest = distance;
                        tipVertex = i;
                    }
                }
            }
            point = weapon.transform.TransformPoint(vertices[tipVertex]);
        }
        return instance.transform.InverseTransformPoint(point) - instance.transform.InverseTransformPoint(hips.position);
    }

    public EnemyStrike MakeStrike(AnimationClip clip, float normalizedTime)
    {
        float seconds = Mathf.Clamp01(normalizedTime) * clip.length;
        Vector3 before = Sample(clip, Mathf.Max(0f, seconds - SwingSampleOffset));
        Vector3 after = Sample(clip, Mathf.Min(clip.length, seconds + SwingSampleOffset));
        return new EnemyStrike { time = normalizedTime, swing = (after - before).normalized };
    }

    public void Dispose()
    {
        Object.DestroyImmediate(baked);
    }
}

// สร้าง Animator และตาราง EnemyAnimationSet ของศัตรูทุกชนิด แล้วฝังโมเดลลง Prefab
// ชนิดใหม่: เพิ่มแถวใน Sources ปรับจังหวะ/ทิศได้ใน Tools/ASCENDIA/Enemy Parry Editor ท่าที่แก้แล้วจะไม่ถูกเขียนทับ
public static class AscendiaBuildEnemyAnimators
{
    public const string SetFolder = "Assets/BattlePhase/Enemy/Anim";
    const string ProjectilePrefabPath = SetFolder + "/Projectile_Wand.prefab";
    const string ProjectileMaterialPath = SetFolder + "/Projectile_Wand.mat";
    const string ImpactPrefabPath = "Assets/Assets (jmes)/VFX/Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Light/CFXR3 Hit Light B (Air).prefab";

    class Source
    {
        public string name, fbx, idleFbx, idleClip, prefix, skillClip, hurtClip, rageClip, weaponMesh, prefab, enemyName, skillName;
        public float height;
        public EnemyAttackDelivery delivery;
        public EnemyBasicAttackMode mode;
        public bool randomDirection;
    }

    static readonly Source[] Sources =
    {
        new Source { name = "Guardian_Boss", fbx = "Assets/Animation/Guardian/BOSS/Boss-Guardians2.fbx", idleFbx = "Assets/Animation/Guardian/BOSS/Boss-IDLE.fbx", idleClip = "Boss-Idle",
            prefix = "Boss_", skillClip = "Boss_DanceSkill", hurtClip = "Boss_Hurt", rageClip = "Boss_RageSkill", weaponMesh = "Blade",
            prefab = "Assets/BattlePhase/Enemy/ORC_Boss.prefab", enemyName = "Guardian", skillName = "Dance", height = 9f,
            delivery = EnemyAttackDelivery.Melee, mode = EnemyBasicAttackMode.Chain, randomDirection = false },
        new Source { name = "Guardian_Blade", fbx = "Assets/Animation/Guardian/BladeGuardian/BladeGuardians.fbx", idleClip = "BladeGuard_Idle",
            prefix = "BladeGuard_", skillClip = "BladeGuard_Skill", hurtClip = "BladeGuard_Hurt", weaponMesh = "Blade.",
            prefab = "Assets/BattlePhase/Enemy/Orc.prefab", enemyName = "Blade Guardian", skillName = "Blade Dance", height = 6.5f,
            delivery = EnemyAttackDelivery.Melee, mode = EnemyBasicAttackMode.RandomCount, randomDirection = true },
        new Source { name = "Guardian_Axe", fbx = "Assets/Animation/Guardian/AxeGuardian/AxeGuardian.fbx", idleClip = "AxeGuard_Idle",
            prefix = "AxeGuard_", skillClip = "AxeGuard_Skill", hurtClip = "AxeGuard_Hurt", weaponMesh = "Axe",
            prefab = "Assets/BattlePhase/Enemy/Kiki.prefab", enemyName = "Axe Guardian", skillName = "Leap Slam", height = 6.5f,
            delivery = EnemyAttackDelivery.Melee, mode = EnemyBasicAttackMode.RandomCount, randomDirection = true },
        new Source { name = "Guardian_Wand", fbx = "Assets/Animation/Guardian/WandGuardian/WandGuardians.fbx", idleClip = "WandGuard_Idle",
            prefix = "WandGuard_", skillClip = "WandGuard_Skill", hurtClip = "WandGuard_Hurt", weaponMesh = "Wand",
            prefab = "Assets/BattlePhase/Enemy/SuperKiki.prefab", enemyName = "Wand Guardian", skillName = "Arcane Volley", height = 5.5f,
            delivery = EnemyAttackDelivery.Projectile, mode = EnemyBasicAttackMode.RandomCount, randomDirection = true },
    };

    // จังหวะฟันที่กำหนดเอง (สัดส่วนของคลิป) ใส่เฉพาะท่าที่หาอัตโนมัติไม่ถูก
    static readonly Dictionary<string, float[]> Overrides = new Dictionary<string, float[]>
    {
        // ท่าร่ายของ Wand ปลายไม้แทบไม่ขยับ จึงกำหนดจังหวะยิง 4 นัดเอง
        { "WandGuard_Skill", new[] { 0.15f, 0.35f, 0.55f, 0.75f } },
    };

    [MenuItem("Tools/ASCENDIA/Build Enemy Animators")]
    public static void BuildFromMenu() { Debug.Log(Build()); }

    [MenuItem("Tools/ASCENDIA/Setup Enemy Prefabs")]
    public static void SetupFromMenu() { Debug.Log(SetupPrefabs()); }

    public static string Build()
    {
        var report = new StringBuilder();
        if (!AssetDatabase.IsValidFolder(SetFolder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(SetFolder).Replace('\\', '/'), Path.GetFileName(SetFolder));

        foreach (Source source in Sources)
        {
            MakeLoop(source.idleFbx ?? source.fbx, source.idleClip);

            Dictionary<string, AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(source.fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview") && !c.name.StartsWith("A-POSE") && !c.name.Contains("Temp")).ToDictionary(c => c.name);
            if (source.idleFbx != null)
            {
                AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(source.idleFbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == source.idleClip);
                if (idle != null) clips[source.idleClip] = idle;
            }
            if (!clips.ContainsKey(source.idleClip)) { report.AppendLine($"{source.name}: no idle clip {source.idleClip}"); continue; }

            string setPath = $"{SetFolder}/{source.name}.asset";
            EnemyAnimationSet set = AssetDatabase.LoadAssetAtPath<EnemyAnimationSet>(setPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<EnemyAnimationSet>();
                set.targetHeight = source.height;
                AssetDatabase.CreateAsset(set, setPath);
            }

            // โครงสร้างมาจากตารางเสมอ ส่วนค่าตัวเลข (ความสูง ดาเมจ โอกาส) แก้ใน Inspector ได้
            set.delivery = source.delivery;
            set.basicMode = source.mode;
            set.randomDirection = source.randomDirection;
            set.skillName = source.skillName;
            set.weaponMeshName = source.weaponMesh;

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(source.fbx);
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject instance = Object.Instantiate(asset);
            SceneManager.MoveGameObjectToScene(instance, scene);
            try
            {
                using (var probe = new WeaponTipProbe(instance, set.weaponMeshName, set.handBone, set.hipsBone))
                {
                    clips[source.idleClip].SampleAnimation(instance, 0f);
                    report.AppendLine($"== {source.name}");
                    set.modelHeight = MeasureHeight(instance, set.weaponMeshName, report);
                    set.model = asset;
                    set.controller = BuildController($"{Path.GetDirectoryName(source.fbx).Replace('\\', '/')}/{source.name}.controller", clips, source.idleClip);
                    set.idleState = source.idleClip;
                    set.hurtState = clips.ContainsKey(source.hurtClip) ? source.hurtClip : null;

                    var basics = new List<EnemyMove>();
                    for (int i = 1; i <= 9; i++)
                    {
                        string name = source.prefix + i;
                        if (!clips.ContainsKey(name)) continue;
                        basics.Add(KeepEdited(set.basicAttacks.FirstOrDefault(m => m.state == name), MakeMove(probe, clips[name], report)));
                    }
                    set.basicAttacks = basics;

                    set.skill = clips.ContainsKey(source.skillClip) ? KeepEdited(set.skill, MakeMove(probe, clips[source.skillClip], report)) : null;

                    if (source.rageClip != null && clips.ContainsKey(source.rageClip))
                    {
                        set.rageState = source.rageClip;
                        set.rageLength = clips[source.rageClip].length;
                        EnemyMove rage = MakeMove(probe, clips[source.rageClip], report);
                        if (rage.strikes.Count > 0) set.rageSummonTime = rage.strikes.OrderBy(s => s.time).First().time;
                    }
                    else set.rageState = null;
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(scene);
            }

            if (set.delivery == EnemyAttackDelivery.Projectile)
            {
                if (set.projectilePrefab == null) set.projectilePrefab = EnsureProjectilePrefab();
                if (set.projectileImpactPrefab == null) set.projectileImpactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
            }

            EditorUtility.SetDirty(set);
        }

        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    // ฝังโมเดลลง Prefab ของศัตรูแต่ละชนิด (ทำซ้ำได้: ลบ Model เดิมแล้วใส่ใหม่)
    public static string SetupPrefabs()
    {
        var report = new StringBuilder();
        foreach (Source source in Sources)
        {
            EnemyAnimationSet set = AssetDatabase.LoadAssetAtPath<EnemyAnimationSet>($"{SetFolder}/{source.name}.asset");
            if (set == null || set.model == null) { report.AppendLine($"{source.name}: no set, run Build Enemy Animators first"); continue; }

            GameObject root = PrefabUtility.LoadPrefabContents(source.prefab);
            try
            {
                Transform old = root.transform.Find("Model");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                float rootScale = Mathf.Max(0.001f, root.transform.localScale.y);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(set.model, root.transform);
                model.name = "Model";
                model.transform.localScale = Vector3.one * (set.targetHeight / (Mathf.Max(0.01f, set.modelHeight) * rootScale));
                model.transform.localPosition = Vector3.down * (set.groundBelowSpawn / rootScale);
                model.transform.localRotation = Quaternion.Euler(0f, set.previewYaw, 0f);

                Animator animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = set.controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                MeshRenderer capsule = root.GetComponent<MeshRenderer>();
                if (capsule != null) capsule.enabled = false;

                CapsuleCollider body = root.GetComponent<CapsuleCollider>();
                if (body != null)
                {
                    body.height = set.targetHeight / rootScale;
                    body.radius = set.targetHeight * 0.18f / rootScale;
                    body.center = Vector3.up * ((set.targetHeight * 0.5f - set.groundBelowSpawn) / rootScale);
                }

                Transform bars = root.transform.Find("Canvas_WorldSpace");
                if (bars != null) bars.localPosition = Vector3.up * ((set.targetHeight + 0.8f - set.groundBelowSpawn) / rootScale);

                EnemyAnimationController controller = root.GetComponent<EnemyAnimationController>();
                if (controller == null) controller = root.AddComponent<EnemyAnimationController>();
                controller.set = set;

                EnemyController enemy = root.GetComponent<EnemyController>();
                if (enemy != null) enemy.enemyName = source.enemyName;

                PrefabUtility.SaveAsPrefabAsset(root, source.prefab, out bool saved);
                report.AppendLine($"{source.prefab}: {(saved ? "model baked" : "SAVE FAILED")} scale {model.transform.localScale.x:0.00}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    static EnemyMove KeepEdited(EnemyMove previous, EnemyMove fresh)
    {
        if (previous == null || !previous.edited) return fresh;
        previous.length = fresh.length;
        return previous;
    }

    static float MeasureHeight(GameObject instance, string weaponMesh, StringBuilder report)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name == weaponMesh) continue;
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

    // หาจังหวะฟันจากความเร็วปลายอาวุธ
    public static EnemyMove MakeMove(WeaponTipProbe probe, AnimationClip clip, StringBuilder report = null)
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
        if (peaks.Count == 0) peaks.Add(Mathf.RoundToInt(frames * 0.5f));

        IEnumerable<float> times = Overrides.TryGetValue(clip.name, out float[] manual) ? manual : peaks.Select(i => (float)i / frames);

        var move = new EnemyMove { state = clip.name, length = clip.length };
        report?.AppendLine($"  {clip.name} {clip.length:0.00}s");
        foreach (float time in times)
        {
            EnemyStrike strike = probe.MakeStrike(clip, time);
            move.strikes.Add(strike);
            report?.AppendLine($"     t {time:0.00} swing ({strike.swing.x:0.0},{strike.swing.y:0.0},{strike.swing.z:0.0})");
        }
        return move;
    }

    static AnimatorController BuildController(string path, Dictionary<string, AnimationClip> clips, string idleClip)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState idle = machine.AddState(idleClip);
        idle.motion = clips[idleClip];
        machine.defaultState = idle;

        foreach (var pair in clips)
        {
            if (pair.Key == idleClip) continue;
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
        if (importer == null) return;
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

    // กระสุนเริ่มต้นของ Wand: ลูกแก้วม่วงพร้อมหาง ตกแต่งต่อได้ที่ Prefab นี้
    static GameObject EnsureProjectilePrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        Color color = new Color(0.75f, 0.45f, 1f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        AssetDatabase.CreateAsset(material, ProjectileMaterialPath);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Projectile_Wand";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.localScale = Vector3.one * 0.8f;
        go.GetComponent<Renderer>().sharedMaterial = material;

        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.startWidth = 0.6f;
        trail.endWidth = 0f;
        trail.sharedMaterial = material;
        trail.startColor = new Color(color.r, color.g, color.b, 0.9f);
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        go.AddComponent<EnemyProjectile>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }
}
