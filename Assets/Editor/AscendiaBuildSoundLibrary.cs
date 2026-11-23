using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools/ASCENDIA/Audio/Build Sound Library: สร้าง/เติม Assets/Resources/SoundLibrary.asset จากตาราง Defaults ด้านล่าง
// - เหตุการณ์ที่ยังไม่มีใน Library = เพิ่มให้พร้อมไฟล์และค่าเริ่มต้น
// - เหตุการณ์ที่มีแล้ว = ไม่แตะค่าที่ปรับเอง (เติมไฟล์ให้เฉพาะเหตุการณ์ที่ยังไม่มีไฟล์เลย)
// Tools/ASCENDIA/Audio/Reset Sound Library To Defaults: ล้างแล้วสร้างใหม่ตามตารางทั้งหมด (ค่าที่ปรับเองหาย)
// ไฟล์เสียง: Assets/Sound (ของทีม) + Assets/Sound/LetYouPick (คลังเสียงที่ทีมเตรียมไว้ให้เลือก)
//  - UI: Cursor / Back / Error / Confirm tones ชุด style 4 (โทนอุ่น ความถี่ต่ำกว่าชุดอื่น เข้ากับธีมพู่กันหมึก) ใช้หลายโน้ตสุ่ม
//  - ศัตรู (Orc / Guardian): Orc Pack (ร้องตอนตี เจ็บ ตาย โกรธ)
//  - เวท/กระทบ: Spells (Spell Impact, Rock Meteor Throw, Ice Freeze, Firebuff, Rock Wall, Ice Throw)
//  - ปีน: Footsteps_Rock (เดิน/วิ่ง/ลงเท้า), Foley_Grass (สมุนไพร)
//  - เสียงผู้เล่น: Ian Lampert (เสียงผู้ชายโทนสูงสุดในคลัง เข้ากับตัวละครหนุ่ม) Grunting / Death / Completion
// ตั้งค่า Import: เสียงสั้น = Decompress On Load (ADPCM), เสียงวน/ยาว = Compressed In Memory (Vorbis), Mono
public static class AscendiaBuildSoundLibrary
{
    const string LibraryPath = "Assets/Resources/" + SoundLibrary.ResourcePath + ".asset";
    const string Root = "Assets/Sound/";
    const string LP = "LetYouPick/";
    const string Confirm4 = LP + "Confirm_tones/style4/confirm_style_4_";
    const string Back4 = LP + "Back_tones/style4/back_style_4_";
    const string Error4 = LP + "Error_tones/style4/error_style_4_";
    const string Orc = LP + "Orc Pack/";
    const string Spell = LP + "Spells/";
    const string Rock = LP + "Footsteps_Rock/";
    const string Ian = "Male/Ian Lampert/";

    struct Row
    {
        public string id;
        public SoundCategory category;
        public string[] files;
        public float volume;
        public Vector2 pitch;
        public int voices;
        public float cooldown;
        public float chance;
        public bool loop;
        public string note;
    }

    static Row R(string id, SoundCategory category, float volume, Vector2 pitch, string note, params string[] files) =>
        new Row { id = id, category = category, files = files, volume = volume, pitch = pitch, voices = 4, cooldown = 0.03f, chance = 1f, note = note };

    static Row Voices(this Row row, int voices, float cooldown) { row.voices = voices; row.cooldown = cooldown; return row; }
    static Row Chance(this Row row, float chance) { row.chance = chance; return row; }
    static Row Loop(this Row row) { row.loop = true; row.voices = 2; return row; }

    // ไฟล์ชุดเลข เช่น Seq("…/hit", 1, 5, ".wav") = hit1.wav … hit5.wav
    static string[] Seq(string prefix, int from, int to, string suffix)
    {
        var list = new List<string>();
        for (int i = from; i <= to; i++) list.Add(prefix + i + suffix);
        return list.ToArray();
    }
    static string[] Seq2(string prefix, int from, int to, string suffix)
    {
        var list = new List<string>();
        for (int i = from; i <= to; i++) list.Add(prefix + i.ToString("00") + suffix);
        return list.ToArray();
    }
    static string[] Voice(string folder, string kind) => Seq(LP + folder + "/" + Ian + kind + "_", 1, 10, "_ian.wav");
    static string[] Join(params string[][] parts) { var l = new List<string>(); foreach (var p in parts) l.AddRange(p); return l.ToArray(); }

    static readonly Vector2 Tight = new Vector2(0.97f, 1.03f);
    static readonly Vector2 Normal = new Vector2(0.93f, 1.07f);
    static readonly Vector2 Wide = new Vector2(0.88f, 1.12f);
    const SoundCategory FX = SoundCategory.Effects, UI = SoundCategory.UI, MUS = SoundCategory.Music, AMB = SoundCategory.Ambience;

