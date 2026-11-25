using System.Collections.Generic;
using System.Linq;
using SlotGame.Data;

namespace SlotGame.Core
{
    /// <summary>
    /// Decides the result of a spin.
    ///
    /// Key design decision: the outcome is rolled BEFORE the reels animate.
    /// The reel animation is purely presentation and simply lands on the pre-decided symbols,
    /// so frame rate, timing or the player slamming STOP can never influence the result.
    /// This is how real slot machines work.
    ///
    /// Each reel is rolled independently using the symbol weights from <see cref="SymbolData"/>.
    /// </summary>
    public sealed class OutcomeGenerator
    {
        private readonly WeightedPicker picker;
        private readonly IRandomProvider random;

        public OutcomeGenerator(IReadOnlyList<SymbolData> symbols, IRandomProvider random)
        {
            this.random = random;
            picker = new WeightedPicker(symbols.Select(symbol => symbol.Weight).ToList());
        }

        /// <summary>Rolls one weighted symbol index.</summary>
        public int PickSymbol()
        {
            return picker.Pick(random);
        }

        /// <summary>Rolls the payline symbol for every reel.</summary>
        public int[] Generate(int reelCount)
        {
            var result = new int[reelCount];

            for (int reel = 0; reel < reelCount; reel++)
            {
                result[reel] = PickSymbol();
            }

            return result;
        }
    }
}
