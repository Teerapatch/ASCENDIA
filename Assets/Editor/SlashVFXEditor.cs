using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

// Tools/ASCENDIA/Slash VFX Editor: จัด VFX_Slash ทีละท่า ทั้งท่าของผู้เล่น (BattleAnimationSet) และศัตรู (EnemyAnimationSet)
// พรีวิวใน Scene View (VFX Graph เรนเดอร์ใน Preview ของหน้าต่างไม่ได้): โมเดล + รอยฟันเป็นวัตถุ DontSave ไม่ถูกบันทึกลงฉาก
// เลือกรอยฟันในฉากแล้วใช้เครื่องมือ Move/Rotate/Scale ปกติ (W/E/R) ค่าถูกเก็บใน Resources/SlashVFXSettings.asset (พื้นที่ของโมเดล)
// "จับตามอาวุธ" = วางรอยโค้งทับเส้นทางของปลายอาวุธจริงในคลิป (วงกลมผ่านปลายอาวุธ 3 จังหวะรอบเวลานั้น)
public class SlashVFXEditor : EditorWindow
{
    enum Side { Player, Enemy }

    class Move
    {
        public string label, state, key;
        public AnimationClip clip;
        public List<float> hits = new List<float>();
    }

    const string PlayerSetPath = "Assets/Resources/BattleAnimationSet.asset";
    const string PrefsPrefix = "Ascendia.SlashVFXEditor.";
    const float FitWindow = 0.07f;   // วินาที ก่อน/หลังจังหวะ ใช้หาเส้นโค้งของปลายอาวุธ
    const int TrailSamples = 90;

    static readonly Dictionary<WeaponType, string> PlayerWeaponMesh = new Dictionary<WeaponType, string>
    {
        { WeaponType.Dagger, "Dagger" }, { WeaponType.Gun, "Gun" }, { WeaponType.LongSword, "Sword" }, { WeaponType.Spell, "Wand" },
    };

    Side side;
    BattleAnimationSet playerSet;
    List<EnemyAnimationSet> enemySets = new List<EnemyAnimationSet>();
    int ownerIndex, moveIndex;
    readonly List<Move> moves = new List<Move>();
    SlashVFXSettings settings;

    GameObject previewRoot, model;
    Vector3 anchor;
    bool anchorSet;
    Transform hand;
    SkinnedMeshRenderer weapon;
    Mesh baked;
    int tipVertex = -1;
    float modelHeight = 2f;
    Vector3[] trail;
    readonly List<Vector3> hitTips = new List<Vector3>();

    float time;
    bool playing;
    float playSpeed = 0.5f;
    double lastTick, nextLoop, repaintUntil;
    bool loopVfx = true, showTrail = true;
    SlashVFXEntry selectedEntry;
    Vector2 scroll;

    // จุดปล่อยกระสุนของท่ายิง (ShotOriginSettings)
    ShotOriginSettings shotSettings;
    bool editShot = true;

    readonly Dictionary<SlashVFXEntry, GameObject> instances = new Dictionary<SlashVFXEntry, GameObject>();
    readonly Dictionary<SlashVFXEntry, (Vector3 position, Quaternion rotation, Vector3 scale)> placed = new Dictionary<SlashVFXEntry, (Vector3, Quaternion, Vector3)>();

    [MenuItem("Tools/ASCENDIA/Slash VFX Editor")]
    static void Open() => GetWindow<SlashVFXEditor>("Slash VFX");

    Move CurrentMove => moveIndex >= 0 && moveIndex < moves.Count ? moves[moveIndex] : null;
    List<SlashVFXEntry> CurrentEntries => settings != null && CurrentMove != null ? settings.For(CurrentMove.key) : new List<SlashVFXEntry>();
    float ClipLength => CurrentMove != null && CurrentMove.clip != null ? CurrentMove.clip.length : 1f;

    // ---------------------------------------------------------------- lifecycle
    void OnEnable()
    {
        minSize = new Vector2(420f, 520f);
        playerSet = AssetDatabase.LoadAssetAtPath<BattleAnimationSet>(PlayerSetPath);
        enemySets = AssetDatabase.FindAssets("t:EnemyAnimationSet")
            .Select(guid => AssetDatabase.LoadAssetAtPath<EnemyAnimationSet>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(s => s != null && s.model != null).OrderBy(s => s.name).ToList();
        settings = AssetDatabase.LoadAssetAtPath<SlashVFXSettings>(SlashVFXSettings.AssetPath);

        side = (Side)EditorPrefs.GetInt(PrefsPrefix + "side", 0);
        ownerIndex = EditorPrefs.GetInt(PrefsPrefix + "owner", 0);
        moveIndex = EditorPrefs.GetInt(PrefsPrefix + "move", 0);

        lastTick = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        if (!EditorApplication.isPlayingOrWillChangePlaymode) Rebuild();
    }

    void OnDisable()
    {
        EditorApplication.update -= Tick;
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
        DestroyPreview();
        if (settings != null) AssetDatabase.SaveAssetIfDirty(settings);
    }

    void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
        {
            DestroyPreview();
            if (settings != null) AssetDatabase.SaveAssetIfDirty(settings);
        }
        else if (change == PlayModeStateChange.EnteredEditMode) Rebuild();
    }

    void OnUndoRedo()
    {
        // Undo สร้างรายการใหม่ใน List: สร้างตัวพรีวิวใหม่ทั้งหมด
        int keep = selectedEntry != null ? CurrentEntries.IndexOf(selectedEntry) : -1;
        DestroyInstances();
        List<SlashVFXEntry> list = CurrentEntries;
        selectedEntry = keep >= 0 && keep < list.Count ? list[keep] : null;
        SyncInstances();
        Repaint();
    }

    void SavePrefs()
    {
        EditorPrefs.SetInt(PrefsPrefix + "side", (int)side);
        EditorPrefs.SetInt(PrefsPrefix + "owner", ownerIndex);
        EditorPrefs.SetInt(PrefsPrefix + "move", moveIndex);
    }

