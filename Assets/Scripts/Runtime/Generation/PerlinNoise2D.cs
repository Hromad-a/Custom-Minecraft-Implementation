using System;

namespace CustomMinecraft.Generation
{
    /// <summary>
    /// Classic 2D Perlin gradient noise, seedable and fully deterministic.
    /// Gradients come from <see cref="DeterministicHash"/> instead of a permutation
    /// table, so the field never repeats and the seed is an explicit argument.
    /// </summary>
    public static class PerlinNoise2D
    {
        // A unit-gradient 2D Perlin sample peaks at sqrt(2)/2; rescale to ~[-1, 1].
        private const float NormalizationFactor = 1.41421356f;

        /// <summary>Single-octave noise in [-1, 1].</summary>
        public static float Noise(float x, float y, int seed)
        {
            int x0 = FastFloor(x);
            int y0 = FastFloor(y);
            float dx = x - x0;
            float dy = y - y0;

            float n00 = GradientDot(x0, y0, seed, dx, dy);
            float n10 = GradientDot(x0 + 1, y0, seed, dx - 1f, dy);
            float n01 = GradientDot(x0, y0 + 1, seed, dx, dy - 1f);
            float n11 = GradientDot(x0 + 1, y0 + 1, seed, dx - 1f, dy - 1f);

            float u = Fade(dx);
            float v = Fade(dy);

            float value = Lerp(Lerp(n00, n10, u), Lerp(n01, n11, u), v) * NormalizationFactor;
            return Math.Clamp(value, -1f, 1f);
        }

        /// <summary>
        /// Fractal Brownian motion: octave layers of Perlin noise at doubling
        /// frequency and decaying amplitude, normalized back to [-1, 1].
        /// </summary>
        public static float Fbm(float x, float y, int seed, int octaves, float persistence, float lacunarity = 2f)
        {
            if (octaves < 1)
                throw new ArgumentOutOfRangeException(nameof(octaves), "At least one octave is required.");

            float sum = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float amplitudeSum = 0f;

            for (int octave = 0; octave < octaves; octave++)
            {
                int octaveSeed = DeterministicHash.DeriveSeed(seed, octave);
                sum += Noise(x * frequency, y * frequency, octaveSeed) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return sum / amplitudeSum;
        }

        private static float GradientDot(int cellX, int cellY, int seed, float dx, float dy)
        {
            float angle = DeterministicHash.Value01(cellX, cellY, seed) * (2f * MathF.PI);
            return MathF.Cos(angle) * dx + MathF.Sin(angle) * dy;
        }

        // Perlin's quintic fade: zero first and second derivatives at cell borders.
        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static int FastFloor(float v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }
    }
}
