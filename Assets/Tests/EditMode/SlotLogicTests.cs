using System.Collections.Generic;
using NUnit.Framework;
using SlotGame.Core;
using SlotGame.Data;

namespace SlotGame.Tests
{
    /// <summary>Unit tests for the pure game logic: RNG weighting, payouts, wallet and jackpot.</summary>
    public sealed class SlotLogicTests
    {
        /// <summary>Returns a scripted sequence, so tests control the "random" rolls exactly.</summary>
        private sealed class ScriptedRandom : IRandomProvider
        {
            private readonly Queue<int> values;

            public ScriptedRandom(params int[] values)
            {
                this.values = new Queue<int>(values);
            }

            public int Next(int maxExclusive)
            {
                return values.Dequeue() % maxExclusive;
            }
        }

        private static List<SymbolData> CreateSymbols()
        {
            return new List<SymbolData>
            {
                SymbolData.Create("seven", "Sevens", 1, 100, awardsJackpot: true),
                SymbolData.Create("cherry", "Cherries", 4, 5),
                SymbolData.Create("bell", "Bells", 3, 10),
                SymbolData.Create("bar", "BARs", 2, 25),
            };
        }

        [Test]
        public void Payout_ThreeOfAKind_PaysBetTimesMultiplier()
        {
            var calculator = new PayoutCalculator(CreateSymbols());

            SpinEvaluation result = calculator.Evaluate(new[] { 2, 2, 2 }, bet: 10);

            Assert.IsTrue(result.IsWin);
            Assert.AreEqual(2, result.WinningSymbolIndex);
            Assert.AreEqual(100, result.LinePayout);
            Assert.IsFalse(result.TriggersJackpot);
        }

        [Test]
        public void Payout_MixedSymbols_IsALoss()
        {
            var calculator = new PayoutCalculator(CreateSymbols());

            SpinEvaluation result = calculator.Evaluate(new[] { 1, 1, 2 }, bet: 10);

            Assert.IsFalse(result.IsWin);
            Assert.AreEqual(0, result.LinePayout);
        }

        [Test]
        public void Payout_ThreeSevens_TriggersJackpot()
        {
            var calculator = new PayoutCalculator(CreateSymbols());

            SpinEvaluation result = calculator.Evaluate(new[] { 0, 0, 0 }, bet: 25);

            Assert.IsTrue(result.TriggersJackpot);
            Assert.AreEqual(2500, result.LinePayout);
        }

        [Test]
        public void WeightedPicker_MapsRollsToCumulativeBuckets()
        {
            // Weights 1,4,3,2 -> buckets [0], [1..4], [5..7], [8..9]
            var picker = new WeightedPicker(new[] { 1, 4, 3, 2 });
            var random = new ScriptedRandom(0, 1, 4, 5, 7, 8, 9);

            int[] expected = { 0, 1, 1, 2, 2, 3, 3 };
            foreach (int index in expected)
            {
                Assert.AreEqual(index, picker.Pick(random));
            }
        }

        [Test]
        public void WeightedPicker_ZeroWeightSymbol_IsNeverPicked()
        {
            var picker = new WeightedPicker(new[] { 0, 5 });
            var random = new SystemRandomProvider(seed: 1);

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(1, picker.Pick(random));
            }
        }

        [Test]
        public void WeightedPicker_Distribution_MatchesWeights()
        {
            var picker = new WeightedPicker(new[] { 1, 4, 3, 2 });
            var random = new SystemRandomProvider(seed: 42);
            var counts = new int[4];
            const int samples = 200_000;

            for (int i = 0; i < samples; i++)
            {
                counts[picker.Pick(random)]++;
            }

            for (int i = 0; i < counts.Length; i++)
            {
                double observed = (double)counts[i] / samples;
                Assert.AreEqual(picker.GetProbability(i), observed, 0.01, $"Symbol {i} frequency is off.");
            }
        }

        [Test]
        public void Wallet_CannotDebitMoreThanBalance()
        {
            var wallet = new Wallet(50);

            Assert.IsFalse(wallet.TryDebit(60));
            Assert.AreEqual(50, wallet.Balance);
            Assert.IsTrue(wallet.TryDebit(50));
            Assert.AreEqual(0, wallet.Balance);
        }

        [Test]
        public void Jackpot_GrowsWithBets_AndResetsWhenWon()
        {
            var jackpot = new ProgressiveJackpot(seedAmount: 500, contributionRate: 0.05f);

            for (int i = 0; i < 10; i++)
            {
                jackpot.Contribute(10); // 0.5 credits each, carried over as a fraction
            }

            Assert.AreEqual(505, jackpot.Pool);
            Assert.AreEqual(505, jackpot.Award());
            Assert.AreEqual(500, jackpot.Pool);
        }

        [Test]
        public void TheoreticalRtp_MatchesDesign()
        {
            // (1*100 + 8*25 + 27*10 + 64*5) / 1000 = 0.89
            Assert.AreEqual(0.89, PayoutCalculator.CalculateLineRtp(CreateSymbols(), 3), 1e-9);
        }
    }
}
