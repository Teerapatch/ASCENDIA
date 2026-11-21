using UnityEngine;

namespace RoamMoon.SimpleDissolve
{
    public enum SimpleDissolveMode
    {
        Noise = 0,
        Burn = 1,
        Directional = 2,
        Radial = 3,
        Pixel = 4,
        Cells = 5,
    }

    public enum SimpleDissolveLighting
    {
        Unlit = 0,
        Lit2D = 1,
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Roam Moon/Simple Dissolve")]
    public class SimpleDissolveEffect : MonoBehaviour
    {
        public const string UnlitShaderName = "Roam Moon/SimpleDissolve";
        public const string Lit2DShaderName = "Roam Moon/SimpleDissolveLit";

        const string SpriteRectName = "_SpriteRect";
        const string SpriteSizeName = "_SpriteSize";
        const string BaseColorName = "_BaseColor";
        const string ModeName = "_Mode";
        const string AmountName = "_Amount";
        const string SoftnessName = "_Softness";
        const string NoiseScaleName = "_NoiseScale";
        const string DirectionName = "_Direction";
        const string CenterName = "_Center";
        const string RadiusName = "_Radius";
        const string EdgeWidthName = "_EdgeWidth";
        const string EdgeColorName = "_EdgeColor";
        const string EdgeIntensityName = "_EdgeIntensity";

        [SerializeField] Shader dissolveShader;
        [SerializeField, HideInInspector] Shader unlitShader;
        [SerializeField, HideInInspector] Shader lit2DShader;

        [SerializeField] SimpleDissolveMode mode = SimpleDissolveMode.Noise;

        [SerializeField] SimpleDissolveLighting lighting = SimpleDissolveLighting.Unlit;

        [Range(0f, 1f)]
        [SerializeField] float amount = 0f;

        [Range(0f, 1f)]
        [SerializeField] float softness = 0.08f;

        [Range(0.1f, 10f)]
        [SerializeField] float noiseScale = 1.5f;

        [SerializeField] Vector2 direction = Vector2.up;
        [SerializeField] Vector2 center = new Vector2(0.5f, 0.5f);

        [Range(0.01f, 2f)]
        [SerializeField] float radius = 0.75f;

        [Range(0f, 0.5f)]
        [SerializeField] float edgeWidth = 0.08f;

        [ColorUsage(true, true)]
        [SerializeField] Color edgeColor = new Color(2f, 2f, 2f, 1f);

        [Range(0f, 8f)]
        [SerializeField] float edgeIntensity = 2f;

        [ColorUsage(true, true)]
        [SerializeField] Color baseColor = Color.white;

        [SerializeField] bool playOnEnable = false;

        [Range(0.05f, 10f)]
        [SerializeField] float playSpeed = 1f;

        [SerializeField] bool pingPong = false;
        [SerializeField] bool loop = false;

        [SerializeField, HideInInspector] Material previousMaterial;
        [SerializeField, HideInInspector] bool materialSwapped;

        SpriteRenderer spriteRenderer;
        MaterialPropertyBlock propertyBlock;
        Sprite trackedSprite;
        Vector4 spriteRect = new Vector4(0f, 0f, 1f, 1f);
        Vector4 spriteSize = new Vector4(1f, 1f, 0f, 0f);
        Material ownMaterial;
        float playDirection = 1f;
        bool playing;
        bool pushedOnce;

        SimpleDissolveMode pushedMode;
        float pushedAmount;
        float pushedSoftness;
        float pushedNoiseScale;
        Vector2 pushedDirection;
        Vector2 pushedCenter;
        float pushedRadius;
        float pushedEdgeWidth;
        Color pushedEdgeColor;
        float pushedEdgeIntensity;
        Color pushedBaseColor;

        public SimpleDissolveLighting Lighting
        {
            get => lighting;
            set
            {
                lighting = value;

                if (isActiveAndEnabled)
                {
                    SwapMaterial();
                    PushProperties();
                }
            }
        }

        public SimpleDissolveMode Mode
        {
            get => mode;
            set { mode = value; PushProperties(); }
        }

        public float Amount
        {
            get => amount;
            set { amount = Mathf.Clamp01(value); PushProperties(); }
        }

        public float Softness
        {
            get => softness;
            set { softness = Mathf.Clamp01(value); PushProperties(); }
        }

        public float NoiseScale
        {
            get => noiseScale;
            set { noiseScale = Mathf.Clamp(value, 0.1f, 10f); PushProperties(); }
        }

