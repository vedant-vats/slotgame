namespace SlotGame.Core
{
    /// <summary>
    /// Abstraction over the random number source.
    /// Game logic depends on this interface instead of a concrete RNG so that:
    ///  - tests can inject a seeded / scripted provider for deterministic results, and
    ///  - a production build could swap in a certified or server-side RNG without touching game code.
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>Returns a uniformly distributed integer in the range [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }
}