    static readonly Row[] Defaults =
    {
        // ---- UI (Confirm/Click ของทีม + ชุดโทน style 4 จาก LetYouPick)
        R(Sfx.UIHover, UI, 0.5f, new Vector2(0.95f, 1.08f), "ชี้เมาส์ที่ปุ่ม/การ์ด", LP + "Cursor_tones/cursor_style_5.wav").Voices(2, 0.04f),
        R(Sfx.UIClick, UI, 0.7f, Tight, "กดปุ่มทั่วไป / เลือก", "UI_choose.wav").Voices(3, 0.04f),
        R(Sfx.UIConfirm, UI, 0.8f, Tight, "ยืนยัน (เริ่มเกม, Confirm การ์ดเส้นทาง, Craft)", "UI_confirm.wav").Voices(2, 0.08f),
        R(Sfx.UIBack, UI, 0.85f, Tight, "ย้อนกลับ / ปิดหน้าต่าง", Back4 + "001.wav", Back4 + "003.wav", Back4 + "004.wav").Voices(2, 0.05f),
        R(Sfx.UIError, UI, 0.85f, Tight, "ทำไม่ได้ (AP ไม่พอ ฯลฯ)", Error4 + "001.wav", Error4 + "002.wav", Error4 + "003.wav").Voices(1, 0.15f),

        // ---- อาวุธผู้เล่น (ของทีม)
        R(Sfx.DaggerSwing, FX, 0.8f, Wide, "ฟันดาบสั้น (ทุกจังหวะโดน)", "dagger.mp3", "dagger2.mp3", "dagger3.mp3").Voices(4, 0.05f),
        R(Sfx.SwordSwing, FX, 0.85f, Normal, "ฟันดาบยาว", "sword.mp3", "sword2.mp3", "sword3.mp3").Voices(4, 0.05f),
        R(Sfx.GunShot, FX, 0.8f, Normal, "ยิงปืน (ทุกนัด รวม Free Aim)", "gun.mp3").Voices(6, 0.03f),
        R(Sfx.SpellCast, FX, 0.75f, Normal, "ร่ายเวท / ยิงลูกพลังงาน", "spell.mp3").Voices(3, 0.08f),
        R(Sfx.KnifeThrow, FX, 0.7f, new Vector2(1.12f, 1.3f), "ปามีด (เสียงสะบัดดาบสั้น เร่ง Pitch)", "dagger.mp3", "dagger2.mp3", "dagger3.mp3").Voices(4, 0.03f),
        R(Sfx.WeaponSwitch, FX, 0.8f, Normal, "สลับอาวุธ", "Switch.mp3"),

        // ---- โดน
        R(Sfx.HitFlesh, FX, 0.4f, new Vector2(0.85f, 1.05f), "ดาเมจลงศัตรู (เสียงกระแทกหนัก ๆ เบา ๆ)", Spell + "Rock Meteor Throw 1.wav", Spell + "Rock Meteor Throw 2.wav").Voices(6, 0.04f),
        R(Sfx.HitCritical, FX, 0.9f, new Vector2(0.7f, 0.8f), "ปิดท้ายฆ่าศัตรู", Spell + "Rock Meteor Throw 1.wav", Spell + "Rock Meteor Throw 2.wav").Voices(2, 0.06f),
        R(Sfx.ShotImpact, FX, 0.6f, Wide, "กระสุนโดนเป้า (ว่าง = ใช้เสียงโดนของศัตรู)").Voices(6, 0.03f),
        R(Sfx.BlastImpact, FX, 0.8f, Normal, "ลูกพลังงานระเบิด", Spell + "Spell Impact 1.wav", Spell + "Spell Impact 2.wav", Spell + "Spell Impact 3.wav").Voices(3, 0.05f),
        R(Sfx.KnifeImpact, FX, 0.7f, Wide, "มีดปักเป้า (ว่าง = ใช้เสียงโดนของศัตรู)").Voices(4, 0.03f),
        R(Sfx.PlayerHurt, FX, 0.8f, Normal, "ผู้เล่นโดน (Parry พลาด)", "hurt.mp3").Voices(2, 0.15f),

        // ---- เสียงผู้เล่น (Ian Lampert)
        R(Sfx.PlayerGrunt, FX, 0.7f, Normal, "ฮึบตอนกระโดดบนผนัง", Voice("9 - Grunting", "grunting")).Voices(1, 0.3f),
        R(Sfx.PlayerAttackVocal, FX, 0.6f, Normal, "ฮึบตอนเริ่มท่าฟัน (บางครั้ง)", Voice("9 - Grunting", "grunting")).Voices(1, 0.6f).Chance(0.35f),
        R(Sfx.PlayerDeath, FX, 0.9f, Tight, "ผู้เล่นแพ้ (สลายหาย)", Voice("8 - Death", "death")).Voices(1, 1f),
        R(Sfx.PlayerWin, FX, 0.85f, Tight, "ชนะการต่อสู้", Voice("1 - Completion", "completion")).Voices(1, 1f),

        // ---- Parry
        R(Sfx.ParrySuccess, FX, 0.9f, Normal, "Parry สำเร็จ", "parry.mp3").Voices(3, 0.05f),
        R(Sfx.ParryHint, FX, 0.45f, new Vector2(1.25f, 1.35f), "สัญญาณทิศ Parry ขึ้น", LP + "Cursor_tones/cursor_style_5.wav").Voices(2, 0.1f),

        // ---- ศัตรู (อาวุธ = ของทีม, เสียงร้อง = Orc Pack)
        R(Sfx.EnemyBladeAttack, FX, 0.8f, Normal, "Blade Guardian ฟัน (ทุกจังหวะ Strike)", "sword kiki.mp3").Voices(3, 0.05f),
        R(Sfx.EnemyAxeAttack, FX, 0.8f, Normal, "Axe Guardian ฟัน", "axe kiki.wav").Voices(3, 0.05f),
        R(Sfx.EnemyWandCast, FX, 0.75f, Normal, "Wand Guardian ร่าย/ยิง", "spell kiki.wav").Voices(3, 0.05f),
        R(Sfx.EnemyBossAttack, FX, 0.85f, Normal, "บอสฟัน", "clash boss.mp3").Voices(3, 0.05f),
        R(Sfx.EnemyBossRoar, FX, 1f, new Vector2(0.97f, 1f), "บอสคำราม (Rage)", "roar.mp3").Voices(1, 1f),
        R(Sfx.EnemyHurt, FX, 0.75f, Wide, "ศัตรูเจ็บ", Join(new[] { "kiki hurt.mp3" }, Seq(Orc + "hit", 1, 5, ".wav"))).Voices(2, 0.2f),
        R(Sfx.EnemyVocalAttack, FX, 0.7f, Normal, "ศัตรูร้องตอนฟัน (บางครั้ง)", Join(Seq(Orc + "attack", 1, 4, ".wav"), Seq(Orc + "lightattack", 1, 3, ".wav"))).Voices(2, 0.25f).Chance(0.5f),
        R(Sfx.EnemyVocalHeavy, FX, 0.85f, Normal, "บอสร้องตอนฟัน", Seq(Orc + "heavyattack", 1, 4, ".wav")).Voices(1, 0.4f).Chance(0.7f),
        R(Sfx.EnemyVocalDeath, FX, 0.8f, Normal, "ศัตรูร้องตอนตาย", Seq(Orc + "death", 1, 5, ".wav")).Voices(3, 0.05f),
        R(Sfx.EnemyDeath, FX, 0.75f, new Vector2(0.8f, 0.9f), "ศัตรูตาย สลายหาย (ไฟเผาจางหาย)", Spell + "Firebuff 1.wav", Spell + "Firebuff 2.wav").Voices(3, 0.05f),
        R(Sfx.EnemySpawn, FX, 0.5f, Normal, "ศัตรู/ผู้เล่นเข้าฉาก (หินก่อตัว)", Spell + "Rock Wall 1.wav", Spell + "Rock Wall 2.wav").Voices(3, 0.08f),
        R(Sfx.EnemyStagger, FX, 0.85f, Normal, "ศัตรูติด Stagger (BREAK!)", Spell + "Ice Freeze 1.wav", Spell + "Ice Freeze 2.wav").Voices(2, 0.1f),

        // ---- การต่อสู้
        R(Sfx.TurnPlayer, UI, 0.6f, Tight, "เริ่มเทิร์นผู้เล่น", Confirm4 + "echo_002.wav").Voices(1, 0.3f),
        R(Sfx.TurnEnemy, FX, 0.6f, Normal, "เริ่มเทิร์นศัตรู (ศัตรูคำราม)", Seq(Orc + "angry", 1, 3, ".wav")).Voices(1, 0.3f),
        R(Sfx.Victory, UI, 0.8f, new Vector2(1f, 1f), "ชนะ", Confirm4 + "echo_007.wav").Voices(1, 1f),
        R(Sfx.Defeat, UI, 0.8f, new Vector2(1f, 1f), "แพ้", Error4 + "echo_001.wav").Voices(1, 1f),
        R(Sfx.QTEHit, UI, 0.7f, Tight, "กด QTE ทัน", Confirm4 + "005.wav", Confirm4 + "006.wav", Confirm4 + "007.wav").Voices(3, 0.05f),
        R(Sfx.QTEMiss, UI, 0.7f, Tight, "กด QTE พลาด", Error4 + "001.wav").Voices(2, 0.05f),
        R(Sfx.StatusBuff, FX, 0.6f, Tight, "ได้บัฟ", Confirm4 + "echo_005.wav", Confirm4 + "echo_006.wav").Voices(2, 0.1f),
        R(Sfx.StatusDebuff, FX, 0.6f, Normal, "ศัตรูติดดีบัฟ", Spell + "Ice Throw 1.wav", Spell + "Ice Throw 2.wav").Voices(2, 0.1f),
        R(Sfx.OrbHarvest, FX, 0.7f, Tight, "Wand เก็บลูกธาตุ", Confirm4 + "echo_003.wav").Voices(2, 0.1f),

        // ---- ปีนเขา
        R(Sfx.ClimbLoop, FX, 0.6f, Tight, "เสียงปีนวน (ระหว่างขยับบนผนัง)", "Climbing.mp3").Loop(),
        R(Sfx.WalkLoop, FX, 0.55f, Tight, "เสียงเดินแบบวน (สำรอง ตอนนี้ใช้ climb.step ทีละก้าวแทน)", "walkkk.mp3").Loop(),
        R(Sfx.Footstep, FX, 0.6f, Wide, "ก้าวเดินบนหิน (ทีละก้าว)", Seq2(LP + "Footsteps_Rock/Footsteps_Rock_Walk/Footsteps_Rock_Walk_", 1, 9, ".wav")).Voices(3, 0.08f),
        R(Sfx.FootstepRun, FX, 0.65f, Wide, "ก้าววิ่งบนหิน", Seq2(Rock + "Footsteps_Rock_Run/Footsteps_Rock_Run_", 1, 10, ".wav")).Voices(3, 0.06f),
        R(Sfx.ClimbJump, FX, 0.8f, Normal, "กระโดดบนผนัง", "Jump.mp3").Voices(2, 0.1f),
        R(Sfx.ClimbLand, FX, 0.75f, Wide, "คว้าผนังหลังกระโดด / ลงเท้า", Seq2(Rock + "Footsteps_Rock_Jump/Footsteps_Rock_Jump_Land_", 1, 6, ".wav")).Voices(2, 0.1f),
        R(Sfx.PickupGem, FX, 0.7f, Tight, "เก็บแร่", Confirm4 + "echo_006.wav", Confirm4 + "echo_007.wav").Voices(2, 0.05f),
        R(Sfx.PickupHerb, FX, 0.75f, Normal, "เก็บสมุนไพร", Seq2(LP + "Footsteps_Grass/Foley_Grass/Footsteps_Grass_Tall_Movement_", 1, 3, ".wav")).Voices(2, 0.05f),
        R(Sfx.PitonLost, FX, 0.8f, Tight, "เสียหมุด (หมดแรง)", Error4 + "echo_002.wav").Voices(1, 0.3f),
        R(Sfx.FloorWhoosh, FX, 0.6f, Tight, "เปลี่ยนชั้น (จอดำ) ว่าง = เงียบ").Voices(1, 0.5f),

        // ---- แคมป์
        R(Sfx.ForgeHammer, FX, 0.8f, new Vector2(0.8f, 0.92f), "ตีอาวุธ (เสียงปะทะโลหะ เสียงต่ำลง)", "clash boss.mp3").Voices(2, 0.1f),
        R(Sfx.ForgeUpgrade, FX, 0.8f, Tight, "อัปเกรดอาวุธ (เล่นคู่กับเสียงตี)", Confirm4 + "echo_004.wav").Voices(1, 0.2f),
        R(Sfx.CampRest, FX, 0.7f, Tight, "พักที่แคมป์", Confirm4 + "echo_001.wav").Voices(1, 0.5f),

        // ---- เพลง / บรรยากาศ (ยังไม่มีไฟล์: ลากไฟล์ใส่ช่อง clips ได้เลย)
        R(Sfx.MusicTitle, MUS, 0.6f, new Vector2(1f, 1f), "เพลงหน้า Title").Loop(),
        R(Sfx.MusicClimb, MUS, 0.5f, new Vector2(1f, 1f), "เพลงตอนปีน").Loop(),
        R(Sfx.MusicCamp, MUS, 0.5f, new Vector2(1f, 1f), "เพลงแคมป์").Loop(),
        R(Sfx.MusicBattle, MUS, 0.55f, new Vector2(1f, 1f), "เพลงต่อสู้").Loop(),
        R(Sfx.AmbWind, AMB, 0.5f, new Vector2(1f, 1f), "ลมบนหน้าผา (วน)").Loop(),
        R(Sfx.AmbCampfire, AMB, 0.6f, new Vector2(1f, 1f), "กองไฟแคมป์ (วน)").Loop(),
        R(Sfx.AmbBattle, AMB, 0.35f, new Vector2(1f, 1f), "บรรยากาศฉากต่อสู้ (วน)").Loop(),
    };