        public Vector2 Direction
        {
            get => direction;
            set { direction = value; PushProperties(); }
        }

        public Vector2 Center
        {
            get => center;
            set { center = value; PushProperties(); }
        }

        public float Radius
        {
            get => radius;
            set { radius = Mathf.Clamp(value, 0.01f, 2f); PushProperties(); }
        }

        public float EdgeWidth
        {
            get => edgeWidth;
            set { edgeWidth = Mathf.Clamp(value, 0f, 0.5f); PushProperties(); }
        }

        public Color EdgeColor
        {
            get => edgeColor;
            set { edgeColor = value; PushProperties(); }
        }

        public float EdgeIntensity
        {
            get => edgeIntensity;
            set { edgeIntensity = Mathf.Clamp(value, 0f, 8f); PushProperties(); }
        }

        public Color BaseColor
        {
            get => baseColor;
            set { baseColor = value; PushProperties(); }
        }

        public float PlaySpeed
        {
            get => playSpeed;
            set => playSpeed = Mathf.Clamp(value, 0.05f, 10f);
        }

        public bool IsPlaying => playing;

        /// Sweeps Amount up to 1, dissolving the sprite away.
        public void Play()
        {
            playDirection = 1f;
            playing = true;
        }

        /// Sweeps Amount back down to 0, bringing the sprite back.
        public void PlayReverse()
        {
            playDirection = -1f;
            playing = true;
        }

        public void Stop()
        {
            playing = false;
        }

        public void Rewind()
        {
            playing = false;
            Amount = 0f;
        }

        void Reset()
        {
            dissolveShader = null;
            ApplyModePreset();
        }

        /// Pulls the tuned colour and sizing for the current mode.
        public void ApplyModePreset()
        {
            SimpleDissolvePreset preset = SimpleDissolvePresets.Get(mode);

            edgeColor = preset.EdgeColor;
            edgeWidth = preset.EdgeWidth;
            edgeIntensity = preset.EdgeIntensity;
            softness = preset.Softness;
            noiseScale = preset.NoiseScale;
            direction = preset.Direction;
            radius = preset.Radius;

            PushProperties();
        }

        public Shader ResolveShader()
        {
            bool overrideIsBuiltIn = dissolveShader != null
                && (dissolveShader.name == UnlitShaderName || dissolveShader.name == Lit2DShaderName);

            if (dissolveShader != null && !overrideIsBuiltIn)
            {
                return dissolveShader;
            }

            if (lighting == SimpleDissolveLighting.Lit2D)
            {
                Shader lit = lit2DShader != null ? lit2DShader : Shader.Find(Lit2DShaderName);

                if (lit != null)
                {
                    return lit;
                }
            }

            return unlitShader != null ? unlitShader : Shader.Find(UnlitShaderName);
        }

        void OnEnable()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (ResolveShader() == null)
            {
                Debug.LogWarning($"{nameof(SimpleDissolveEffect)}: shader not found. This package needs URP and Shader Graph.", this);
                enabled = false;
                return;
            }

            SwapMaterial();
            RefreshSpriteRect();
            PushProperties();

            if (playOnEnable && Application.isPlaying)
            {
                Play();
            }
        }

        void OnDisable()
        {
            RestoreMaterial();
        }

        void OnValidate()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            SwapMaterial();
            RefreshSpriteRect();
            PushProperties();
        }

        void Update()
        {
            if (playing)
            {
                Step(Application.isPlaying ? Time.deltaTime : 0.016f);
            }
        }

        void LateUpdate()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (!ReferenceEquals(spriteRenderer.sprite, trackedSprite))
            {
                RefreshSpriteRect();
                PushProperties();
            }
            else if (AnimatedValuesChanged())
            {
                PushProperties();
            }

#if UNITY_EDITOR
            if (playing && !Application.isPlaying)
            {
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }

        /// The animation system writes the serialized fields directly and never
        /// goes through a property setter, so Timeline and Animator changes are
        /// only visible if something compares the values each frame.
        bool AnimatedValuesChanged()
        {
            return !pushedOnce
                || mode != pushedMode
                || amount != pushedAmount
                || softness != pushedSoftness
                || noiseScale != pushedNoiseScale
                || direction != pushedDirection
                || center != pushedCenter
                || radius != pushedRadius
                || edgeWidth != pushedEdgeWidth
                || edgeColor != pushedEdgeColor
                || edgeIntensity != pushedEdgeIntensity
                || baseColor != pushedBaseColor;
        }

