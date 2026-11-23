using System;
using System.Collections.Generic;
using UnityEngine;

// หมวดของเสียง: ความดังของแต่ละหมวดผูกกับหน้า Settings (GameSettings)
//  Effects = เสียงต่อสู้/ปีน/ทั่วไป, UI = ปุ่มและเมนู (ใช้ระดับ Effects), Music = เพลง, Ambience = เสียงบรรยากาศวน (ใช้ระดับ Music)
// ค่าเลขถูกเซฟในไฟล์ ห้ามสลับลำดับ
public enum SoundCategory { Effects = 0, UI = 1, Music = 2, Ambience = 3 }

// วิธีเลือกไฟล์เมื่อเหตุการณ์มีหลายเสียง
public enum SoundPick { RandomNoRepeat = 0, Random = 1, Sequence = 2 }

// เหตุการณ์เสียง 1 อย่าง (เช่น "weapon.sword.swing") มีได้หลายไฟล์ สุ่มเลือก + สุ่ม Pitch/ความดังทุกครั้งที่เล่น
[Serializable]
public class SoundEvent
{
    [Tooltip("ชื่อที่โค้ดเรียก (ดู Sfx.cs)")]
    public string id;
    public SoundCategory category = SoundCategory.Effects;
    [Tooltip("ไฟล์เสียง ใส่หลายไฟล์ได้ จะสุ่มเล่นทีละไฟล์")]
    public AudioClip[] clips = new AudioClip[0];
    public SoundPick pick = SoundPick.RandomNoRepeat;
    [Range(0f, 1.5f)] public float volume = 1f;
    [Tooltip("สุ่มคูณความดัง (ต่ำสุด, สูงสุด)")]
    public Vector2 volumeRandom = new Vector2(0.9f, 1f);
    [Tooltip("สุ่ม Pitch (ต่ำสุด, สูงสุด) 1 = ปกติ")]
    public Vector2 pitchRandom = new Vector2(0.95f, 1.05f);
    [Tooltip("ห้ามเล่นซ้ำถี่กว่านี้ (วินาทีจริง) กันเสียงซ้อนเฟรมเดียวกัน")]
    public float cooldown = 0.03f;
    [Tooltip("เล่นพร้อมกันได้สูงสุดกี่เสียง (เกินแล้วตัดเสียงเก่าสุด)")]
    public int maxVoices = 4;
    [Tooltip("หน่วงก่อนเล่น (วินาทีจริง)")]
    public float delay = 0f;
    [Tooltip("ตัดเสียงต้นไฟล์ (วินาที) ใช้กับไฟล์ที่มีช่วงเงียบหรือเสียงไม่ใช้ข้างหน้า")]
    public float startTime = 0f;
    [Tooltip("0 = 2D ทั้งจอ, 1 = 3D ตามตำแหน่ง")]
    [Range(0f, 1f)] public float spatialBlend = 0f;
    [Tooltip("ใช้กับเสียงวน (เพลง / บรรยากาศ / เสียงปีน-เดิน)")]
    public bool loop;
    [Tooltip("โอกาสที่จะเล่นแต่ละครั้ง (เช่นเสียงตะโกนตอนโจมตี ไม่ต้องดังทุกครั้ง)")]
    [Range(0f, 1f)] public float chance = 1f;
    [Tooltip("ตัดช่วงเงียบหัว/ท้ายไฟล์ให้อัตโนมัติ (ไฟล์ที่มีช่วงเงียบข้างหน้าจะเล่นช้ากว่าจังหวะ)")]
    public bool trimSilence = true;
    [TextArea(1, 3)] public string note;

    [NonSerialized] public float lastPlayed = -100f;
    [NonSerialized] public int lastIndex = -1;
    [NonSerialized] public int sequenceIndex;

    public AudioClip PickClip()
    {
        int count = clips != null ? clips.Length : 0;
        if (count == 0) return null;
        int index;
        switch (pick)
        {
            case SoundPick.Sequence:
                index = sequenceIndex++ % count;
                break;
            case SoundPick.Random:
                index = UnityEngine.Random.Range(0, count);
                break;
            default:
                index = UnityEngine.Random.Range(0, count);
                if (count > 1 && index == lastIndex) index = (index + 1 + UnityEngine.Random.Range(0, count - 1)) % count;
                break;
        }
        lastIndex = index;
        return clips[index];
    }
}

// เพลง/เสียงบรรยากาศประจำฉาก (ชื่อฉาก -> id ของเหตุการณ์เสียง) ว่าง = ไม่เปลี่ยน, "-" = ค่อย ๆ ปิด
[Serializable]
public class SceneAudio
{
    public string scene;
    public string music;
    public string ambience;
}

// ตารางเสียงทั้งเกม: Assets/Resources/SoundLibrary.asset (Tools/ASCENDIA/Audio/Build Sound Library สร้าง/เติม เหตุการณ์ใหม่ให้)
// แก้ไฟล์เสียง / ความดัง / Pitch ของแต่ละเหตุการณ์ได้ใน Inspector ไม่ต้องแก้โค้ด
[CreateAssetMenu(menuName = "Ascendia/Sound Library", fileName = "SoundLibrary")]
public class SoundLibrary : ScriptableObject
{
    public const string ResourcePath = "SoundLibrary";

    public List<SoundEvent> events = new List<SoundEvent>();
    public List<SceneAudio> scenes = new List<SceneAudio>();
    [Tooltip("เวลาค่อย ๆ เปลี่ยนเพลง/บรรยากาศ (วินาที)")]
    public float musicFade = 1.5f;

    private Dictionary<string, SoundEvent> lookup;

    public SoundEvent Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (lookup == null || lookup.Count != events.Count)
        {
            lookup = new Dictionary<string, SoundEvent>();
            foreach (SoundEvent e in events)
                if (e != null && !string.IsNullOrEmpty(e.id)) lookup[e.id] = e;
        }
        lookup.TryGetValue(id, out SoundEvent found);
        return found;
    }

    public SceneAudio FindScene(string scene) => scenes.Find(s => s.scene == scene);

    private void OnValidate() => lookup = null;
}
