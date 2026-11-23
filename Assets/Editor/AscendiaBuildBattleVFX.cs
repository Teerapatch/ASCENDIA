using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

// Tools/ASCENDIA/Build Battle VFX: คัด VFX จากฉากทดสอบของอาร์ต (Assets/Scenes/ReArt/VFX_test.unity) มาเป็น Prefab ใน Resources/BattleVFX
// ให้ CombatVFX ใช้ตอนต่อสู้ ไม่แก้/ไม่บันทึกฉากทดสอบ (ถ้าเปิดอยู่แล้วใช้ตัวที่เปิด ไม่งั้นเปิดแบบ Additive แล้วปิดทิ้ง)
//
// โครง Prefab: ราก > Pivot > ชุด VFX เดิม
//   ราก  = กรอบของจอ: Z = ทิศที่กล้องมอง, X = ทิศของแรง (ทิศที่ฟัน / ทิศที่ต้องลาก) บนจอ, Y = ขึ้นบนจอ  (CombatVFX หมุนรากตามนี้)
//   Pivot = ย้อนการหมุนของกล้องในฉากทดสอบ (กล้องต่อสู้เห็น VFX แบบเดียวกับที่อาร์ตจัดไว้) แล้วหมุนรอบแกนจอให้ทิศหลักของ VFX ชี้ +X
// VFX_Slash ไม่ได้สร้างจากที่นี่แล้ว: เป็น Prefab ที่จัดเองใน Resources/BattleVFX (CombatVFX.Slash หมุนตามการเหวี่ยงจริงแบบ 3D) รันเครื่องมือนี้ไม่ทับ
//   แต่ยังทำสำเนากราฟ Slash_01_Battle.vfx ให้ (ExtraGraphs) เพราะ Prefab นั้นใช้สำเนานี้
// ทิศหลักคำนวณจากข้อมูลจริง:
//   Parry: สองชุดหันออกซ้าย/ขวาตามแกน X ของกลุ่ม -> แกน X ของกลุ่ม
//   Hint : ประกายแบบ Parry ที่อาร์ตบีบเป็นเส้น (สเกล 2 x 0.1) วางตามแกน +Z ของกลุ่ม -> แกน +Z ของกลุ่ม (เส้นไม่มีหัวท้าย บอกแนวที่ต้องลาก)
//   Hit  : ไม่มีทิศ (CombatVFX สุ่มมุมหมุนให้)
//
// กราฟ VFX: Prefab ไม่ใช้ไฟล์ .vfx ของอาร์ตตรง ๆ แต่ใช้สำเนาที่แก้แล้วใน Assets/BattlePhase/VFX/Graphs (ไฟล์ของอาร์ตไม่ถูกแตะ)
//   ของอาร์ตทำไว้ดูวนในฉากทดสอบ: Periodic Burst ทุก 1 วิ (ระเบิดครั้งแรกหลังผ่านไป 1 วิ จึงดูหน่วงมากในเกม)
//   และอัปเดตแบบ Fixed Delta Time ตามเวลาเกม (เฟรมเรตต่ำ = เล่นช้า, หยุด/ช้าตาม Hitstop และ Slow-mo ของ Parry)
//   สำเนา: Burst ครั้งเดียวทันที (Single, Delay 0) + Delta Time ไม่สน Time Scale (Hint วนซ้ำเองใน ParryManager)
// ตัวทดสอบในฉากสูง 2 หน่วย CombatVFX ขยายตามความสูงตัวละคร ปรับมุม/ขนาดเพิ่มได้ที่ Pivot / สเกลของรากใน Prefab (รันซ้ำจะสร้างทับ)
public static class AscendiaBuildBattleVFX
{
    const string TestScene = "Assets/Scenes/ReArt/VFX_test.unity";
    const float ParrySparkYaw = 90f; // ดูที่ใช้ใน Build: แนวประกายของ Parry_01 ไปอยู่บนแกน X (ทิศที่ลาก)
    public const string Folder = "Assets/Resources/BattleVFX";
    public const string GraphFolder = "Assets/BattlePhase/VFX/Graphs";
    // กราฟที่ทำสำเนาให้ด้วย แม้ไม่ได้สร้าง Prefab จากฉากทดสอบ
    static readonly string[] ExtraGraphs = { "Assets/Assets (jmes)/_vfxBattle/Slash_01.vfx" };

    enum Axis { None, GroupX, GroupZ }