        void CaptureAnimatedValues()
        {
            pushedOnce = true;
            pushedMode = mode;
            pushedAmount = amount;
            pushedSoftness = softness;
            pushedNoiseScale = noiseScale;
            pushedDirection = direction;
            pushedCenter = center;
            pushedRadius = radius;
            pushedEdgeWidth = edgeWidth;
            pushedEdgeColor = edgeColor;
            pushedEdgeIntensity = edgeIntensity;
            pushedBaseColor = baseColor;
        }

        void Step(float deltaTime)
        {
            float next = amount + playDirection * playSpeed * deltaTime;

            if (next >= 1f)
            {
                if (pingPong)
                {
                    next = 1f;
                    playDirection = -1f;
                }
                else if (loop)
                {
                    next = 0f;
                }
                else
                {
                    next = 1f;
                    playing = false;
                }
            }
            else if (next <= 0f)
            {
                if (pingPong && loop)
                {
                    next = 0f;
                    playDirection = 1f;
                }
                else if (pingPong)
                {
                    next = 0f;
                    playing = false;
                }
                else if (loop)
                {
                    next = 1f;
                }
                else
                {
                    next = 0f;
                    playing = false;
                }
            }

            Amount = next;
        }

        void RefreshSpriteRect()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            Sprite sprite = spriteRenderer.sprite;
            trackedSprite = sprite;

            if (sprite == null || sprite.texture == null)
            {
                spriteRect = new Vector4(0f, 0f, 1f, 1f);
                spriteSize = new Vector4(1f, 1f, 0f, 0f);
                return;
            }

            Rect rect = sprite.textureRect;
            float w = sprite.texture.width;
            float h = sprite.texture.height;

            if (w <= 0f || h <= 0f)
            {
                spriteRect = new Vector4(0f, 0f, 1f, 1f);
                return;
            }

            spriteRect = new Vector4(rect.x / w, rect.y / h, rect.width / w, rect.height / h);
            spriteSize = new Vector4(rect.width, rect.height, 0f, 0f);
        }

        Material GetDissolveMaterial()
        {
            Shader shader = ResolveShader();

            if (shader == null)
            {
                return null;
            }

            if (ownMaterial == null || ownMaterial.shader != shader)
            {
                DestroyOwnMaterial();
                ownMaterial = new Material(shader)
                {
                    name = "SimpleDissolveEffect",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            return ownMaterial;
        }

        void DestroyOwnMaterial()
        {
            if (ownMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(ownMaterial);
            }
            else
            {
                DestroyImmediate(ownMaterial);
            }

            ownMaterial = null;
        }

        void SwapMaterial()
        {
            Material dissolveMaterial = GetDissolveMaterial();

            if (dissolveMaterial == null || spriteRenderer.sharedMaterial == dissolveMaterial)
            {
                return;
            }

            if (!materialSwapped)
            {
                previousMaterial = spriteRenderer.sharedMaterial;
                materialSwapped = true;
            }

            spriteRenderer.sharedMaterial = dissolveMaterial;
        }

        void RestoreMaterial()
        {
            if (!materialSwapped || spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sharedMaterial = previousMaterial;
            spriteRenderer.SetPropertyBlock(null);
            materialSwapped = false;
            DestroyOwnMaterial();
        }

        void PushProperties()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();

            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(SpriteRectName, spriteRect);
            propertyBlock.SetVector(SpriteSizeName, spriteSize);
            propertyBlock.SetColor(BaseColorName, baseColor);
            propertyBlock.SetFloat(ModeName, (float)(int)mode);
            propertyBlock.SetFloat(AmountName, amount);
            propertyBlock.SetFloat(SoftnessName, softness);
            propertyBlock.SetFloat(NoiseScaleName, noiseScale);
            propertyBlock.SetVector(DirectionName, direction);
            propertyBlock.SetVector(CenterName, center);
            propertyBlock.SetFloat(RadiusName, radius);
            propertyBlock.SetFloat(EdgeWidthName, edgeWidth);
            propertyBlock.SetColor(EdgeColorName, edgeColor);
            propertyBlock.SetFloat(EdgeIntensityName, edgeIntensity);

            spriteRenderer.SetPropertyBlock(propertyBlock);

            CaptureAnimatedValues();
        }
    }
}
