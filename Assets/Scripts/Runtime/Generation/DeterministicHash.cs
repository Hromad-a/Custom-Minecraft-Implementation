namespace CustomMinecraft.Generation
{
    /// <summary>
    /// Stateless integer hashing for all generation randomness. Every random-looking
    /// decision in the world is a pure function of position + seed, never a stateful
    /// RNG, so results are independent of evaluation order and always reproducible.
    /// </summary>
    public static class DeterministicHash
    {
        /// <summary>Derives an independent sub-seed from a master seed (one per concern).</summary>
        public static int DeriveSeed(int masterSeed, int salt) =>
            unchecked((int)Fold(Fold((uint)masterSeed) ^ (uint)salt));

        /// <summary>Uniform value in [0, 1) from a 2D coordinate and seed.</summary>
        public static float Value01(int x, int y, int seed)
        {
            uint h = Fold((uint)seed);
            h = Fold(h ^ (uint)x);
            h = Fold(h ^ (uint)y);
            return ToUnitFloat(h);
        }

        /// <summary>Uniform value in [0, 1) from a 3D coordinate and seed.</summary>
        public static float Value01(int x, int y, int z, int seed)
        {
            uint h = Fold((uint)seed);
            h = Fold(h ^ (uint)x);
            h = Fold(h ^ (uint)y);
            h = Fold(h ^ (uint)z);
            return ToUnitFloat(h);
        }

        // SplitMix-style avalanche; cheap and well distributed for lattice input.
        private static uint Fold(uint v)
        {
            unchecked
            {
                v ^= v >> 16;
                v *= 0x7FEB352Du;
                v ^= v >> 15;
                v *= 0x846CA68Bu;
                v ^= v >> 16;
                return v;
            }
        }

        // Top 24 bits -> float, keeps full float precision in [0, 1).
        private static float ToUnitFloat(uint h) => (h >> 8) * (1f / 16777216f);
    }
}
