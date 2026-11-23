using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// คุมเสียงทั้งเกม: สร้างตัวเองตอนเริ่มเกม อยู่ข้ามฉาก (DontDestroyOnLoad) ไม่ต้องวางในฉาก
// เล่นเสียง: SoundManager.Play(Sfx.SwordSwing) / Play(id, ตำแหน่ง) / PlayLoop(id) + StopLoop(handle)
// ทุกเหตุการณ์อยู่ใน SoundLibrary (Resources) มีหลายไฟล์ได้ สุ่มไฟล์ (ไม่ซ้ำติดกัน) + สุ่ม Pitch/ความดังทุกครั้ง
// ความดัง: Master = AudioListener.volume, หมวด Effects/UI = GameSettings.EffectsVolume, Music/Ambience = GameSettings.MusicVolume
// เพลง/บรรยากาศประจำฉาก (SoundLibrary.scenes) เปลี่ยนเองตอนโหลดฉาก ค่อย ๆ จางข้ามกัน
// เสียงไม่ขึ้นกับ Time.timeScale (Hitstop / Slow-mo / เมนูหยุดเกม ไม่ทำให้เสียงช้าหรือหยุด)
[DefaultExecutionOrder(-900)]
public class SoundManager : MonoBehaviour
{
    private static SoundManager instance;
    private static bool quitting;

