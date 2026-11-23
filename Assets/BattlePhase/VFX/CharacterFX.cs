using System;
using System.Collections.Generic;
using UnityEngine;

// เอฟเฟกต์ตัวละครตาย (สลายเป็นประกายและเถ้า) และเข้าฉาก (ก่อตัวขึ้นจากแสง) สร้างจากโค้ดทั้งหมด ไม่ต้องมี Prefab
//  - Dissolve: ท่าค้าง ตัวเรืองแสงสีประจำตัว แล้วประกาย + เถ้าหลุดออกจากผิวโมเดลไล่จากหัวลงเท้า ตัวยุบลงหาเท้าจนหาย
//  - Materialize: วงแหวนแสงบนพื้น + เสาแสง + ละอองดูดเข้าหาตัว แล้วโมเดลโตขึ้นจากเท้า (เด้งเกินนิดหน่อย) ตัวเรืองแล้วจางเป็นปกติ
// ปรับขนาดที่ Transform ของโมเดลเท่านั้น (จุดหมุนของโมเดลคือเท้า) ทำใน LateUpdate หลัง Animator จึงไม่ชนกับตำแหน่งที่สคริปต์อื่นคุม
// สร้างตัวเองในฉากต่อสู้ครั้งแรกที่ใช้ (วางคอมโพเนนต์นี้ในฉากเพื่อปรับค่า) ใช้เวลาจริง เล่นต่อแม้ Hitstop หยุดเมื่อเมนู Esc เปิด
public class CharacterFX : MonoBehaviour
{
    private static CharacterFX instance;
    public static CharacterFX Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<CharacterFX>();
            if (instance == null) instance = new GameObject("CharacterFX").AddComponent<CharacterFX>();
            return instance;
        }
    }

    [Header("สี")]
    public Color enemyColor = new Color(1f, 0.32f, 0.4f, 1f);
    public Color playerColor = new Color(0.45f, 0.75f, 1f, 1f);
    public Color ashColor = new Color(0.07f, 0.05f, 0.08f, 0.9f);
    [Tooltip("ความสว่างของตัวตอนเรืองแสง (คูณสี HDR ให้ Bloom จับ)")]
    public float bodyGlow = 2.6f;

    [Header("ตาย (สลาย)")]
    public float deathDuration = 1.9f;
    [Tooltip("ช่วงแรกที่ท่าค้างและตัวมืดลงก่อนเริ่มสลาย")]
    public float deathHold = 0.35f;
    public int deathPoints = 460;
    [Range(0f, 1f)] public float ashShare = 0.25f;

    [Header("ตาย: Shader สลายแบบ Genshin (Resources/CharacterDissolve)")]
    [Tooltip("ปิด = แบบเดิม (ตัวเรืองแล้วยุบลงหาเท้า)")]
    public bool useShaderDissolve = true;
    [Tooltip("เปิด = มีประกาย/เถ้าลอยออกและแสงวาบตอนสลายด้วย Shader (ปิด = Shader สลายอย่างเดียว)")]
    public bool deathParticles = false;
    [Tooltip("สีเงาที่ตัวมืดลงไปหา")]
    public Color darkColor = new Color(0.05f, 0.02f, 0.09f, 1f);
    [Range(0f, 1f)] public float darkenAmount = 0.85f;
    [Tooltip("ความสว่างของขอบรอยสลาย (คูณสีประจำตัว)")]
    public float edgeGlow = 4f;
    [Tooltip("ความสว่างของขอบตัว (Fresnel) ตอนมืดลง")]
    public float rimGlow = 2f;
    [Range(0.01f, 0.3f)] public float edgeWidth = 0.09f;
    [Tooltip("จำนวนก้อนรอยสลายตามความสูงตัว (มาก = รูเล็กละเอียด)")]
    public float noiseDetail = 5f;
    [Tooltip("0 = หายเป็นหย่อม ๆ ทั่วตัว, 1 = ไล่จากหัวลงเท้า")]
    [Range(0f, 1f)] public float topDownBias = 0.3f;
    [Tooltip("ขนาดประกาย (เท่าของความสูงตัว)")]
    public float emberSize = 0.05f;
    public float ashSize = 0.075f;
    [Tooltip("ความเร็วลอยขึ้น (เท่าของความสูงตัว ต่อวินาที)")]
    public float riseSpeed = 0.38f;
    public float scatterSpeed = 0.16f;
    public float particleLife = 1.4f;

    [Header("เข้าฉาก")]
    public float entranceDuration = 1f;
    [Tooltip("ระยะห่างการเกิดของศัตรูแต่ละตัวในชุดเดียวกัน")]
    public float entranceStagger = 0.18f;
    public float ringSize = 1.5f;
    public float pillarWidth = 0.42f;
    public float pillarHeight = 2.3f;
    public int moteCount = 46;
    [Tooltip("ขนาดตอนเด้งเกิน (1 = ไม่เด้ง)")]
    public float overshoot = 1.07f;

    public int sortingOrder = 32;

    // เวลารอทั้งหมดก่อนเริ่มเทิร์นแรก (ศัตรู count ตัวเกิดเหลื่อมกัน)
    public static float EntranceTime(int count) => Instance.entranceDuration + Mathf.Max(0, count - 1) * Instance.entranceStagger;
    public static float DeathTime => Instance.deathDuration;

    private abstract class Effect
    {
        public Transform model;
        public Vector3 baseScale;
        public float time;
        public float height;
        public Color color;
        public List<Renderer> renderers;
        public bool Alive => model != null;
        public abstract bool Tick(CharacterFX fx, float dt); // false = จบ
    }

    private readonly List<Effect> effects = new List<Effect>();
    // สร้างตอนใช้ครั้งแรก: new MaterialPropertyBlock() ใน field initializer ของ MonoBehaviour ได้ออบเจกต์ที่ไม่มีตัวจริงฝั่ง native (ArgumentNullException)
    private MaterialPropertyBlock block;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int Shade1Id = Shader.PropertyToID("_1st_ShadeColor");
    private static readonly int Shade2Id = Shader.PropertyToID("_2nd_ShadeColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    // ---------------------------------------------------------------- เรียกใช้
    // ตัวละครตาย: model = Transform ของโมเดล (จุดหมุนที่เท้า) height = ความสูงในโลก (0 = วัดจากขอบเขตโมเดล)
    public static void Dissolve(Transform model, Color color, float height = 0f, Action onDone = null)
    {
        if (model == null) return;
        CharacterFX fx = Instance;
        fx.Remove(model);
        var effect = new DissolveEffect { model = model, color = color, onDone = onDone };
        effect.Begin(fx, height);
        SoundManager.Play(Sfx.EnemyDeath);
        fx.effects.Add(effect);
    }

    // ตัวละครเข้าฉาก: ซ่อนตัวทันที (สเกลเล็กมาก) แล้วก่อตัวหลัง delay วินาที
    public static void Materialize(Transform model, Color color, float height = 0f, float delay = 0f, Action onDone = null)
    {
        if (model == null) return;
        CharacterFX fx = Instance;
        fx.Remove(model);
        // วัดความสูงตอนยังเต็มตัว ก่อนซ่อน
        if (height <= 0.01f) height = RendererBounds(CollectRenderers(model)).size.y;
        var effect = new MaterializeEffect { model = model, color = color, onDone = onDone, delay = delay, heightHint = height };
        effect.baseScale = model.localScale;
        model.localScale = effect.baseScale * 0.0001f; // ซ่อนตั้งแต่เฟรมนี้ ไม่ให้โผล่เต็มตัวก่อน
        fx.effects.Add(effect);
    }

    public static bool IsPlaying(Transform model)
    {
        if (instance == null || model == null) return false;
        foreach (Effect effect in instance.effects) if (effect.model == model) return true;
        return false;
    }

    // ยกเลิกเอฟเฟกต์เดิมของโมเดลนี้ (เช่นตายระหว่างกำลังเข้าฉาก) คืนสเกลเต็มก่อน
    private void Remove(Transform model)
    {
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].model != model) continue;
            if (effects[i] is MaterializeEffect) model.localScale = effects[i].baseScale;
            ClearTint(effects[i].renderers);
            effects.RemoveAt(i);
        }
    }

    private void LateUpdate()
    {
        if (GameExitMenu.IsOpen) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            Effect effect = effects[i];
            bool keep;
            try { keep = effect.Alive && effect.Tick(this, dt); }
            catch (Exception e) { Debug.LogException(e); keep = false; Restore(effect); }
            if (!keep) effects.RemoveAt(i);
        }
    }

    // เอฟเฟกต์พังกลางทาง: ตัวที่กำลังเข้าฉากต้องกลับมาเต็มตัว ไม่งั้นค้างที่สเกล 0.0001 (โมเดลหาย)
    private static void Restore(Effect effect)
    {
        try
        {
            if (effect is MaterializeEffect && effect.model != null) effect.model.localScale = effect.baseScale;
            ClearTint(effect.renderers);
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    // ---------------------------------------------------------------- ตาย
    // แบบ Genshin (มี Shader Resources/CharacterDissolve): ท่าค้าง ตัวมืดลงเป็นเงาขอบเรือง แล้วหายเป็นรูตาม Noise ขอบรอยสลายเรืองแสง
    // ประกาย/เถ้าหลุดออกจากจุดบนผิวตอนที่รอยสลายไปถึงจุดนั้น (คำนวณ Noise แบบเดียวกับ Shader บน CPU)
    // ไม่มี Shader: แบบเดิม ตัวเรืองแล้วยุบลงหาเท้าพร้อมประกายไล่จากหัวลงเท้า
    private class DissolveEffect : Effect
    {
        public Action onDone;
        private readonly List<Vector3> points = new List<Vector3>(); // ในพื้นที่ของโมเดล
        private readonly List<float> order = new List<float>();     // ค่าที่ใช้เรียง: ค่าสลาย (Shader) หรือความสูงกลับด้าน (แบบเดิม)
        private int next;
        private float topY, bottomY;
        private ParticleSystem embers, ash;
        private readonly List<Animator> animators = new List<Animator>();
        private readonly List<float> animatorSpeeds = new List<float>();
        private readonly List<Material> dissolveMaterials = new List<Material>();
        private bool finished;

        public void Begin(CharacterFX fx, float heightHint)
        {
            baseScale = model.localScale;
            renderers = CollectRenderers(model);
            Bounds bounds = SampleSurface(model, renderers, fx.deathPoints, points);
            height = heightHint > 0.01f ? heightHint : Mathf.Max(0.5f, bounds.size.y);
            model.GetComponentsInChildren(animators);
            foreach (Animator animator in animators) animatorSpeeds.Add(animator.speed);

            Shader shader = fx.useShaderDissolve ? DissolveShader : null;
            if (shader != null && renderers.Count > 0)
            {
                float minY = bounds.min.y, maxY = bounds.max.y;
                float noiseScale = fx.noiseDetail / Mathf.Max(0.1f, height);
                foreach (Renderer renderer in renderers) SwapToDissolve(fx, renderer, shader, noiseScale, minY, maxY);
                foreach (Vector3 local in points) order.Add(DissolveValue(model.TransformPoint(local), noiseScale, fx.topDownBias, minY, maxY));
            }
            else
            {
                foreach (Vector3 local in points) order.Add(-local.y);
            }
            // เรียงจุดตามลำดับที่จะหาย
            var index = new List<int>(points.Count);
            for (int i = 0; i < points.Count; i++) index.Add(i);
            index.Sort((a, b) => order[a].CompareTo(order[b]));
            var sortedPoints = new List<Vector3>(points.Count);
            var sortedOrder = new List<float>(points.Count);
            foreach (int i in index) { sortedPoints.Add(points[i]); sortedOrder.Add(order[i]); }
            points.Clear(); points.AddRange(sortedPoints);
            order.Clear(); order.AddRange(sortedOrder);
            topY = points.Count > 0 ? -order[0] : 1f;
            bottomY = points.Count > 0 ? -order[order.Count - 1] : 0f;

            // สลายด้วย Shader: ไม่มี Particle (ประกาย / เถ้า / แสงวาบ) ตามค่า deathParticles
            if (dissolveMaterials.Count > 0 && !fx.deathParticles) return;

            Vector3 center = bounds.size.sqrMagnitude > 0f ? bounds.center : model.position + Vector3.up * height * 0.5f;
            BattleProjectiles shots = BattleProjectiles.Instance;
            shots.Flash(center, color, height * 0.8f, 0.25f);
            if (dissolveMaterials.Count == 0) shots.Ring(center, color, height * 1.1f, 0.35f);

            float life = fx.deathDuration + fx.particleLife + 0.2f;
            embers = fx.MakeDrift("DeathEmbers", model.position, BattleProjectiles.Additive(BattleProjectiles.Dot), life, height);
            ash = fx.MakeDrift("DeathAsh", model.position, AshMaterial, life, height);
        }

        // สลับวัสดุของ Renderer เป็นตัวสลาย คัดลอกสี/ภาพ/แสงเรืองจากวัสดุเดิม (Toon, URP Lit หรืออื่น ๆ)
        private void SwapToDissolve(CharacterFX fx, Renderer renderer, Shader shader, float noiseScale, float minY, float maxY)
        {
            Material[] source = renderer.sharedMaterials;
            var swapped = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Material original = source[i];
                var material = new Material(shader) { name = (original != null ? original.name : "Mat") + "_Dissolve", hideFlags = HideFlags.DontSave };
                if (original != null)
                {
                    Color baseColor = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
                    material.SetColor("_BaseColor", baseColor);
                    string mapName = original.HasProperty("_MainTex") && original.GetTexture("_MainTex") != null ? "_MainTex" : original.HasProperty("_BaseMap") ? "_BaseMap" : null;
                    if (mapName != null && original.GetTexture(mapName) != null)
                    {
                        material.SetTexture("_BaseMap", original.GetTexture(mapName));
                        material.SetTextureScale("_BaseMap", original.GetTextureScale(mapName));
                        material.SetTextureOffset("_BaseMap", original.GetTextureOffset(mapName));
                    }
                    material.SetColor("_ShadeColor", original.HasProperty("_1st_ShadeColor") ? original.GetColor("_1st_ShadeColor") : baseColor * 0.65f);
                    if (original.HasProperty("_Emissive_Color"))
                    {
                        material.SetColor("_EmissiveColor", original.GetColor("_Emissive_Color"));
                        if (original.HasProperty("_Emissive_Tex") && original.GetTexture("_Emissive_Tex") != null) material.SetTexture("_EmissiveMap", original.GetTexture("_Emissive_Tex"));
                    }
                    else if (original.HasProperty("_EmissionColor") && original.IsKeywordEnabled("_EMISSION"))
                    {
                        material.SetColor("_EmissiveColor", original.GetColor("_EmissionColor"));
                        if (original.HasProperty("_EmissionMap") && original.GetTexture("_EmissionMap") != null) material.SetTexture("_EmissiveMap", original.GetTexture("_EmissionMap"));
                    }
                    else material.SetColor("_EmissiveColor", Color.black);
                }
                Color edge = Color.Lerp(color, Color.white, 0.35f) * fx.edgeGlow;
                material.SetColor("_EdgeColor", new Color(edge.r, edge.g, edge.b, 1f));
                Color rim = color * fx.rimGlow;
                material.SetColor("_RimColor", new Color(rim.r, rim.g, rim.b, 1f));
                material.SetColor("_DarkColor", fx.darkColor);
                material.SetFloat("_EdgeWidth", fx.edgeWidth);
                material.SetFloat("_NoiseScale", noiseScale);
                material.SetFloat("_Direction", fx.topDownBias);
                material.SetVector("_BoundsY", new Vector4(minY, maxY, 0f, 0f));
                material.SetFloat("_Dissolve", 0f);
                material.SetFloat("_Darken", 0f);
                swapped[i] = material;
                dissolveMaterials.Add(material);
            }
            renderer.sharedMaterials = swapped;
        }

        public override bool Tick(CharacterFX fx, float dt)
        {
            time += dt;
            bool shaderMode = dissolveMaterials.Count > 0;
            float hold = Mathf.Min(fx.deathHold, fx.deathDuration * 0.5f);

            // ท่าช้าลงจนค้าง
            float freeze = Mathf.Clamp01(time / Mathf.Max(0.01f, hold));
            for (int i = 0; i < animators.Count; i++) if (animators[i] != null) animators[i].speed = animatorSpeeds[i] * (1f - freeze);

            float p = Mathf.Clamp01((time - hold) / Mathf.Max(0.01f, fx.deathDuration - hold));
            float cut;
            if (shaderMode)
            {
                // มืดลงเป็นเงาตอนค้าง แล้วหายเป็นรู (เริ่มช้า เร่งกลาง จบนุ่ม)
                float darken = Mathf.SmoothStep(0f, fx.darkenAmount, freeze);
                cut = Mathf.Lerp(0f, 1.02f, p * p * (3f - 2f * p));
                foreach (Material material in dissolveMaterials)
                {
                    if (material == null) continue;
                    material.SetFloat("_Darken", darken);
                    material.SetFloat("_Dissolve", cut);
                }
            }
            else
            {
                fx.Tint(renderers, color, Mathf.SmoothStep(0f, 1f, freeze));
                // ยุบลงหาเท้า: ช่วงแรกยังเห็นทั้งตัว แล้วหายเร็วตอนท้าย
                float shrink = 1f - p * p * (3f - 2f * p) * 0.35f - Mathf.Pow(p, 3f) * 0.65f;
                model.localScale = baseScale * Mathf.Max(0.0001f, shrink);
                cut = -Mathf.Lerp(topY, bottomY - 0.01f, p);
            }

            // ปล่อยประกาย/เถ้าจากจุดที่รอยสลาย (หรือเส้นกวาด) ไปถึงแล้ว
            if (p > 0f)
            {
                while (next < points.Count && order[next] <= cut)
                {
                    fx.EmitBit(embers, ash, model.TransformPoint(points[next]), model.position, height, color);
                    next++;
                }
            }

            if (p < 1f) return true;
            if (!finished)
            {
                finished = true;
                foreach (Renderer renderer in renderers) if (renderer != null) renderer.enabled = false;
                foreach (Material material in dissolveMaterials) if (material != null) Destroy(material);
                dissolveMaterials.Clear();
                if (!shaderMode)
                {
                    Vector3 feet = model.position + Vector3.up * height * 0.15f;
                    BattleProjectiles.Instance.Flash(feet, color, height * 0.6f, 0.3f);
                    BattleProjectiles.Instance.Sparks(feet, Vector3.up, color, 18, height * 0.4f, height * 1.2f, height * 0.04f, 0.6f, 70f);
                }
                onDone?.Invoke();
            }
            return false;
        }
    }

    private static Shader dissolveShader;
    private static bool dissolveShaderLoaded;
    private static Shader DissolveShader
    {
        get
        {
            if (!dissolveShaderLoaded)
            {
                dissolveShaderLoaded = true;
                dissolveShader = Resources.Load<Shader>("CharacterDissolve");
                if (dissolveShader != null && !dissolveShader.isSupported) dissolveShader = null;
                if (dissolveShader == null) Debug.LogWarning("CharacterFX: ไม่พบ Shader Resources/CharacterDissolve ใช้แบบยุบตัวแทน");
            }
            return dissolveShader;
        }
    }

    // Noise แบบเดียวกับ CharacterDissolve.shader (DissolveValue) ค่าน้อยหายก่อน
    public static float DissolveValue(Vector3 positionWS, float noiseScale, float direction, float minY, float maxY)
    {
        Vector3 q = positionWS * noiseScale;
        float n = ValueNoise(q) * 0.55f + ValueNoise(q * 2.03f + Vector3.one * 17.1f) * 0.3f + ValueNoise(q * 4.11f + Vector3.one * 31.7f) * 0.15f;
        n = Mathf.Clamp01((n - 0.2f) / 0.6f);
        float h = Mathf.Clamp01((positionWS.y - minY) / Mathf.Max(0.001f, maxY - minY));
        return Mathf.Lerp(n, 1f - h, direction);
    }

    private static float Frac(float x) => x - Mathf.Floor(x);

    private static float Hash31(Vector3 p)
    {
        p = new Vector3(Frac(p.x * 0.3183099f + 0.1f), Frac(p.y * 0.3183099f + 0.1f), Frac(p.z * 0.3183099f + 0.1f)) * 17f;
        return Frac(p.x * p.y * p.z * (p.x + p.y + p.z));
    }

    private static float ValueNoise(Vector3 x)
    {
        Vector3 i = new Vector3(Mathf.Floor(x.x), Mathf.Floor(x.y), Mathf.Floor(x.z));
        Vector3 f = x - i;
        f = new Vector3(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y), f.z * f.z * (3f - 2f * f.z));
        float a = Mathf.Lerp(Hash31(i), Hash31(i + new Vector3(1, 0, 0)), f.x);
        float b = Mathf.Lerp(Hash31(i + new Vector3(0, 1, 0)), Hash31(i + new Vector3(1, 1, 0)), f.x);
        float c = Mathf.Lerp(Hash31(i + new Vector3(0, 0, 1)), Hash31(i + new Vector3(1, 0, 1)), f.x);
        float d = Mathf.Lerp(Hash31(i + new Vector3(0, 1, 1)), Hash31(i + new Vector3(1, 1, 1)), f.x);
        return Mathf.Lerp(Mathf.Lerp(a, b, f.y), Mathf.Lerp(c, d, f.y), f.z);
    }

    // ---------------------------------------------------------------- เข้าฉาก
    private class MaterializeEffect : Effect
    {
        public Action onDone;
        public float delay, heightHint;
        private bool started, flashed;
        private Vector3 feet;

        private void Start(CharacterFX fx)
        {
            started = true;
            SoundManager.Play(Sfx.EnemySpawn);
            renderers = CollectRenderers(model);
            // วัดความสูงที่สเกลเต็ม (ตอนนี้ตัวถูกย่อไว้)
            if (heightHint > 0.01f) height = heightHint;
            else
            {
                Vector3 small = model.localScale;
                model.localScale = baseScale;
                Bounds bounds = RendererBounds(renderers);
                model.localScale = small;
                height = Mathf.Max(0.5f, bounds.size.y);
            }
            feet = model.position;
            fx.Tint(renderers, color, 1f);

            BattleProjectiles shots = BattleProjectiles.Instance;
            fx.GroundRing(feet + Vector3.up * 0.05f, color, height * fx.ringSize, 0.75f);
            fx.GroundRing(feet + Vector3.up * 0.05f, Color.Lerp(color, Color.white, 0.5f), height * fx.ringSize * 0.6f, 0.5f);
            fx.Pillar(feet + Vector3.up * height * fx.pillarHeight * 0.45f, color, height * fx.pillarWidth, height * fx.pillarHeight, fx.entranceDuration * 0.75f);
            fx.Motes(feet, color, height, fx.moteCount, fx.entranceDuration * 0.4f);
            shots.Flash(feet + Vector3.up * height * 0.1f, color, height * 0.7f, 0.3f);
        }

        public override bool Tick(CharacterFX fx, float dt)
        {
            if (delay > 0f)
            {
                delay -= dt;
                model.localScale = baseScale * 0.0001f;
                return true;
            }
            if (!started) Start(fx);

            time += dt;
            float t = Mathf.Clamp01(time / Mathf.Max(0.05f, fx.entranceDuration));

            // โตจากเท้าช่วง 25%..80% แบบเด้งเกินนิดหน่อย
            float g = Mathf.Clamp01((t - 0.25f) / 0.55f);
            float scale = g <= 0f ? 0.0001f : EaseOutBack(g, fx.overshoot);
            model.localScale = baseScale * Mathf.Max(0.0001f, scale);

            // เรืองเต็มที่ตอนก่อตัว แล้วจางคืนสีเดิม
            float glow = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.55f) / 0.45f));
            fx.Tint(renderers, color, glow);

            if (!flashed && g >= 0.75f)
            {
                flashed = true;
                Vector3 center = feet + Vector3.up * height * 0.5f;
                BattleProjectiles.Instance.Flash(center, color, height * 1.1f, 0.35f);
                BattleProjectiles.Instance.Sparks(center, Vector3.up, color, 16, height * 0.4f, height * 1.1f, height * 0.035f, 0.5f, 120f);
            }

            if (t < 1f) return true;
            model.localScale = baseScale;
            ClearTint(renderers);
            onDone?.Invoke();
            return false;
        }

        private static float EaseOutBack(float x, float peak)
        {
            // c ปรับให้ค่าสูงสุดราว ๆ peak
            float c = Mathf.Max(0f, (peak - 1f) * 14f);
            float a = x - 1f;
            return 1f + (c + 1f) * a * a * a + c * a * a;
        }
    }

    // ---------------------------------------------------------------- ชิ้นส่วน
    private static List<Renderer> CollectRenderers(Transform model)
    {
        var list = new List<Renderer>();
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer || renderer is SpriteRenderer) continue;
            list.Add(renderer);
        }
        return list;
    }

    private static Bounds RendererBounds(List<Renderer> renderers)
    {
        bool any = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            if (!any) { bounds = renderer.bounds; any = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    // สุ่มจุดบนผิวโมเดล (ท่าปัจจุบัน) เก็บในพื้นที่ของโมเดล
    private static Mesh bake;
    private static Bounds SampleSurface(Transform model, List<Renderer> renderers, int count, List<Vector3> result)
    {
        var all = new List<Vector3>();
        var vertices = new List<Vector3>();
        if (bake == null) bake = new Mesh { hideFlags = HideFlags.DontSave };
        foreach (Renderer renderer in renderers)
        {
            vertices.Clear();
            Matrix4x4 toWorld;
            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                // BakeMesh แบบไม่ใส่สเกล ได้ขนาดในโลกแล้ว (ดู CLAUDE.md เรื่อง BakeMesh) แปลงด้วยตำแหน่ง/การหมุนอย่างเดียว
                skinned.BakeMesh(bake);
                bake.GetVertices(vertices);
                toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
            }
            else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null && filter.sharedMesh.isReadable)
            {
                filter.sharedMesh.GetVertices(vertices);
                toWorld = renderer.transform.localToWorldMatrix;
            }
            else continue;
            int step = Mathf.Max(1, vertices.Count / 4000);
            for (int i = 0; i < vertices.Count; i += step) all.Add(toWorld.MultiplyPoint3x4(vertices[i]));
        }

        Bounds bounds = RendererBounds(renderers);
        if (all.Count == 0)
        {
            // อ่าน Mesh ไม่ได้: สุ่มในกล่องขอบเขตแทน
            for (int i = 0; i < count; i++)
                all.Add(bounds.center + Vector3.Scale(bounds.extents, new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-0.6f, 0.6f))));
        }
        for (int i = 0; i < count; i++) result.Add(model.InverseTransformPoint(all[UnityEngine.Random.Range(0, all.Count)]));
        return bounds;
    }

    // ระบบอนุภาคที่ลอยขึ้นแบบมีลมปั่น (ปล่อยทีละเม็ดด้วย Emit)
    private ParticleSystem MakeDrift(string name, Vector3 position, Material material, float life, float height)
    {
        ParticleSystem ps = BattleProjectiles.Instance.MakeSystem(name, position, material, life);
        var main = ps.main;
        main.maxParticles = 1200;
        main.startLifetime = particleLife;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = height * 0.12f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.6f;
        noise.damping = false;
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)));
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = gradient;
        ps.GetComponent<ParticleSystemRenderer>().sortingOrder = sortingOrder;
        ps.Play();
        return ps;
    }

    private void EmitBit(ParticleSystem embers, ParticleSystem ash, Vector3 position, Vector3 axis, float height, Color color)
    {
        Vector3 outward = position - axis;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : UnityEngine.Random.insideUnitSphere;
        Vector3 velocity = Vector3.up * height * riseSpeed * UnityEngine.Random.Range(0.5f, 1.4f)
                         + (outward + UnityEngine.Random.insideUnitSphere * 0.8f) * height * scatterSpeed;
        bool isAsh = UnityEngine.Random.value < ashShare;
        var emit = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startLifetime = particleLife * UnityEngine.Random.Range(0.55f, 1.1f),
            applyShapeToPosition = false,
        };
        if (isAsh)
        {
            if (ash == null) return;
            emit.startSize = height * ashSize * UnityEngine.Random.Range(0.6f, 1.3f);
            emit.startColor = ashColor;
            ash.Emit(emit, 1);
        }
        else
        {
            if (embers == null) return;
            emit.startSize = height * emberSize * UnityEngine.Random.Range(0.5f, 1.3f);
            emit.startColor = UnityEngine.Random.value < 0.25f ? Color.Lerp(color, Color.white, 0.7f) : color;
            embers.Emit(emit, 1);
        }
    }

    // วงแหวนนอนราบบนพื้น ขยายออกแล้วจาง
    private void GroundRing(Vector3 position, Color color, float size, float life)
    {
        ParticleSystem ps = BattleProjectiles.Instance.MakeSystem("SpawnRing", position, BattleProjectiles.Additive(BattleProjectiles.RingTexture), life);
        ps.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var main = ps.main;
        main.startSize = size;
        main.startColor = color;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.15f, 0f, 2.5f), new Keyframe(1f, 1f, 0f, 0f)));
        BattleProjectiles.FadeOut(ps, color);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.sortingOrder = sortingOrder;
        ps.Play();
        ps.Emit(1);
    }

    // เสาแสงแนวตั้ง (หันเข้ากล้อง) บางลงจนหาย
    private void Pillar(Vector3 center, Color color, float width, float tall, float life)
    {
        ParticleSystem ps = BattleProjectiles.Instance.MakeSystem("SpawnPillar", center, BattleProjectiles.Additive(PillarTexture), life);
        var main = ps.main;
        main.startSize3D = true;
        main.startSizeX = width;
        main.startSizeY = tall;
        main.startSizeZ = 1f;
        main.startColor = color;
        main.startSpeed = 0f;
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.separateAxes = true;
        sizeOverLife.x = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.1f)));
        sizeOverLife.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.1f)));
        sizeOverLife.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
        BattleProjectiles.FadeOut(ps, color);
        ps.GetComponent<ParticleSystemRenderer>().sortingOrder = sortingOrder;
        ps.Play();
        ps.Emit(1);
        // แกนสว่างตรงกลาง
        BattleProjectiles.Instance.Flash(center - Vector3.up * tall * 0.3f, Color.Lerp(color, Color.white, 0.6f), width * 1.4f, life * 0.6f);
    }

    // ละอองรอบตัวพุ่งเข้าหาแกนกลางแล้วขึ้นบน
    private void Motes(Vector3 feet, Color color, float height, int count, float life)
    {
        ParticleSystem ps = BattleProjectiles.Instance.MakeSystem("SpawnMotes", feet, BattleProjectiles.Additive(BattleProjectiles.Dot), life + 0.1f);
        var main = ps.main;
        main.maxParticles = count + 8;
        main.startSpeed = 0f;
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f)));
        BattleProjectiles.FadeOut(ps, color);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = sortingOrder;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.05f;
        renderer.lengthScale = 1.5f;
        ps.Play();
        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float radius = height * UnityEngine.Random.Range(0.45f, 0.8f);
            float y = height * UnityEngine.Random.Range(0f, 0.9f);
            Vector3 from = feet + new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
            Vector3 to = feet + Vector3.up * (y * 0.6f + height * 0.35f);
            float moteLife = life * UnityEngine.Random.Range(0.7f, 1f);
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = from,
                velocity = (to - from) / moteLife,
                startLifetime = moteLife,
                startSize = height * UnityEngine.Random.Range(0.03f, 0.06f),
                startColor = UnityEngine.Random.value < 0.3f ? Color.Lerp(color, Color.white, 0.6f) : color,
                applyShapeToPosition = false,
            }, 1);
        }
    }

    // ตัวเรืองแสงสีประจำตัว (amount 0..1) ผ่าน MaterialPropertyBlock ไม่แตะวัสดุจริง
    private void Tint(List<Renderer> renderers, Color color, float amount)
    {
        if (renderers == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        Color glow = new Color(color.r * bodyGlow, color.g * bodyGlow, color.b * bodyGlow, 1f);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) continue;
                renderer.GetPropertyBlock(block, i);
                bool any = false;
                any |= Blend(material, BaseColorId, glow, amount);
                any |= Blend(material, Shade1Id, glow, amount);
                any |= Blend(material, Shade2Id, glow, amount);
                if (!material.HasProperty(BaseColorId)) any |= Blend(material, ColorId, glow, amount);
                if (any) renderer.SetPropertyBlock(block, i);
            }
        }
    }

    private bool Blend(Material material, int id, Color glow, float amount)
    {
        if (!material.HasProperty(id)) return false;
        Color original = material.GetColor(id);
        Color target = new Color(glow.r, glow.g, glow.b, original.a);
        block.SetColor(id, Color.Lerp(original, target, amount));
        return true;
    }

    private static MaterialPropertyBlock empty;
    private static void ClearTint(List<Renderer> renderers)
    {
        if (renderers == null) return;
        if (empty == null) empty = new MaterialPropertyBlock();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            int count = renderer.sharedMaterials.Length;
            for (int i = 0; i < count; i++) renderer.SetPropertyBlock(empty, i);
        }
    }

    // ---------------------------------------------------------------- วัสดุ / ภาพ
    private static Material ashMaterial;
    private static Material AshMaterial
    {
        get
        {
            if (ashMaterial == null) ashMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = BattleProjectiles.Dot, renderQueue = 3040, hideFlags = HideFlags.DontSave };
            return ashMaterial;
        }
    }

    // เสาแสง: นุ่มด้านข้าง จางที่ปลายบนล่าง
    private static Texture2D pillarTexture;
    private static Texture2D PillarTexture
    {
        get
        {
            if (pillarTexture != null) return pillarTexture;
            const int w = 64, h = 128;
            pillarTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float across = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2f);
                    float along = Mathf.Sin(v * Mathf.PI);
                    float a = across * across * Mathf.Pow(along, 0.6f) * Mathf.Lerp(1f, 0.55f, v);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            pillarTexture.SetPixels(pixels);
            pillarTexture.Apply();
            return pillarTexture;
        }
    }
}