    // ---------------------------------------------------------------- data
    string[] OwnerNames()
    {
        if (side == Side.Player) return playerSet != null ? playerSet.weapons.Select(w => w.weapon.ToString()).ToArray() : new string[0];
        return enemySets.Select(s => s.name).ToArray();
    }

    void Rebuild()
    {
        DestroyPreview();
        moves.Clear();
        string[] owners = OwnerNames();
        if (owners.Length == 0) return;
        ownerIndex = Mathf.Clamp(ownerIndex, 0, owners.Length - 1);

        GameObject source;
        string meshName, handName;
        if (side == Side.Player)
        {
            BattleWeaponAnimation data = playerSet.weapons[ownerIndex];
            Dictionary<string, AnimationClip> clips = ClipsOf(data.model, data.controller);
            AddPlayerMove(data, data.attack, "Attack", clips);
            foreach (BattleSkillClip skill in data.skills) AddPlayerMove(data, skill.clip, skill.skillName, clips);
            source = data.model;
            PlayerWeaponMesh.TryGetValue(data.weapon, out meshName);
            handName = "DEF-hand.R";
        }
        else
        {
            EnemyAnimationSet set = enemySets[ownerIndex];
            Dictionary<string, AnimationClip> clips = ClipsOf(set.model, set.controller);
            foreach (EnemyMove move in set.basicAttacks) AddEnemyMove(set, move, move.state, clips);
            if (set.skill != null) AddEnemyMove(set, set.skill, set.skillName, clips);
            source = set.model;
            meshName = set.weaponMeshName;
            handName = set.handBone;
        }

        CreatePreview(source, meshName, handName);
        moveIndex = Mathf.Clamp(moveIndex, 0, Mathf.Max(0, moves.Count - 1));
        SelectMove(moveIndex);
    }