    static readonly SceneAudio[] DefaultScenes =
    {
        new SceneAudio { scene = "TitleScene", music = Sfx.MusicTitle, ambience = "-" },
        new SceneAudio { scene = "ClimbScene", music = Sfx.MusicClimb, ambience = Sfx.AmbWind },
        new SceneAudio { scene = "CampScene", music = Sfx.MusicCamp, ambience = Sfx.AmbCampfire },
        new SceneAudio { scene = "TurnBase_Phase", music = Sfx.MusicBattle, ambience = Sfx.AmbBattle },
    };

    [MenuItem("Tools/ASCENDIA/Audio/Build Sound Library")]
    static void BuildMenu() => Debug.Log(Build(false));

    [MenuItem("Tools/ASCENDIA/Audio/Reset Sound Library To Defaults")]
    static void ResetMenu()
    {
        if (EditorUtility.DisplayDialog("Reset Sound Library", "ล้างค่าเสียงทั้งหมดใน SoundLibrary แล้วตั้งใหม่ตามตารางเริ่มต้น?", "Reset", "Cancel"))
            Debug.Log(Build(true));
    }

    [InitializeOnLoadMethod]
    static void EnsureLibrary()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(LibraryPath)) return;
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "UI_choose.wav") == null) return; // ยัง Import ไม่เสร็จ
            Debug.Log(Build(false));
        };
    }

    public static string Build(bool reset)
    {
        var log = new System.Text.StringBuilder("Sound Library: ");
        Directory.CreateDirectory("Assets/Resources");
        SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
            log.Append("created, ");
        }
        if (reset)
        {
            library.events.Clear();
            library.scenes.Clear();
            log.Append("reset, ");
        }

        int added = 0, filled = 0, missing = 0;
        foreach (Row row in Defaults)
        {
            var clips = new List<AudioClip>();
            foreach (string file in row.files)
            {
                string path = Root + file;
                ConfigureImport(path, row.loop);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) clips.Add(clip);
                else { log.Append($"\n  missing {path}"); missing++; }
            }

            SoundEvent existing = library.events.Find(e => e.id == row.id);
            if (existing == null)
            {
                library.events.Add(new SoundEvent
                {
                    id = row.id,
                    category = row.category,
                    clips = clips.ToArray(),
                    volume = row.volume,
                    pitchRandom = row.pitch,
                    volumeRandom = row.loop ? Vector2.one : new Vector2(0.9f, 1f),
                    maxVoices = row.voices,
                    cooldown = row.cooldown,
                    chance = row.chance,
                    loop = row.loop,
                    note = row.note,
                });
                added++;
            }
            else if ((existing.clips == null || existing.clips.Length == 0) && clips.Count > 0)
            {
                existing.clips = clips.ToArray();
                filled++;
            }
        }

        foreach (SceneAudio scene in DefaultScenes)
            if (library.FindScene(scene.scene) == null) library.scenes.Add(new SceneAudio { scene = scene.scene, music = scene.music, ambience = scene.ambience });

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        log.Append($"{added} events added, {filled} filled, {missing} files missing, {library.events.Count} total -> {LibraryPath}");
        return log.ToString();
    }

    // เสียงสั้นโหลดเป็น PCM ทั้งไฟล์ (เล่นทันทีไม่สะดุด และ SoundManager อ่านข้อมูลไปตัดช่วงเงียบได้) เสียงวน/ยาวเก็บแบบบีบอัด
    static void ConfigureImport(string path, bool loop)
    {
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) return;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        bool longClip = loop || (clip != null && clip.length > 8f);
        AudioClipLoadType load = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        AudioCompressionFormat format = longClip ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
        if (settings.loadType == load && settings.compressionFormat == format && importer.forceToMono) return;
        settings.loadType = load;
        settings.compressionFormat = format;
        settings.quality = 0.7f;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.loadInBackground = longClip;
        importer.SaveAndReimport();
    }
}