    public static SoundManager Instance
    {
        get
        {
            if (instance == null && !quitting && Application.isPlaying)
            {
                instance = FindAnyObjectByType<SoundManager>();
                if (instance == null) instance = new GameObject("SoundManager").AddComponent<SoundManager>();
            }
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        quitting = false;
        _ = Instance;
    }

    [Tooltip("จำนวนช่องเสียงพร้อมกันสูงสุด")]
    public int maxVoices = 40;

    private SoundLibrary library;
    private readonly List<Voice> voices = new List<Voice>();
    private readonly HashSet<string> warned = new HashSet<string>();
    private int nextHandle = 1;
    private float nextScan;

    private class Voice
    {
        public AudioSource source;
        public SoundEvent sound;
        public float baseVolume;
        public float started;
        public float playAt;      // เล่นจริงเมื่อเวลานี้ (delay)
        public bool pending;
        public Transform follow;
        public int handle;
        public float fade = 1f, fadeTarget = 1f, fadeSpeed;
        public float end = float.MaxValue; // หยุดเมื่อเล่นถึงตรงนี้ (ตัดช่วงเงียบท้ายไฟล์)
        public bool Busy => pending || (source != null && source.isPlaying);
    }

    // เพลงและบรรยากาศ: 2 ช่องต่อชนิด จางข้ามกัน
    private class Channel
    {
        public AudioSource[] sources = new AudioSource[2];
        public float[] weights = new float[2];
        public float[] baseVolumes = new float[2];
        public int active;
        public string id;
        public float fadeSpeed = 1f;
    }
    private Channel music, ambience;

    public SoundLibrary Library => library;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        library = Resources.Load<SoundLibrary>(SoundLibrary.ResourcePath);
        if (library == null) Debug.LogWarning("SoundManager: ไม่พบ Resources/SoundLibrary (Tools/ASCENDIA/Audio/Build Sound Library)");
        music = MakeChannel("Music");
        ambience = MakeChannel("Ambience");
        // ฉากที่ใช้งานเปลี่ยน (โหลดฉากใหม่ / ต่อสู้แบบ Additive ขึ้นและจบ) = เปลี่ยนเพลงและบรรยากาศตามฉากนั้น
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameSettings.ApplyAudio();
        ApplySceneAudio(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnApplicationQuit() => quitting = true;

    private Channel MakeChannel(string name)
    {
        var channel = new Channel();
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject(name + i);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            channel.sources[i] = source;
        }
        return channel;
    }

    // ---------------------------------------------------------------- เรียกใช้
    // เล่นเสียงครั้งเดียว position = null คือเสียง 2D ทั้งจอ คืน AudioSource ที่ใช้ (อาจ null ถ้าไม่มีเสียงนี้/ติด cooldown)
    public static AudioSource Play(string id, Vector3? position = null, float volume = 1f, float pitch = 1f)
    {
        SoundManager manager = Instance;
        return manager != null ? manager.PlayInternal(id, position, null, volume, pitch, false, out _) : null;
    }

    // เล่นแบบติดตามวัตถุ (เสียง 3D ที่เคลื่อนตาม)
    public static AudioSource PlayAt(string id, Transform follow, float volume = 1f, float pitch = 1f)
    {
        SoundManager manager = Instance;
        return manager != null && follow != null ? manager.PlayInternal(id, follow.position, follow, volume, pitch, false, out _) : null;
    }

    // เสียงวน (เช่นเสียงปีน/เดิน) คืน handle ไว้หยุดด้วย StopLoop 0 = เล่นไม่ได้
    public static int PlayLoop(string id, Transform follow = null, float volume = 1f, float fadeIn = 0.15f)
    {
        SoundManager manager = Instance;
        if (manager == null) return 0;
        AudioSource source = manager.PlayInternal(id, follow != null ? follow.position : (Vector3?)null, follow, volume, 1f, true, out int handle);
        if (source == null) return 0;
        Voice voice = manager.voices.Find(v => v.handle == handle);
        if (voice != null && fadeIn > 0f) { voice.fade = 0f; voice.fadeTarget = 1f; voice.fadeSpeed = 1f / fadeIn; }
        return handle;
    }

    public static void StopLoop(int handle, float fadeOut = 0.2f)
    {
        if (handle == 0 || instance == null) return;
        Voice voice = instance.voices.Find(v => v.handle == handle);
        if (voice == null) return;
        if (fadeOut <= 0f) { Stop(voice); return; }
        voice.fadeTarget = 0f;
        voice.fadeSpeed = 1f / fadeOut;
    }

    public static bool IsPlaying(int handle) => handle != 0 && instance != null && instance.voices.Exists(v => v.handle == handle && v.Busy);

    // ปรับความดังของเสียงวนที่เล่นอยู่ (0..1 คูณกับค่าในตาราง)
    public static void SetLoopVolume(int handle, float volume)
    {
        if (handle == 0 || instance == null) return;
        Voice voice = instance.voices.Find(v => v.handle == handle);
        if (voice != null) voice.baseVolume = volume * (voice.sound != null ? voice.sound.volume : 1f);
    }

    public static void PlayMusic(string id, float fade = -1f) { if (Instance != null) instance.SetChannel(instance.music, id, fade); }
    public static void PlayAmbience(string id, float fade = -1f) { if (Instance != null) instance.SetChannel(instance.ambience, id, fade); }
    public static void StopMusic(float fade = -1f) => PlayMusic(null, fade);
    public static void StopAmbience(float fade = -1f) => PlayAmbience(null, fade);

    // ---------------------------------------------------------------- ภายใน
    private AudioSource PlayInternal(string id, Vector3? position, Transform follow, float volume, float pitch, bool loop, out int handle)
    {
        handle = 0;
        SoundEvent sound = library != null ? library.Find(id) : null;
        if (sound == null)
        {
            if (warned.Add(id ?? "")) Debug.LogWarning($"SoundManager: ไม่มีเสียง '{id}' ใน SoundLibrary");
            return null;
        }
        float now = Time.unscaledTime;
        if (!loop && now - sound.lastPlayed < sound.cooldown) return null;
        if (!loop && sound.chance < 1f && Random.value > sound.chance) return null;
        AudioClip clip = sound.PickClip();
        if (clip == null) return null;
        sound.lastPlayed = now;

        // เกินจำนวนเสียงพร้อมกันของเหตุการณ์นี้ = ตัดเสียงที่เก่าสุด
        int same = 0;
        Voice oldest = null;
        foreach (Voice v in voices)
        {
            if (v.sound != sound || !v.Busy) continue;
            same++;
            if (oldest == null || v.started < oldest.started) oldest = v;
        }
        if (same >= Mathf.Max(1, sound.maxVoices) && oldest != null) Stop(oldest);

        Voice voice = GetVoice();
        if (voice == null) return null;
        AudioSource source = voice.source;
        source.clip = clip;
        source.loop = loop || sound.loop;
        source.pitch = Random.Range(sound.pitchRandom.x, sound.pitchRandom.y) * pitch;
        source.spatialBlend = position.HasValue ? sound.spatialBlend : 0f;
        source.transform.position = position ?? Vector3.zero;
        ClipRange range = sound.trimSilence && !(loop || sound.loop) ? Range(clip) : new ClipRange { start = 0f, end = clip.length };
        source.time = Mathf.Clamp(range.start + sound.startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));
        voice.end = loop || sound.loop ? float.MaxValue : range.end;
        voice.sound = sound;
        voice.baseVolume = sound.volume * Random.Range(sound.volumeRandom.x, sound.volumeRandom.y) * volume;
        voice.follow = follow;
        voice.started = now;
        voice.fade = voice.fadeTarget = 1f;
        voice.handle = nextHandle++;
        handle = voice.handle;
        source.volume = voice.baseVolume * CategoryVolume(sound.category);
        if (sound.delay > 0f)
        {
            voice.pending = true;
            voice.playAt = now + sound.delay;
        }
        else
        {
            voice.pending = false;
            source.Play();
        }
        return source;
    }

