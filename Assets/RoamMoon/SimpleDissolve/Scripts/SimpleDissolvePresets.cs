using UnityEngine;

namespace RoamMoon.SimpleDissolve
{
    public readonly struct SimpleDissolvePreset
    {
        public readonly Color EdgeColor;
        public readonly float EdgeWidth;
        public readonly float EdgeIntensity;
        public readonly float Softness;
        public readonly float NoiseScale;
        public readonly Vector2 Direction;
        public readonly float Radius;

        public SimpleDissolvePreset(
            Color edgeColor, float edgeWidth, float edgeIntensity,
            float softness, float noiseScale, Vector2 direction, float radius)
        {
            EdgeColor = edgeColor;
            EdgeWidth = edgeWidth;
            EdgeIntensity = edgeIntensity;
            Softness = softness;
            NoiseScale = noiseScale;
            Direction = direction;
            Radius = radius;
        }
    }

    public static class SimpleDissolvePresets
    {
        static Color C(int r, int g, int b, float exposure)
        {
            Color linear = new Color(r / 255f, g / 255f, b / 255f, 1f).linear;
            float scale = Mathf.Pow(2f, exposure);
            return new Color(linear.r * scale, linear.g * scale, linear.b * scale, 1f);
        }

        static readonly Vector2 Up = new Vector2(0f, 1f);
        static readonly Vector2 Right = new Vector2(1f, 0f);

        static readonly SimpleDissolvePreset[] Table =
        {
            // Noise
            new SimpleDissolvePreset(C(255, 255, 255, 1.2f), 0.09f, 2.0f, 0.07f, 1.4f, Up, 0.75f),
            // Burn
            new SimpleDissolvePreset(C(255, 95, 20, 1.7f), 0.12f, 2.5f, 0.06f, 1.3f, Up, 0.75f),
            // Directional
            new SimpleDissolvePreset(C(150, 215, 255, 1.3f), 0.09f, 2.2f, 0.06f, 1.8f, Right, 0.75f),
            // Radial
            new SimpleDissolvePreset(C(255, 205, 120, 1.4f), 0.10f, 2.3f, 0.06f, 1.8f, Up, 0.8f),
            // Pixel
            new SimpleDissolvePreset(C(120, 255, 190, 1.2f), 0.06f, 2.0f, 0.02f, 2.0f, Up, 0.75f),
            // Cells
            new SimpleDissolvePreset(C(190, 160, 255, 1.3f), 0.08f, 2.1f, 0.05f, 1.3f, Up, 0.75f),
        };

        public static SimpleDissolvePreset Get(SimpleDissolveMode mode)
        {
            int index = (int)mode;

            if (index < 0 || index >= Table.Length)
            {
                index = 0;
            }

            return Table[index];
        }
    }
}
