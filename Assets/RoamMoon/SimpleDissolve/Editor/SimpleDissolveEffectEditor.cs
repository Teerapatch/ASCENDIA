using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoamMoon.SimpleDissolve.Editor
{
    [CustomEditor(typeof(SimpleDissolveEffect))]
    [CanEditMultipleObjects]
    public class SimpleDissolveEffectEditor : UnityEditor.Editor
    {
        const string LogoFallbackFilter = "logorm t:Texture2D";

        static readonly HashSet<SimpleDissolveMode> UsesDirection = new HashSet<SimpleDissolveMode>
        {
            SimpleDissolveMode.Directional,
        };

        static readonly HashSet<SimpleDissolveMode> UsesCenter = new HashSet<SimpleDissolveMode>
        {
            SimpleDissolveMode.Radial,
        };

        static bool Dark => EditorGUIUtility.isProSkin;

        static Color HeaderBackground => Dark ? new Color(0.16f, 0.17f, 0.21f) : new Color(0.85f, 0.86f, 0.90f);
        static Color CardBackground => Dark ? new Color(0.235f, 0.235f, 0.255f) : new Color(0.925f, 0.925f, 0.935f);
        static Color OutlineBorder => Dark ? new Color(0.32f, 0.33f, 0.38f) : new Color(0.74f, 0.75f, 0.79f);
        static Color Accent => Dark ? new Color(0.63f, 0.69f, 0.93f) : new Color(0.33f, 0.40f, 0.72f);
        static Color MutedText => Dark ? new Color(0.60f, 0.62f, 0.68f) : new Color(0.36f, 0.38f, 0.44f);
        static Color TitleText => Dark ? new Color(0.90f, 0.91f, 0.94f) : new Color(0.12f, 0.13f, 0.16f);

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            root.Add(BuildBanner());
            root.Add(BuildShaderWarning());
            root.Add(BuildDissolveCard());
            root.Add(BuildShapeCard());
            root.Add(BuildCard("Edge", "edgeWidth", "edgeColor", "edgeIntensity"));
            root.Add(BuildAnimationCard());
            root.Add(BuildCard("Base", "baseColor"));
            root.Add(BuildAdvanced());

            return root;
        }

        VisualElement BuildDissolveCard()
        {
            VisualElement card = CreateCard("Dissolve");

            PropertyField modeField = CreateField("mode");
            card.Add(modeField);
            card.Add(CreateField("lighting"));
            card.Add(CreateField("amount"));
            card.Add(CreateField("softness"));
            card.Add(CreateField("noiseScale"));

            SerializedProperty modeProperty = serializedObject.FindProperty("mode");

            modeField.TrackPropertyValue(modeProperty, _ =>
            {
                serializedObject.ApplyModifiedProperties();

                foreach (Object target in targets)
                {
                    if (target is SimpleDissolveEffect effect)
                    {
                        Undo.RecordObject(effect, "Apply Dissolve Preset");
                        effect.ApplyModePreset();
                        EditorUtility.SetDirty(effect);
                    }
                }

                serializedObject.Update();
            });

            return card;
        }

        VisualElement BuildShapeCard()
        {
            VisualElement card = CreateCard("Shape");

            PropertyField directionField = CreateField("direction");
            PropertyField centerField = CreateField("center");
            PropertyField radiusField = CreateField("radius");

            card.Add(directionField);
            card.Add(centerField);
            card.Add(radiusField);

            var hint = new Label("Direction drives the Directional sweep. Center and Radius drive Radial.");
            hint.style.fontSize = 10f;
            hint.style.color = MutedText;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 3f;
            card.Add(hint);

            SerializedProperty modeProperty = serializedObject.FindProperty("mode");

            void Sync()
            {
                var current = (SimpleDissolveMode)modeProperty.enumValueIndex;
                Dim(directionField, UsesDirection.Contains(current));
                Dim(centerField, UsesCenter.Contains(current));
                Dim(radiusField, UsesCenter.Contains(current));
            }

            card.TrackPropertyValue(modeProperty, _ => Sync());
            Sync();

            return card;
        }

        static void Dim(VisualElement element, bool used)
        {
            element.SetEnabled(used);
            element.style.opacity = used ? 1f : 0.55f;
        }

        VisualElement BuildAnimationCard()
        {
            VisualElement card = CreateCard("Animation");

            card.Add(CreateField("playOnEnable"));
            card.Add(CreateField("playSpeed"));
            card.Add(CreateField("pingPong"));
            card.Add(CreateField("loop"));

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 6f;

            row.Add(ActionButton("Dissolve", e => e.Play()));
            row.Add(ActionButton("Restore", e => e.PlayReverse()));
            row.Add(ActionButton("Rewind", e => e.Rewind()));

            card.Add(row);

            return card;
        }

        Button ActionButton(string label, System.Action<SimpleDissolveEffect> action)
        {
            var button = new Button(() =>
            {
                foreach (Object target in targets)
                {
                    if (target is SimpleDissolveEffect effect)
                    {
                        Undo.RecordObject(effect, label);
                        action(effect);
                        EditorUtility.SetDirty(effect);
                    }
                }
            })
            {
                text = label
            };

            button.style.flexGrow = 1f;
            button.style.marginLeft = 1f;
            button.style.marginRight = 1f;
            return button;
        }

        VisualElement BuildBanner()
        {
            var banner = new VisualElement();
            banner.style.flexDirection = FlexDirection.Row;
            banner.style.alignItems = Align.Center;
            banner.style.backgroundColor = HeaderBackground;
            banner.style.marginBottom = 10f;
            SetPadding(banner, 10f, 12f);
            SetRadius(banner, 6f);
            SetBorder(banner, OutlineBorder, 1f);

            Texture2D logo = LoadLogo();

            if (logo != null)
            {
                var badge = new Image { image = logo, scaleMode = ScaleMode.ScaleToFit };
                badge.style.width = 40f;
                badge.style.height = 40f;
                badge.style.marginRight = 10f;
                banner.Add(badge);
            }

            var titles = new VisualElement();
            titles.style.flexGrow = 1f;
            titles.style.justifyContent = Justify.Center;

            var title = new Label("Simple Dissolve");
            title.style.fontSize = 14f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = TitleText;
            titles.Add(title);

            var subtitle = new Label("Roam Moon");
            subtitle.style.fontSize = 10f;
            subtitle.style.letterSpacing = 1.4f;
            subtitle.style.marginTop = 2f;
            subtitle.style.color = MutedText;
            titles.Add(subtitle);

            banner.Add(titles);

            return banner;
        }

        VisualElement BuildShaderWarning()
        {
            bool unlitFound = Shader.Find(SimpleDissolveEffect.UnlitShaderName) != null;
            bool litFound = Shader.Find(SimpleDissolveEffect.Lit2DShaderName) != null;

            var box = new HelpBox(
                unlitFound
                    ? $"{SimpleDissolveEffect.Lit2DShaderName} was not found, so Lit 2D falls back to Unlit."
                    : "The dissolve shader was not found. This package needs URP and the Shader Graph package.",
                HelpBoxMessageType.Warning);

            box.style.marginBottom = 8f;
            box.style.display = unlitFound && litFound ? DisplayStyle.None : DisplayStyle.Flex;

            return box;
        }

        VisualElement BuildCard(string title, params string[] propertyNames)
        {
            VisualElement card = CreateCard(title);

            foreach (string propertyName in propertyNames)
            {
                card.Add(CreateField(propertyName));
            }

            return card;
        }

        VisualElement BuildAdvanced()
        {
            var foldout = new Foldout { text = "Advanced", value = false };
            foldout.style.marginTop = 2f;
            foldout.style.marginLeft = 2f;
            foldout.Add(CreateField("dissolveShader"));

            var hint = new Label("Leave empty to use the shipped shader.");
            hint.style.fontSize = 10f;
            hint.style.color = MutedText;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginLeft = 3f;
            hint.style.marginBottom = 6f;
            foldout.Add(hint);

            return foldout;
        }

        VisualElement CreateCard(string title)
        {
            var card = new VisualElement();
            card.style.backgroundColor = CardBackground;
            card.style.marginBottom = 8f;
            SetPadding(card, 8f, 10f);
            SetRadius(card, 5f);
            SetBorder(card, OutlineBorder, 1f);

            var heading = new VisualElement();
            heading.style.flexDirection = FlexDirection.Row;
            heading.style.alignItems = Align.Center;
            heading.style.marginBottom = 7f;

            var bar = new VisualElement();
            bar.style.width = 3f;
            bar.style.height = 11f;
            bar.style.marginRight = 6f;
            bar.style.backgroundColor = Accent;
            SetRadius(bar, 1.5f);
            heading.Add(bar);

            var label = new Label(title.ToUpperInvariant());
            label.style.fontSize = 10f;
            label.style.letterSpacing = 1.2f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = MutedText;
            heading.Add(label);

            card.Add(heading);

            return card;
        }

        PropertyField CreateField(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            var field = new PropertyField(property);
            field.AddToClassList("unity-base-field__aligned");
            field.style.marginBottom = 1f;
            return field;
        }

        static Texture2D LoadLogo()
        {
            string[] matches = AssetDatabase.FindAssets(LogoFallbackFilter);

            foreach (string match in matches)
            {
                string candidate = AssetDatabase.GUIDToAssetPath(match);

                if (candidate.Contains("SimpleDissolve"))
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(candidate);
                }
            }

            if (matches.Length == 0)
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(matches[0]));
        }

        static void SetPadding(VisualElement element, float vertical, float horizontal)
        {
            element.style.paddingTop = vertical;
            element.style.paddingBottom = vertical;
            element.style.paddingLeft = horizontal;
            element.style.paddingRight = horizontal;
        }

        static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        static void SetBorder(VisualElement element, Color color, float width)
        {
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
        }
    }
}