    // ช่วงที่มีเสียงจริงของไฟล์ (ตัดความเงียบหัวท้าย) คำนวณครั้งเดียวต่อไฟล์
    private struct ClipRange { public float start, end; }
    private readonly Dictionary<AudioClip, ClipRange> ranges = new Dictionary<AudioClip, ClipRange>();

    private ClipRange Range(AudioClip clip)
    {
        if (ranges.TryGetValue(clip, out ClipRange cached)) return cached;
        var range = new ClipRange { start = 0f, end = clip.length };
        try
        {
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            int channels = Mathf.Max(1, clip.channels);
            int frames = clip.samples;
            if (clip.loadState == AudioDataLoadState.Loaded && frames > 0 && frames < clip.frequency * 20)
            {
                var data = new float[frames * channels];
                if (clip.GetData(data, 0))
                {
                    float peak = 0f;
                    foreach (float s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
                    if (peak > 0.0001f)
                    {
                        float threshold = peak * 0.02f;
                        int first = 0, last = data.Length - 1;
                        while (first < data.Length && Mathf.Abs(data[first]) < threshold) first++;
                        while (last > first && Mathf.Abs(data[last]) < threshold) last--;
                        // เผื่อหัวไว้นิด (ไม่ตัดจังหวะเริ่มของเสียงหาย) ท้ายเผื่อหางเสียงจาง
                        range.start = Mathf.Max(0f, first / (float)channels / clip.frequency - 0.01f);
                        range.end = Mathf.Min(clip.length, last / (float)channels / clip.frequency + 0.15f);
                    }
                }
            }
        }
        catch (System.Exception) { }
        ranges[clip] = range;
        return range;
    }

    private Voice GetVoice()
    {
        foreach (Voice v in voices) if (!v.Busy) return v;
        if (voices.Count < maxVoices)
        {
            var go = new GameObject("Voice" + voices.Count);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 15f;
            source.maxDistance = 160f;
            source.dopplerLevel = 0f;
            var voice = new Voice { source = source };
            voices.Add(voice);
            return voice;
        }
        // เต็ม: ใช้ช่องที่เล่นนานสุด (ที่ไม่ใช่เสียงวน)
        Voice oldest = null;
        foreach (Voice v in voices)
            if (!v.source.loop && (oldest == null || v.started < oldest.started)) oldest = v;
        if (oldest != null) Stop(oldest);
        return oldest;
    }

    private static void Stop(Voice voice)
    {
        voice.pending = false;
        if (voice.source != null) voice.source.Stop();
        voice.follow = null;
        voice.handle = 0;
    }

    public static float CategoryVolume(SoundCategory category)
    {
        switch (category)
        {
            case SoundCategory.Music:
            case SoundCategory.Ambience:
                return GameSettings.MusicVolume;
            default:
                return GameSettings.EffectsVolume;
        }
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        foreach (Voice voice in voices)
        {
            if (voice.pending && now >= voice.playAt)
            {
                voice.pending = false;
                voice.source.Play();
            }
            if (!voice.Busy) continue;
            if (voice.follow != null) voice.source.transform.position = voice.follow.position;
            if (voice.source.time >= voice.end) { Stop(voice); continue; }
            if (voice.fade != voice.fadeTarget)
            {
                voice.fade = Mathf.MoveTowards(voice.fade, voice.fadeTarget, dt * voice.fadeSpeed);
                if (voice.fade <= 0f && voice.fadeTarget <= 0f) { Stop(voice); continue; }
            }
            voice.source.volume = voice.baseVolume * voice.fade * CategoryVolume(voice.sound != null ? voice.sound.category : SoundCategory.Effects);
        }
        UpdateChannel(music, dt);
        UpdateChannel(ambience, dt);

        if (now >= nextScan)
        {
            nextScan = now + 0.5f;
            ScanScene();
        }
    }

    // ปุ่มที่เปิดอยู่ทุกปุ่มได้เสียงชี้/กด (UIButtonSound) และตัวผู้เล่นในฉากปีนได้เสียงปีน/เดิน (PlayerMovementAudio)
    // สแกนทุกครึ่งวินาที (เวลาจริง) จึงรวมปุ่มที่สร้างขึ้นทีหลังด้วย (การ์ดสกิล วงล้ออาวุธ การ์ดเส้นทาง ฯลฯ)
    private void ScanScene()
    {
        foreach (UnityEngine.UI.Button button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            GameObject go = button.gameObject;
            if (go.GetComponent<UIButtonSound>() != null || UIButtonSound.HasOwnSound(go)) continue;
            go.AddComponent<UIButtonSound>();
        }
        foreach (PlayerStateManager player in FindObjectsByType<PlayerStateManager>(FindObjectsSortMode.None))
            if (player.GetComponent<PlayerMovementAudio>() == null) player.gameObject.AddComponent<PlayerMovementAudio>();
    }

    private void SetChannel(Channel channel, string id, float fade)
    {
        if (fade < 0f) fade = library != null ? library.musicFade : 1.5f;
        if (string.IsNullOrEmpty(id) || id == "-") id = null;
        if (channel.id == id) return;
        channel.id = id;
        SoundEvent sound = id != null && library != null ? library.Find(id) : null;
        AudioClip clip = sound != null ? sound.PickClip() : null;
        // มีเหตุการณ์แต่ยังไม่ใส่ไฟล์ (เช่นเพลงที่ยังไม่มี) = เงียบไปเฉย ๆ ไม่เตือน
        if (id != null && sound == null && warned.Add(id)) Debug.LogWarning($"SoundManager: ไม่มีเพลง/บรรยากาศ '{id}' ใน SoundLibrary");

        // ช่องใหม่ขึ้น ช่องเดิมจางออก
        int next = 1 - channel.active;
        AudioSource source = channel.sources[next];
        source.Stop();
        if (clip != null)
        {
            source.clip = clip;
            source.loop = true;
            source.pitch = 1f;
            source.time = 0f;
            source.Play();
            channel.baseVolumes[next] = sound.volume;
        }
        channel.weights[next] = fade <= 0f ? 1f : 0f;
        channel.active = next;
        channel.fadeSpeed = fade <= 0f ? 1000f : 1f / fade;
    }

    private void UpdateChannel(Channel channel, float dt)
    {
        for (int i = 0; i < 2; i++)
        {
            AudioSource source = channel.sources[i];
            float target = i == channel.active && source.clip != null && channel.id != null ? 1f : 0f;
            channel.weights[i] = Mathf.MoveTowards(channel.weights[i], target, dt * channel.fadeSpeed);
            if (channel.weights[i] <= 0f && target <= 0f && source.isPlaying) source.Stop();
            SoundCategory category = channel == music ? SoundCategory.Music : SoundCategory.Ambience;
            source.volume = channel.baseVolumes[i] * channel.weights[i] * CategoryVolume(category);
        }
    }

    private void OnActiveSceneChanged(Scene previous, Scene next) => ApplySceneAudio(next);
    // ฉากแรกของเกมโหลดหลัง SoundManager เกิด: ใช้เพลงของฉากที่ใช้งานอยู่ (ซ้ำกับเพลงเดิมจะไม่เริ่มใหม่)
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplySceneAudio(SceneManager.GetActiveScene());

    private void ApplySceneAudio(Scene scene)
    {
        if (library == null || !scene.IsValid()) return;
        SceneAudio audio = library.FindScene(scene.name);
        if (audio == null) return;
        if (!string.IsNullOrEmpty(audio.music)) PlayMusic(audio.music);
        if (!string.IsNullOrEmpty(audio.ambience)) PlayAmbience(audio.ambience);
    }
}
