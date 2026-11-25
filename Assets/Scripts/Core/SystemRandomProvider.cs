using System;

namespace SlotGame.Core
{
    /// <summary>
    /// Default RNG backed by <see cref="System.Random"/>.
    ///
    /// Why not UnityEngine.Random?
    /// UnityEngine.Random is a single global state shared by every system in the game
    /// (particles, cosmetic effects, etc.), so any unrelated call would shift the slot results.
    /// Owning a private instance keeps the outcome stream isolated and lets us seed it for tests.
    ///
    /// Note: a real-money slot would use a certified CSPRNG on a server. For this client-side
    /// demo, an unseeded System.Random gives fair, unpredictable results.
    /// </summary>
    public sealed class SystemRandomProvider : IRandomProvider
    {
        private readonly Random random;

        /// <param name="seed">Optional fixed seed. Pass null for an unpredictable, time + GUID based seed.</param>
        public SystemRandomProvider(int? seed = null)
        {
            // Mixing tick count with a GUID hash avoids two instances created in the same tick
            // ending up with the same sequence.
            int actualSeed = seed ?? (Environment.TickCount ^ Guid.NewGuid().GetHashCode());
            random = new Random(actualSeed);
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Upper bound must be positive.");
            }

            return random.Next(maxExclusive);
        }
    }
}