    // world = กรอบของรากเป็นโลก 3D ไม่ใช่จอ: Pivot ไม่หมุน ชุด VFX คงมุมเดิมของอาร์ต (+Z ของรากคือทิศที่ตัวละครหัน, X คือแนวประกาย)
    //         CombatVFX หมุนรากในโลกเอง ไม่แบนติดจอแบบ Canvas
    static readonly (string root, string group, string prefab, Axis axis, bool center, bool world)[] Sources =
    {
        ("HitVFX", "Parry_01", "VFX_Hit", Axis.None, true, false),
        ("ParryVFX", "Parry_01", "VFX_Parry", Axis.None, true, true),
        ("HintVFX", "Hint_01", "VFX_Hint", Axis.GroupZ, true, false),
    };

    [MenuItem("Tools/ASCENDIA/Build Battle VFX")]
    static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        Scene test = default;
        bool openedHere = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).path == TestScene) test = SceneManager.GetSceneAt(i);
        if (!test.IsValid())
        {
            test = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Additive);
            openedHere = true;
        }

        string log = "";
        try
        {
            System.IO.Directory.CreateDirectory(Folder);
            foreach (string graph in ExtraGraphs) BattleGraph(AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graph), ref log);
            Quaternion testCamera = Quaternion.identity;
            foreach (GameObject r in test.GetRootGameObjects())
                if (r.GetComponent<Camera>() != null) { testCamera = r.transform.rotation; break; }

            foreach (var source in Sources)
            {
                Transform dummy = null;
                foreach (GameObject r in test.GetRootGameObjects()) if (r.name == source.root) dummy = r.transform;
                Transform group = dummy != null ? dummy.Find(source.group) : null;
                if (group == null) { log += $"ไม่พบ {source.root}/{source.group}\n"; continue; }

                Vector3 mainAxis = MainAxis(source.axis, group);
                float roll = 0f;
                if (mainAxis != Vector3.zero)
                {
                    Vector3 onScreen = Quaternion.Inverse(testCamera) * mainAxis;
                    roll = -Mathf.Atan2(onScreen.y, onScreen.x) * Mathf.Rad2Deg;
                }

                var root = new GameObject(source.prefab);
                var pivot = new GameObject("Pivot").transform;
                pivot.SetParent(root.transform, false);
                pivot.localRotation = source.world ? Quaternion.identity : Quaternion.Euler(0f, 0f, roll) * Quaternion.Inverse(testCamera);
                GameObject copy = Object.Instantiate(group.gameObject, pivot);
                copy.name = group.name;
                // ตำแหน่งเทียบกลางตัวทดสอบ (Hit/Parry/Hint วางเองตรงจุดปะทะ จึงตัดระยะเยื้องออก)
                copy.transform.localPosition = source.center ? Vector3.zero : group.localPosition;
                copy.transform.localRotation = group.localRotation;
                // ประกายของ Parry_01 พุ่งตามแกน ±Z ของอาร์ต (กล้องทดสอบมองจากด้านข้างเลยเห็นเป็นซ้าย-ขวา)
                // ในเกม +Z ของรากชี้ไปหาศัตรู = แนวสายตากล้อง ประกายจึงพุ่งเข้า/ออกจากกล้องจนมองไม่เห็น: หมุนกลุ่ม 90° ให้แนวประกายอยู่บน +X (ทิศที่ลาก)
                if (source.prefab == "VFX_Parry") copy.transform.localRotation = Quaternion.Euler(0f, ParrySparkYaw, 0f) * group.localRotation;
                copy.transform.localScale = group.localScale;
                if (source.center)
                {
                    // อาร์ตวางตัวปล่อยเยื้องจากกลางกลุ่ม (Parry อยู่ที่ (2, 1, 0), Hint อยู่ที่ z -4) พอหมุนรากตามทิศ จุดที่เยื้องจะเหวี่ยงไปรอบตัว
                    // ย้ายให้กลางของตัวปล่อยทั้งหมดอยู่ตรงจุดเกิด
                    Vector3 middle = Vector3.zero;
                    int emitters = 0;
                    foreach (Component c in copy.GetComponentsInChildren<Component>(true))
                        if (c is VisualEffect || c is ParticleSystem) { middle += c.transform.position; emitters++; }
                    if (emitters > 0) copy.transform.position -= middle / emitters - pivot.position;
                }
                if (source.prefab == "VFX_Parry") AddParryShockwave(copy.transform.parent);
                if (source.prefab == "VFX_Hint")
                {
                    // บั้งลูกศรไหลตามทิศที่ต้องลาก (+X ของราก) ประกายของอาร์ตเป็นเส้นไม่มีหัวท้าย
                    var arrows = new GameObject("DirectionArrows");
                    arrows.transform.SetParent(root.transform, false);
                    arrows.AddComponent<ParryHintArrows>();
                }
                // Particle System แบบเดิมในกลุ่ม (เช่น Shockwave Distortion ของ Parry) ตั้งวนทุก 1 วิตามเวลาเกม: ให้เล่นครั้งเดียวตามเวลาจริง
                foreach (ParticleSystem ps in copy.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.loop = false;
                    main.useUnscaledTime = true;
                }
                foreach (VisualEffect effect in copy.GetComponentsInChildren<VisualEffect>(true))
                {
                    effect.initialEventName = "OnPlay";
                    VisualEffectAsset battleGraph = BattleGraph(effect.visualEffectAsset, ref log);
                    if (battleGraph != null) effect.visualEffectAsset = battleGraph;
                }

                PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{source.prefab}.prefab");
                Object.DestroyImmediate(root);
                log += $"{source.prefab}: roll {roll:0.0} (main axis on test screen {(mainAxis == Vector3.zero ? "none" : (Quaternion.Inverse(testCamera) * mainAxis).ToString("0.00"))})\n";
            }
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(test, true); }
        return log;
    }

    // วงคลื่นกระแทกที่มองเห็นได้ของ Parry (Shockwave Distortion ของอาร์ตแค่บิดภาพด้านหลัง ไม่เห็นเป็นวง)
    // ของ Vefects เป็นผนังทรงกระบอก (กว้าง 2 สูง 1 แกน Y) ขยายออก: หมุนให้แกนชี้ +Z ของราก (ผู้เล่น -> ศัตรู)
    // กล้องหลังตัวผู้เล่นจึงเห็นเป็นวงแหวนขยายรอบจุดปะทะ ขนาดในหน่วยของราก (รากถูกขยายตามตัวละครอีกที)
    const string ParryShockwavePrefab = "Assets/Vefects/Easy Shockwaves VFX URP/VFX/Shockwaves/Particles/Color Variation/VFX_Shockwave_Vertical_03_Orange_1s.prefab";
    const float ParryShockwaveDiameter = 4.8f;

    static void AddParryShockwave(Transform pivot)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ParryShockwavePrefab);
        if (source == null) return;
        GameObject ring = Object.Instantiate(source, pivot);
        ring.name = "Shockwave Ring";
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = Vector3.one * (ParryShockwaveDiameter / 2f);
        foreach (ParticleSystem ps in ring.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.loop = false;
            main.useUnscaledTime = true; // ขยายวงต่อแม้ Hitstop / Slow-mo ของ Parry หยุดเวลาเกม
        }
    }

    // แปลงบล็อก Constant Spawn Rate เป็น Burst: เปลี่ยนสคริปต์ของบล็อก, ช่อง Rate -> Count (ค่าเดิม = จำนวนตัว),
    // เพิ่มช่อง Delay = 0 (คัดจากช่อง Rate) แล้วตั้ง Single Burst
    const string ConstantRateGuid = "f05c6884b705ce14d82ae720f0ec209f";
    const string BurstGuid = "5e382412bb691334bb79457a6c127924";

    static int ConstantRateToBurst(ref string text)
    {
        int converted = 0;
        while (true)
        {
            int at = text.IndexOf("UnityEditor.VFX.VFXSpawnerConstantRate", System.StringComparison.Ordinal);
            if (at < 0) break;
            int start = text.LastIndexOf("--- !u!", at, System.StringComparison.Ordinal);
            int end = text.IndexOf("\n--- !u!", at, System.StringComparison.Ordinal);
            if (start < 0) break;
            if (end < 0) end = text.Length;
            string block = text.Substring(start, end - start);
            string blockId = System.Text.RegularExpressions.Regex.Match(block, @"&(-?\d+)").Groups[1].Value;
            var slotMatch = System.Text.RegularExpressions.Regex.Match(block, @"m_InputSlots:\n  - \{fileID: (\d+)\}");
            if (!slotMatch.Success) break;
            string rateId = slotMatch.Groups[1].Value;

            long newId = long.Parse(rateId) + 700000;
            while (text.Contains("&" + newId + "\n") || text.Contains("fileID: " + newId + "}")) newId++;

            block = block.Replace("guid: " + ConstantRateGuid, "guid: " + BurstGuid)
                         .Replace("UnityEditor.VFX.VFXSpawnerConstantRate", "UnityEditor.VFX.VFXSpawnerBurst")
                         .Replace(slotMatch.Value, slotMatch.Value + "\n  - {fileID: " + newId + "}");
            block = System.Text.RegularExpressions.Regex.Replace(block, @"(\n  m_ActivationSlot: \{fileID: \d+\})", "$1\n  repeat: 0\n  spawnMode: 0\n  delayMode: 0");
            text = text.Substring(0, start) + block + text.Substring(end);

            // ช่อง Rate -> Count
            int slotStart = text.IndexOf("--- !u!114 &" + rateId + "\n", System.StringComparison.Ordinal);
            if (slotStart < 0) break;
            int slotEnd = text.IndexOf("\n--- !u!", slotStart + 5, System.StringComparison.Ordinal);
            if (slotEnd < 0) slotEnd = text.Length;
            string slot = text.Substring(slotStart, slotEnd - slotStart).Replace("    name: Rate\n", "    name: Count\n");
            text = text.Substring(0, slotStart) + slot + text.Substring(slotEnd);

            // ช่อง Delay ใหม่ = 0
            string delay = slot.Replace("&" + rateId + "\n", "&" + newId + "\n")
                               .Replace("m_MasterSlot: {fileID: " + rateId + "}", "m_MasterSlot: {fileID: " + newId + "}")
                               .Replace("    name: Count\n", "    name: Delay\n");
            delay = System.Text.RegularExpressions.Regex.Replace(delay, @"m_SerializableObject: [^\n]+", "m_SerializableObject: 0");
            if (!text.EndsWith("\n")) text += "\n";
            text += delay.TrimEnd('\n') + "\n";
            converted++;
        }
        return converted;
    }

    // สำเนากราฟสำหรับเกม: ระเบิดทันทีครั้งเดียว และเล่นตามเวลาจริง (ดูหัวไฟล์)
    static VisualEffectAsset BattleGraph(VisualEffectAsset source, ref string log)
    {
        if (source == null) return null;
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath) || sourcePath.StartsWith(GraphFolder)) return source;
        string path = $"{GraphFolder}/{System.IO.Path.GetFileNameWithoutExtension(sourcePath)}_Battle.vfx";
        string text = System.IO.File.ReadAllText(sourcePath).Replace("\r\n", "\n");
        int bursts = 0, delays = 0;
        // Constant Spawn Rate (เช่นแสงวาบหลักของ Parry อัตรา 1 ตัว/วิ = ตัวแรกเกิดหลังผ่านไป 1 วิ) -> Single Burst ทันที
        int rates = ConstantRateToBurst(ref text);
        // Periodic Burst -> Single Burst
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?m)^  repeat: 1$", m => { bursts++; return "  repeat: 0"; });
        // Delay ของ Burst -> 0 (ค่าอยู่ก่อนชื่อช่อง)
        text = System.Text.RegularExpressions.Regex.Replace(text,
            @"(m_SerializableObject: )[^\n]+(\n    m_Space: -?\d+\n  m_Property:\n    name: Delay\n)",
            m => { delays++; return m.Groups[1].Value + "0" + m.Groups[2].Value; });
        // Update Mode: Delta Time + Ignore Time Scale (VFXUpdateMode 1 | 2)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?m)^(    m_UpdateMode: )\d+$", "${1}3");
        System.IO.Directory.CreateDirectory(GraphFolder);
        if (!System.IO.File.Exists(path) || System.IO.File.ReadAllText(path) != text)
        {
            System.IO.File.WriteAllText(path, text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        log += $"  graph {System.IO.Path.GetFileName(sourcePath)} -> {path} (constant rate -> burst {rates}, bursts {bursts}, delays {delays})\n";
        return AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
    }

    // ทิศหลักของ VFX ในโลกของฉากทดสอบ
    static Vector3 MainAxis(Axis axis, Transform group)
    {
        switch (axis)
        {
            case Axis.GroupX: return group.rotation * Vector3.right;
            case Axis.GroupZ: return group.rotation * Vector3.forward;
        }
        return Vector3.zero;
    }
}