    static Dictionary<string, AnimationClip> ClipsOf(GameObject fbx, RuntimeAnimatorController controller)
    {
        var clips = new Dictionary<string, AnimationClip>();
        if (fbx != null)
            foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(fbx)).OfType<AnimationClip>())
                if (!clip.name.StartsWith("__preview")) clips[clip.name] = clip;
        if (controller != null)
            foreach (AnimationClip clip in controller.animationClips) clips[clip.name] = clip;
        return clips;
    }

    void AddPlayerMove(BattleWeaponAnimation data, BattleClip clip, string label, Dictionary<string, AnimationClip> clips)
    {
        if (clip == null || string.IsNullOrEmpty(clip.state)) return;
        Move existing = moves.Find(m => m.state == clip.state);
        if (existing != null) { existing.label = existing.label.Replace("  (", " / " + label + "  ("); return; }
        clips.TryGetValue(clip.state, out AnimationClip animation);
        moves.Add(new Move
        {
            label = label + "  (" + clip.state + (clip.melee ? ", เข้าไปตี" : "") + ")",
            state = clip.state,
            key = SlashVFXSettings.PlayerKey(data.weapon, clip.state),
            clip = animation,
            hits = clip.hits != null && clip.hits.Count > 0 ? new List<float>(clip.hits) : new List<float> { clip.impact },
        });
    }

    void AddEnemyMove(EnemyAnimationSet set, EnemyMove move, string label, Dictionary<string, AnimationClip> clips)
    {
        if (move == null || string.IsNullOrEmpty(move.state) || moves.Exists(m => m.state == move.state)) return;
        clips.TryGetValue(move.state, out AnimationClip animation);
        moves.Add(new Move
        {
            label = label == move.state ? move.state : label + "  (" + move.state + ")",
            state = move.state,
            key = SlashVFXSettings.EnemyKey(set, move.state),
            clip = animation,
            hits = move.strikes.Select(s => s.time).ToList(),
        });
    }

    void SelectMove(int index)
    {
        moveIndex = index;
        SavePrefs();
        playing = false;
        DestroyInstances();
        selectedEntry = null;
        Move move = CurrentMove;
        time = move != null && move.hits.Count > 0 ? move.hits[0] : 0f;
        BuildTrail();
        Sample();
        SyncInstances();
        Repaint();
    }

    SlashVFXSettings EnsureSettings()
    {
        if (settings != null) return settings;
        settings = AssetDatabase.LoadAssetAtPath<SlashVFXSettings>(SlashVFXSettings.AssetPath);
        if (settings == null)
        {
            settings = CreateInstance<SlashVFXSettings>();
            AssetDatabase.CreateAsset(settings, SlashVFXSettings.AssetPath);
            AssetDatabase.SaveAssets();
        }
        return settings;
    }

    // ---------------------------------------------------------------- preview objects
    void CreatePreview(GameObject source, string meshName, string handName)
    {
        if (source == null) return;
        if (!anchorSet)
        {
            anchor = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            anchorSet = true;
        }
        previewRoot = EditorUtility.CreateGameObjectWithHideFlags("SlashVFX_Preview (ไม่ถูกบันทึก)", HideFlags.DontSave);
        previewRoot.transform.position = anchor;
        model = Instantiate(source, previewRoot.transform);
        model.name = source.name;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        SetDontSave(model);
        Animator animator = model.GetComponent<Animator>();
        if (animator != null) animator.enabled = false;

        Transform[] bones = model.GetComponentsInChildren<Transform>(true);
        hand = bones.FirstOrDefault(t => t.name == handName) ?? bones.FirstOrDefault(t => t.name.Contains("hand.R")) ?? model.transform;
        SkinnedMeshRenderer[] skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        weapon = string.IsNullOrEmpty(meshName) ? null : skinned.FirstOrDefault(r => r.name == meshName) ?? skinned.FirstOrDefault(r => r.name.Contains(meshName));
        baked = new Mesh { hideFlags = HideFlags.DontSave };
        tipVertex = -1;

        Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
        modelHeight = bounds.size.y > 0.1f ? bounds.size.y : 2f;
    }

    static void SetDontSave(GameObject go)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
    }

    void DestroyInstances()
    {
        foreach (GameObject go in instances.Values) if (go != null) DestroyImmediate(go);
        instances.Clear();
        placed.Clear();
    }

    void DestroyPreview()
    {
        DestroyInstances();
        if (previewRoot != null) DestroyImmediate(previewRoot);
        if (baked != null) DestroyImmediate(baked);
        previewRoot = null;
        model = null;
        trail = null;
    }

    // ---------------------------------------------------------------- animation sampling
    void SampleAt(float seconds)
    {
        Move move = CurrentMove;
        if (model == null || move == null || move.clip == null) return;
        move.clip.SampleAnimation(model, Mathf.Clamp(seconds, 0f, move.clip.length));
        // รากของโมเดลถูกคุมด้วยโค้ดในเกม (พุ่งเข้าไปตี / หันหาเป้า) ไม่ใช่คลิป
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
    }

    void Sample() => SampleAt(time * ClipLength);

    // ปลายอาวุธ (ไม่มี Mesh อาวุธ = มือ) ในพื้นที่ของโมเดล
    Vector3 TipAt(float seconds)
    {
        SampleAt(seconds);
        Vector3 point = hand != null ? hand.position : model.transform.position;
        if (weapon != null)
        {
            // BakeMesh (ไม่ใช้สเกล) ได้จุดยอดขนาดจริงแล้ว: ใช้แค่ตำแหน่ง+การหมุนของ Renderer (สเกลนำเข้า FBX ทำให้ TransformPoint ไปไกลหลายสิบเท่า)
            weapon.BakeMesh(baked);
            Vector3[] vertices = baked.vertices;
            Matrix4x4 toWorld = Matrix4x4.TRS(weapon.transform.position, weapon.transform.rotation, Vector3.one);
            if (vertices.Length > 0)
            {
                if (tipVertex < 0 || tipVertex >= vertices.Length)
                {
                    float far = -1f;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        float d = (toWorld.MultiplyPoint3x4(vertices[i]) - point).sqrMagnitude;
                        if (d > far) { far = d; tipVertex = i; }
                    }
                }
                point = toWorld.MultiplyPoint3x4(vertices[tipVertex]);
            }
        }
        return model.transform.InverseTransformPoint(point);
    }

    void BuildTrail()
    {
        trail = null;
        hitTips.Clear();
        Move move = CurrentMove;
        if (model == null || move == null || move.clip == null) return;
        trail = new Vector3[TrailSamples];
        for (int i = 0; i < TrailSamples; i++) trail[i] = TipAt(i / (float)(TrailSamples - 1) * move.clip.length);
        foreach (float hit in move.hits) hitTips.Add(TipAt(hit * move.clip.length));
        Sample();
    }

    // วางรอยโค้งทับเส้นทางปลายอาวุธรอบจังหวะของรายการนี้
    void FitToWeapon(SlashVFXEntry entry)
    {
        Move move = CurrentMove;
        if (model == null || move == null || move.clip == null) return;
        float t = entry.time * move.clip.length;
        Vector3 p0 = TipAt(Mathf.Max(0f, t - FitWindow));
        Vector3 p1 = TipAt(t);
        Vector3 p2 = TipAt(Mathf.Min(move.clip.length, t + FitWindow));
        Sample();

        Vector3 a = p0 - p1, b = p2 - p1, n = Vector3.Cross(a, b);
        Vector3 travel = p2 - p0;
        Vector3 outward;
        float radius;
        if (n.sqrMagnitude > 1e-8f)
        {
            // วงกลมผ่าน 3 จุด
            Vector3 center = p1 + (a.sqrMagnitude * Vector3.Cross(b, n) + b.sqrMagnitude * Vector3.Cross(n, a)) / (2f * n.sqrMagnitude);
            outward = p1 - center;
        }
        else
        {
            // ปลายอาวุธวิ่งเป็นเส้นตรง: ถือว่าเหวี่ยงรอบกลางอก
            outward = p1 - new Vector3(0f, modelHeight * 0.55f, 0f);
            if (travel.sqrMagnitude > 1e-8f) outward = Vector3.ProjectOnPlane(outward, travel.normalized);
        }
        radius = outward.magnitude;
        outward = radius > 1e-4f ? outward / radius : Vector3.up;
        radius = Mathf.Clamp(radius, modelHeight * 0.15f, modelHeight * 1.2f);
        Vector3 sweep = Vector3.ProjectOnPlane(travel, outward);
        if (sweep.sqrMagnitude < 1e-8f) sweep = Vector3.Cross(outward, Vector3.up);
        if (sweep.sqrMagnitude < 1e-8f) sweep = Vector3.right;

        if (!CombatVFX.SlashPoseFromArc(p1, outward, sweep.normalized, radius, out CombatVFX.SlashPose pose)) return;
        Undo.RecordObject(settings, "Fit Slash");
        entry.position = pose.position;
        entry.euler = pose.rotation.eulerAngles;
        entry.scale = pose.scale;
        EditorUtility.SetDirty(settings);
    }

    // ---------------------------------------------------------------- slash instances
    (Vector3 position, Quaternion rotation, Vector3 scale) Expected(SlashVFXEntry entry)
    {
        GameObject prefab = CombatVFX.SlashPrefab;
        Transform m = model.transform;
        return (m.TransformPoint(entry.position), m.rotation * Quaternion.Euler(entry.euler),
                (prefab != null ? prefab.transform.localScale : Vector3.one) * (entry.scale * m.lossyScale.x));
    }

    void SyncInstances()
    {
        if (model == null || previewRoot == null) return;
        List<SlashVFXEntry> list = CurrentEntries;
        foreach (SlashVFXEntry gone in instances.Keys.Where(e => !list.Contains(e)).ToList())
        {
            if (instances[gone] != null) DestroyImmediate(instances[gone]);
            instances.Remove(gone);
            placed.Remove(gone);
        }
        GameObject prefab = CombatVFX.SlashPrefab;
        if (prefab == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            SlashVFXEntry entry = list[i];
            if (!instances.TryGetValue(entry, out GameObject go) || go == null)
            {
                go = Instantiate(prefab, previewRoot.transform);
                SetDontSave(go);
                instances[entry] = go;
                Place(entry);
            }
            go.name = "Slash " + (i + 1) + (entry.hit >= 0 ? " (hit " + (entry.hit + 1) + ")" : "");
        }
    }

    void Place(SlashVFXEntry entry)
    {
        if (!instances.TryGetValue(entry, out GameObject go) || go == null) return;
        var pose = Expected(entry);
        go.transform.SetPositionAndRotation(pose.position, pose.rotation);
        go.transform.localScale = pose.scale;
        placed[entry] = (go.transform.position, go.transform.rotation, go.transform.localScale);
    }

    void Replay(SlashVFXEntry entry)
    {
        if (entry == null || !entry.enabled || !instances.TryGetValue(entry, out GameObject go) || go == null) return;
        foreach (VisualEffect effect in go.GetComponentsInChildren<VisualEffect>()) effect.Reinit();
        repaintUntil = EditorApplication.timeSinceStartup + 1.5;
    }

    static bool Same((Vector3 position, Quaternion rotation, Vector3 scale) a, Transform t)
    {
        return (a.position - t.position).sqrMagnitude < 1e-8f && Quaternion.Angle(a.rotation, t.rotation) < 0.01f && (a.scale - t.localScale).sqrMagnitude < 1e-8f;
    }

    // ---------------------------------------------------------------- tick
    void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - lastTick);
        lastTick = now;
        if (EditorApplication.isPlayingOrWillChangePlaymode || model == null) return;

        List<SlashVFXEntry> list = CurrentEntries;
        if (playing && CurrentMove != null && CurrentMove.clip != null)
        {
            float previous = time;
            time += dt * playSpeed / Mathf.Max(0.01f, ClipLength);
            foreach (SlashVFXEntry entry in list)
                if (previous < entry.time && time >= entry.time) Replay(entry);
            if (time >= 1f) time = 0f;
            Sample();
            Repaint();
        }
        else if (loopVfx && selectedEntry != null && now >= nextLoop)
        {
            nextLoop = now + 0.9;
            Replay(selectedEntry);
        }

        // ผู้ใช้ขยับรอยฟันในฉาก -> เขียนกลับเป็นค่าในพื้นที่ของโมเดล / ค่าเปลี่ยนจากหน้าต่างหรือ Undo -> วางใหม่
        foreach (SlashVFXEntry entry in list)
        {
            if (!instances.TryGetValue(entry, out GameObject go) || go == null) continue;
            if (placed.TryGetValue(entry, out var last) && !Same(last, go.transform))
            {
                Undo.RecordObject(settings, "Move Slash");
                Transform m = model.transform;
                entry.position = m.InverseTransformPoint(go.transform.position);
                entry.euler = (Quaternion.Inverse(m.rotation) * go.transform.rotation).eulerAngles;
                GameObject prefab = CombatVFX.SlashPrefab;
                float baseScale = prefab != null ? Mathf.Max(1e-4f, prefab.transform.localScale.x) : 1f;
                entry.scale = go.transform.localScale.x / (baseScale * Mathf.Max(1e-4f, m.lossyScale.x));
                EditorUtility.SetDirty(settings);
                placed[entry] = (go.transform.position, go.transform.rotation, go.transform.localScale);
                Repaint();
            }
            else if (!Same(Expected(entry), go.transform)) Place(entry);
        }

        if (playing || now < repaintUntil) SceneView.RepaintAll();
    }

    // ---------------------------------------------------------------- scene view
    void OnSceneGUI(SceneView view)
    {
        if (model == null) return;
        Transform m = model.transform;

        if (showTrail && trail != null)
        {
            Handles.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Handles.DrawAAPolyLine(3f, trail.Select(p => m.TransformPoint(p)).ToArray());
            Handles.color = new Color(1f, 0.25f, 0.2f, 1f);
            float size = modelHeight * 0.025f;
            foreach (Vector3 tip in hitTips) Handles.SphereHandleCap(0, m.TransformPoint(tip), Quaternion.identity, size, EventType.Repaint);
            if (CurrentMove != null && CurrentMove.clip != null)
            {
                // ปลายอาวุธ ณ เวลาที่เลื่อนอยู่ (ไม่ Sample ซ้ำใน OnSceneGUI: ประมาณจากเส้นทาง)
                int index = Mathf.Clamp(Mathf.RoundToInt(time * (TrailSamples - 1)), 0, TrailSamples - 1);
                Handles.color = Color.white;
                Handles.SphereHandleCap(0, m.TransformPoint(trail[index]), Quaternion.identity, size * 0.8f, EventType.Repaint);
            }
        }

        DrawShotScene(m);

        List<SlashVFXEntry> list = CurrentEntries;
        for (int i = 0; i < list.Count; i++)
        {
            SlashVFXEntry entry = list[i];
            if (!instances.TryGetValue(entry, out GameObject go) || go == null) continue;
            VisualEffect fx = go.GetComponentInChildren<VisualEffect>();
            if (fx == null) continue;
            bool isSelected = entry == selectedEntry;
            Color color = !entry.enabled ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : isSelected ? new Color(0.3f, 0.95f, 1f, 1f) : new Color(0.3f, 0.7f, 1f, 0.55f);

            // รอยโค้งช่วงที่สว่าง (ครึ่งล่างของวงในพื้นที่ของ Mesh) กับลูกศรทิศที่ฟัน
            Vector3[] arc = new Vector3[19];
            for (int k = 0; k < arc.Length; k++)
            {
                float angle = Mathf.Lerp(0f, -180f, k / (float)(arc.Length - 1)) * Mathf.Deg2Rad;
                Vector3 local = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * -CombatVFX.SlashMeshEdge.y;
                arc[k] = fx.transform.TransformPoint(CombatVFX.SlashParticleAngle * local);
            }
            Handles.color = color;
            Handles.DrawAAPolyLine(isSelected ? 6f : 3f, arc);
            Vector3 middle = arc[arc.Length / 2];
            Vector3 sweep = fx.transform.TransformDirection(CombatVFX.SlashParticleAngle * Vector3.right).normalized;
            float arrow = HandleUtility.GetHandleSize(middle) * 0.6f;
            Handles.DrawAAPolyLine(isSelected ? 5f : 2f, middle, middle + sweep * arrow);
            Handles.ConeHandleCap(0, middle + sweep * arrow, Quaternion.LookRotation(sweep), arrow * 0.25f, EventType.Repaint);
            Handles.Label(middle, " " + (i + 1) + (entry.enabled ? "" : " (ปิด)"));

            float button = HandleUtility.GetHandleSize(middle) * 0.08f;
            if (Handles.Button(middle, Quaternion.identity, button, button * 1.5f, Handles.DotHandleCap)) Select(entry, true);
        }
    }

    void Select(SlashVFXEntry entry, bool selectInScene)
    {
        selectedEntry = entry;
        if (entry != null)
        {
            time = entry.time;
            playing = false;
            Sample();
            Replay(entry);
            if (selectInScene && instances.TryGetValue(entry, out GameObject go) && go != null) Selection.activeGameObject = go;
        }
        Repaint();
        SceneView.RepaintAll();
    }

    void ViewFrom(Quaternion rotation)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || model == null) return;
        view.LookAt(model.transform.TransformPoint(new Vector3(0f, modelHeight * 0.5f, modelHeight * 0.3f)), model.transform.rotation * rotation, modelHeight * 1.4f);
    }

    // ---------------------------------------------------------------- window
    void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("ออกจาก Play Mode ก่อน แล้วพรีวิวจะกลับมาเอง", MessageType.Info);
            return;
        }
        if (CombatVFX.SlashPrefab == null)
        {
            EditorGUILayout.HelpBox("ไม่พบ Resources/BattleVFX/VFX_Slash.prefab", MessageType.Error);
            return;
        }

        EditorGUI.BeginChangeCheck();
        Side newSide = (Side)GUILayout.Toolbar((int)side, new[] { "ผู้เล่น", "ศัตรู" });
        if (EditorGUI.EndChangeCheck() && newSide != side)
        {
            side = newSide;
            ownerIndex = 0;
            moveIndex = 0;
            SavePrefs();
            Rebuild();
            GUIUtility.ExitGUI();
        }

        string[] owners = OwnerNames();
        if (owners.Length == 0)
        {
            EditorGUILayout.HelpBox(side == Side.Player ? "ไม่พบ " + PlayerSetPath : "ไม่พบ EnemyAnimationSet", MessageType.Warning);
            return;
        }
        int newOwner = EditorGUILayout.Popup(side == Side.Player ? "อาวุธ" : "ศัตรู", ownerIndex, owners);
        if (newOwner != ownerIndex)
        {
            ownerIndex = newOwner;
            moveIndex = 0;
            SavePrefs();
            Rebuild();
            GUIUtility.ExitGUI();
        }
        if (moves.Count == 0)
        {
            EditorGUILayout.HelpBox("ไม่มีท่าโจมตี", MessageType.Info);
            return;
        }
        int newMove = EditorGUILayout.Popup("ท่า", moveIndex, moves.Select(mv => mv.label).ToArray());
        if (newMove != moveIndex) { SelectMove(newMove); GUIUtility.ExitGUI(); }

        Move move = CurrentMove;
        if (move.clip == null) EditorGUILayout.HelpBox("ไม่พบคลิป " + move.state, MessageType.Warning);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawTimeline(move);
        DrawEntries(move);
        DrawSelected(move);
        DrawShotOrigin(move);
        DrawPreviewControls();
        EditorGUILayout.EndScrollView();
    }

    void DrawTimeline(Move move)
    {
        EditorGUILayout.Space(4);
        Rect rect = GUILayoutUtility.GetRect(10f, 38f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.14f, 0.17f));
        foreach (float hit in move.hits)
            EditorGUI.DrawRect(new Rect(rect.x + hit * rect.width - 1f, rect.y + 18f, 2f, rect.height - 18f), new Color(1f, 0.3f, 0.25f));
        foreach (SlashVFXEntry entry in CurrentEntries)
        {
            Color c = !entry.enabled ? Color.gray : entry == selectedEntry ? new Color(0.3f, 0.95f, 1f) : new Color(0.3f, 0.6f, 1f);
            EditorGUI.DrawRect(new Rect(rect.x + entry.time * rect.width - 2f, rect.y, 4f, 18f), c);
        }
        EditorGUI.DrawRect(new Rect(rect.x + time * rect.width - 1f, rect.y, 2f, rect.height), Color.white);

        Event e = Event.current;
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && rect.Contains(e.mousePosition))
        {
            time = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width);
            playing = false;
            Sample();
            SceneView.RepaintAll();
            e.Use();
        }

        EditorGUILayout.BeginHorizontal();
        float newTime = EditorGUILayout.Slider(time, 0f, 1f);
        if (!Mathf.Approximately(newTime, time)) { time = newTime; playing = false; Sample(); SceneView.RepaintAll(); }
        GUILayout.Label((time * ClipLength).ToString("0.00") + " / " + ClipLength.ToString("0.00") + " s", GUILayout.Width(95));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(playing ? "■ หยุด" : "▶ เล่นท่า", GUILayout.Width(90))) { playing = !playing; if (playing && time >= 0.99f) time = 0f; }
        GUILayout.Label("ความเร็ว", GUILayout.Width(50));
        playSpeed = EditorGUILayout.Slider(playSpeed, 0.05f, 1f);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < move.hits.Count; i++)
            if (GUILayout.Button("ฮิต " + (i + 1) + " (" + move.hits[i].ToString("0.00") + ")", EditorStyles.miniButton)) { time = move.hits[i]; playing = false; Sample(); SceneView.RepaintAll(); }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("แดง = จังหวะโดน, ฟ้า = รอยฟัน, เส้นเหลืองในฉาก = ทางเดินปลายอาวุธ", EditorStyles.miniLabel);
    }

    void DrawEntries(Move move)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("รอยฟันของท่านี้", EditorStyles.boldLabel);
        List<SlashVFXEntry> list = CurrentEntries;
        if (list.Count == 0)
            EditorGUILayout.HelpBox(move.label.Contains("เข้าไปตี") || side == Side.Enemy
                ? "ยังไม่ได้จัด: ในเกมใช้รอยฟันอัตโนมัติตามทิศอาวุธ เริ่มจัดด้วยปุ่มด้านล่าง"
                : "ยังไม่ได้จัด: ท่านี้ไม่ใช่ท่าเข้าไปตี ในเกมจึงไม่มีรอยฟัน (เพิ่มเองได้)", MessageType.None);

        for (int i = 0; i < list.Count; i++)
        {
            SlashVFXEntry entry = list[i];
            EditorGUILayout.BeginHorizontal();
            bool isSelected = entry == selectedEntry;
            if (GUILayout.Toggle(isSelected, (i + 1).ToString(), "Button", GUILayout.Width(28)) && !isSelected) Select(entry, true);
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle(entry.enabled, GUILayout.Width(16));
            float t = EditorGUILayout.Slider(entry.time, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "Edit Slash");
                entry.enabled = enabled;
                entry.time = t;
                EditorUtility.SetDirty(settings);
            }
            GUILayout.Label(entry.hit >= 0 ? "ฮิต " + (entry.hit + 1) : "เสริม", GUILayout.Width(40));
            if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(22)))
            {
                Undo.RecordObject(settings, "Delete Slash");
                settings.entries.Remove(entry);
                EditorUtility.SetDirty(settings);
                if (selectedEntry == entry) selectedEntry = null;
                SyncInstances();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ เพิ่มที่เวลานี้ (จับตามอาวุธ)")) AddAt(move, time);
        if (GUILayout.Button("จับทุกฮิตตามอาวุธ")) FitAllHits(move);
        EditorGUILayout.EndHorizontal();
        if (list.Count > 0 && GUILayout.Button("ล้างท่านี้ (กลับไปใช้รอยอัตโนมัติ)"))
        {
            if (EditorUtility.DisplayDialog("ล้างรอยฟัน", "ลบรอยฟันที่จัดไว้ทั้งหมดของ " + move.state + " ?", "ลบ", "ยกเลิก"))
            {
                Undo.RecordObject(settings, "Clear Slashes");
                settings.entries.RemoveAll(en => en.key == move.key);
                EditorUtility.SetDirty(settings);
                selectedEntry = null;
                SyncInstances();
                GUIUtility.ExitGUI();
            }
        }
    }

    int NearestFreeHit(Move move, float at)
    {
        int best = -1;
        float bestDistance = 0.08f;
        for (int i = 0; i < move.hits.Count; i++)
        {
            float d = Mathf.Abs(move.hits[i] - at);
            if (d < bestDistance && (settings == null || !settings.Covers(move.key, i))) { best = i; bestDistance = d; }
        }
        return best;
    }

    void AddAt(Move move, float at)
    {
        EnsureSettings();
        Undo.RecordObject(settings, "Add Slash");
        SlashVFXEntry entry = new SlashVFXEntry { key = move.key, hit = NearestFreeHit(move, at), time = at };
        settings.entries.Add(entry);
        FitToWeapon(entry);
        EditorUtility.SetDirty(settings);
        SyncInstances();
        Select(entry, true);
    }

    void FitAllHits(Move move)
    {
        EnsureSettings();
        bool hasAny = settings.entries.Exists(en => en.key == move.key && en.hit >= 0);
        if (hasAny && !EditorUtility.DisplayDialog("จับทุกฮิต", "รอยฟันของฮิตที่จัดไว้แล้วจะถูกวางใหม่ตามอาวุธ ทำต่อ?", "ทำต่อ", "ยกเลิก")) return;
        Undo.RecordObject(settings, "Fit All Slashes");
        SlashVFXEntry first = null;
        for (int i = 0; i < move.hits.Count; i++)
        {
            SlashVFXEntry entry = settings.entries.Find(en => en.key == move.key && en.hit == i);
            if (entry == null)
            {
                entry = new SlashVFXEntry { key = move.key, hit = i };
                settings.entries.Add(entry);
            }
            entry.time = move.hits[i];
            FitToWeapon(entry);
            if (first == null) first = entry;
        }
        EditorUtility.SetDirty(settings);
        SyncInstances();
        foreach (SlashVFXEntry entry in CurrentEntries) Place(entry);
        Select(first, true);
    }

    void DrawSelected(Move move)
    {
        SlashVFXEntry entry = selectedEntry;
        if (entry == null || !CurrentEntries.Contains(entry)) return;
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("รอยฟันที่เลือก", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("เลือกรอยฟันในฉาก (จุดกลางรอยโค้ง หรือใน Hierarchy) แล้วใช้ Move / Rotate / Scale (W / E / R) ได้เลย", MessageType.None);

        EditorGUI.BeginChangeCheck();
        string[] hitNames = new[] { "เสริม (ไม่แทนฮิตไหน)" }.Concat(move.hits.Select((h, i) => "ฮิต " + (i + 1) + " (" + h.ToString("0.00") + ")")).ToArray();
        int hit = EditorGUILayout.Popup("แทนฮิต", entry.hit + 1, hitNames) - 1;
        Vector3 position = EditorGUILayout.Vector3Field("ตำแหน่ง (โมเดล)", entry.position);
        Vector3 euler = EditorGUILayout.Vector3Field("มุม (โมเดล)", entry.euler);
        float scale = EditorGUILayout.FloatField("ขนาด", entry.scale);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Edit Slash");
            entry.hit = hit;
            entry.position = position;
            entry.euler = euler;
            entry.scale = Mathf.Max(0.01f, scale);
            EditorUtility.SetDirty(settings);
            SyncInstances();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("เลือกในฉาก") && instances.TryGetValue(entry, out GameObject go)) Selection.activeGameObject = go;
        if (GUILayout.Button("เล่น VFX")) Replay(entry);
        if (GUILayout.Button("ไปที่เวลานี้")) { time = entry.time; Sample(); SceneView.RepaintAll(); }
        if (GUILayout.Button("ใช้เวลาปัจจุบัน")) { Undo.RecordObject(settings, "Edit Slash"); entry.time = time; EditorUtility.SetDirty(settings); }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("จับตามอาวุธใหม่")) { FitToWeapon(entry); Place(entry); Replay(entry); }
        if (GUILayout.Button("กลับด้านทิศฟัน"))
        {
            // หมุน 180° รอบแกนออกจากวง: รอยเดิม ทิศฟันกลับ
            Undo.RecordObject(settings, "Flip Slash");
            if (instances.TryGetValue(entry, out GameObject flip) && flip != null)
            {
                VisualEffect fx = flip.GetComponentInChildren<VisualEffect>();
                Vector3 edgeWorld = fx.transform.TransformPoint(CombatVFX.SlashParticleAngle * CombatVFX.SlashMeshEdge);
                Vector3 outWorld = fx.transform.TransformDirection(CombatVFX.SlashParticleAngle * Vector3.down);
                flip.transform.RotateAround(edgeWorld, outWorld, 180f);
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    void DrawPreviewControls()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("พรีวิว", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        loopVfx = GUILayout.Toggle(loopVfx, "เล่นรอยที่เลือกวน", EditorStyles.miniButton);
        bool newTrail = GUILayout.Toggle(showTrail, "ทางปลายอาวุธ", EditorStyles.miniButton);
        if (newTrail != showTrail) { showTrail = newTrail; SceneView.RepaintAll(); }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("มุมกล้องต่อสู้")) ViewFrom(BattleViewRotation());
        if (GUILayout.Button("หน้า")) ViewFrom(Quaternion.LookRotation(Vector3.back));
        if (GUILayout.Button("ข้าง")) ViewFrom(Quaternion.LookRotation(Vector3.left));
        if (GUILayout.Button("หลัง")) ViewFrom(Quaternion.LookRotation(Vector3.forward));
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("ย้ายพรีวิวมากลาง Scene View"))
        {
            anchor = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            if (previewRoot != null) previewRoot.transform.position = anchor;
            foreach (SlashVFXEntry entry in CurrentEntries) Place(entry);
            SceneView.RepaintAll();
        }
        EditorGUILayout.Space(4);
        if (settings != null && GUILayout.Button("บันทึก (SlashVFXSettings.asset)")) AssetDatabase.SaveAssetIfDirty(settings);
        EditorGUILayout.LabelField("ค่าเก็บในพื้นที่ของโมเดลผู้ตี: ในเกมรอยฟันจะตามตัวไปทุกที่ที่ท่านี้เล่น", EditorStyles.miniLabel);
    }

    // มุมกล้องตอนต่อสู้เทียบกับโมเดล: ศัตรูใช้มุมของ Enemy Parry Editor, ผู้เล่นใช้กล้องฉาก (3, 65, 0) เทียบกับโมเดลที่หัน 65 แล้วถอยไปด้านหลังขวา
    Quaternion BattleViewRotation()
    {
        if (side == Side.Enemy && ownerIndex < enemySets.Count)
        {
            EnemyAnimationSet set = enemySets[ownerIndex];
            return Quaternion.Inverse(Quaternion.Euler(0f, set.previewYaw, 0f)) * Quaternion.Euler(set.previewCameraEuler);
        }
        return Quaternion.Euler(12f, -30f, 0f);
    }

    // ---------------------------------------------------------------- จุดปล่อยกระสุน (ผู้เล่น)
    WeaponType? PlayerWeapon => side == Side.Player && playerSet != null && ownerIndex < playerSet.weapons.Count ? playerSet.weapons[ownerIndex].weapon : (WeaponType?)null;

    ShotOriginSettings EnsureShotSettings()
    {
        if (shotSettings != null) return shotSettings;
        shotSettings = AssetDatabase.LoadAssetAtPath<ShotOriginSettings>(ShotOriginSettings.AssetPath);
        if (shotSettings == null)
        {
            shotSettings = CreateInstance<ShotOriginSettings>();
            AssetDatabase.CreateAsset(shotSettings, ShotOriginSettings.AssetPath);
            AssetDatabase.SaveAssets();
        }
        return shotSettings;
    }

    ShotOriginEntry ActiveShot(Move move)
    {
        if (shotSettings == null) shotSettings = AssetDatabase.LoadAssetAtPath<ShotOriginSettings>(ShotOriginSettings.AssetPath);
        WeaponType? weaponType = PlayerWeapon;
        return shotSettings != null && weaponType.HasValue && move != null ? shotSettings.Find(weaponType.Value, move.state) : null;
    }

    Transform Bone(string name)
    {
        if (model == null || string.IsNullOrEmpty(name)) return null;
        return model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }

    // จุดที่โมเดลเล็ง (ข้างหน้าไกล ๆ ระดับอก) ใช้แทนเป้าจริงตอนพรีวิว
    Vector3 PreviewAim => model.transform.TransformPoint(new Vector3(0f, modelHeight * 0.55f, modelHeight * 6f));

    Vector3 AutoShotPoint()
    {
        Vector3 handAt = hand != null ? hand.position : model.transform.position;
        return PlayerBattleAnimator.AutoShotOrigin(weapon, handAt, PreviewAim, baked);
    }

    Vector3 ShotPoint(ShotOriginEntry entry)
    {
        if (entry != null)
        {
            Transform bone = Bone(entry.bone);
            if (bone != null) return ShotOriginSettings.Resolve(entry, bone, model.transform.lossyScale.x);
        }
        return AutoShotPoint();
    }

    string[] BoneChoices(string currentBone)
    {
        var names = new List<string>();
        void Add(string n) { if (!string.IsNullOrEmpty(n) && !names.Contains(n)) names.Add(n); }
        if (weapon != null && weapon.rootBone != null) Add(weapon.rootBone.name);
        Add("DEF-hand.R");
        Add("DEF-hand.L");
        if (weapon != null) foreach (Transform b in weapon.bones) if (b != null) Add(b.name);
        Add(currentBone);
        return names.ToArray();
    }

    string DefaultBone() => weapon != null && weapon.rootBone != null ? weapon.rootBone.name : "DEF-hand.R";

    // ตั้งจุดยิงของอาวุธ/ท่า ให้อยู่ที่ world (เก็บเป็นระยะจากกระดูก)
    void SetShot(WeaponType weaponType, string state, Vector3 world)
    {
        EnsureShotSettings();
        Undo.RecordObject(shotSettings, "Set Shot Origin");
        ShotOriginEntry entry = shotSettings.FindExact(weaponType, state);
        if (entry == null)
        {
            entry = new ShotOriginEntry { weapon = weaponType, state = state, bone = DefaultBone() };
            shotSettings.entries.Add(entry);
        }
        Transform bone = Bone(entry.bone);
        if (bone == null) { entry.bone = "DEF-hand.R"; bone = Bone(entry.bone); }
        if (bone != null) entry.offset = ShotOriginSettings.ToOffset(world, bone, model.transform.lossyScale.x);
        EditorUtility.SetDirty(shotSettings);
        SceneView.RepaintAll();
    }

    void DrawShotOrigin(Move move)
    {
        WeaponType? weaponType = PlayerWeapon;
        if (!weaponType.HasValue || model == null) return;
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("จุดปล่อยกระสุน (ท่ายิงของ " + weaponType.Value + ")", EditorStyles.boldLabel);
        ShotOriginEntry exact = shotSettings != null ? shotSettings.FindExact(weaponType.Value, move.state) : null;
        ShotOriginEntry active = ActiveShot(move);
        string status = active == null ? "อัตโนมัติ (จุดของอาวุธที่ยื่นไปทางเป้ามากที่สุด)"
            : exact != null ? "ค่าของท่านี้ (" + move.state + ")" : "ค่าของทุกท่าของ " + weaponType.Value;
        EditorGUILayout.HelpBox("ตอนนี้ใช้: " + status + "\nเลื่อนเวลาไปที่จังหวะยิง (เส้นแดง) แล้วลากจุดสีส้มในฉาก จุดติดกับกระดูกที่เลือก จึงตามอาวุธไปทุกเฟรมของท่า", MessageType.None);

        Vector3 point = ShotPoint(active);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(exact != null ? "ตั้งใหม่ (ท่านี้)" : "ตั้งเฉพาะท่านี้")) SetShot(weaponType.Value, move.state, point);
        if (GUILayout.Button("ตั้งให้ทุกท่าของอาวุธนี้")) SetShot(weaponType.Value, "", point);
        EditorGUILayout.EndHorizontal();
        if (active == null) return;

        EditorGUI.BeginChangeCheck();
        string[] bones = BoneChoices(active.bone);
        int boneIndex = EditorGUILayout.Popup("ติดกับกระดูก", Mathf.Max(0, System.Array.IndexOf(bones, active.bone)), bones);
        Vector3 offset = EditorGUILayout.Vector3Field("ระยะจากกระดูก", active.offset);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(shotSettings, "Edit Shot Origin");
            if (bones[boneIndex] != active.bone)
            {
                // เปลี่ยนกระดูกแต่คงจุดเดิมในฉาก
                Vector3 keep = ShotPoint(active);
                active.bone = bones[boneIndex];
                Transform bone = Bone(active.bone);
                if (bone != null) active.offset = ShotOriginSettings.ToOffset(keep, bone, model.transform.lossyScale.x);
            }
            else active.offset = offset;
            EditorUtility.SetDirty(shotSettings);
            SceneView.RepaintAll();
        }

        EditorGUILayout.BeginHorizontal();
        editShot = GUILayout.Toggle(editShot, "ลากในฉาก", EditorStyles.miniButton);
        if (GUILayout.Button("จับปลายอาวุธ ณ เวลานี้", EditorStyles.miniButton)) SetShot(active.weapon, active.state, AutoShotPoint());
        if (GUILayout.Button("ลบ (กลับไปอัตโนมัติ)", EditorStyles.miniButton))
        {
            Undo.RecordObject(shotSettings, "Remove Shot Origin");
            shotSettings.entries.Remove(active);
            EditorUtility.SetDirty(shotSettings);
            SceneView.RepaintAll();
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("บันทึก (ShotOriginSettings.asset)", EditorStyles.miniButton)) AssetDatabase.SaveAssetIfDirty(shotSettings);
    }

    void DrawShotScene(Transform m)
    {
        WeaponType? weaponType = PlayerWeapon;
        Move move = CurrentMove;
        if (!weaponType.HasValue || move == null) return;
        ShotOriginEntry active = ActiveShot(move);
        Vector3 point = ShotPoint(active);
        float size = HandleUtility.GetHandleSize(point) * 0.12f;
        Handles.color = new Color(1f, 0.55f, 0.15f, 1f);
        Handles.SphereHandleCap(0, point, Quaternion.identity, size, EventType.Repaint);
        Handles.DrawDottedLine(point, Vector3.Lerp(point, PreviewAim, 0.35f), 4f);
        Handles.Label(point + Vector3.up * size, active == null ? " จุดยิง (อัตโนมัติ)" : " จุดยิง");

        if (active == null || !editShot || shotSettings == null) return;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(point, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Transform bone = Bone(active.bone);
            if (bone == null) return;
            Undo.RecordObject(shotSettings, "Move Shot Origin");
            active.offset = ShotOriginSettings.ToOffset(moved, bone, m.lossyScale.x);
            EditorUtility.SetDirty(shotSettings);
            Repaint();
        }
    }
}
