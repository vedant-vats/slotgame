using System;
using System.Collections.Generic;
using SlotGame.Data;

namespace SlotGame.Core
{
    /// <summary>
    /// Pure paytable logic, with no Unity scene dependencies, so it is easy to unit test.
    ///
    /// Rule (from the brief): the player wins when ALL reels show the same symbol on the payline.
    /// Payout = bet x that symbol's multiplier. Symbols flagged with AwardsJackpot also trigger
    /// the progressive jackpot, which is paid by <see cref="SlotGame.Game.SlotMachine"/>
    /// because the pool is game state, not paytable data.
    /// </summary>
    public sealed class PayoutCalculator
    {
        private readonly IReadOnlyList<SymbolData> symbols;

        public PayoutCalculator(IReadOnlyList<SymbolData> symbols)
        {
            this.symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
        }

        public SpinEvaluation Evaluate(IReadOnlyList<int> paylineSymbols, int bet)
        {
            if (paylineSymbols == null || paylineSymbols.Count == 0)
            {
                return SpinEvaluation.Loss;
            }

            int first = paylineSymbols[0];

            for (int reel = 1; reel < paylineSymbols.Count; reel++)
            {
                if (paylineSymbols[reel] != first)
                {
                    return SpinEvaluation.Loss;
                }
            }

            SymbolData symbol = symbols[first];
            return new SpinEvaluation(first, bet * symbol.PayoutMultiplier, symbol.AwardsJackpot);
        }

        /// <summary>
        /// Theoretical return-to-player of the line wins (jackpot excluded), for independent reels.
        /// P(all reels = s) = p(s)^reels, so RTP = sum over s of p(s)^reels * multiplier(s).
        /// </summary>
        public static double CalculateLineRtp(IReadOnlyList<SymbolData> symbols, int reelCount)
        {
            double totalWeight = 0;
            foreach (SymbolData symbol in symbols)
            {
                totalWeight += symbol.Weight;
            }

            double rtp = 0;
            foreach (SymbolData symbol in symbols)
            {
                double p = symbol.Weight / totalWeight;
                rtp += Math.Pow(p, reelCount) * symbol.PayoutMultiplier;
            }

            return rtp;
        }
    }
}
