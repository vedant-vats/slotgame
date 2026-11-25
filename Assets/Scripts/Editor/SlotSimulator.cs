using SlotGame.Core;
using SlotGame.Data;
using UnityEditor;
using UnityEngine;

namespace SlotGame.EditorTools
{
    /// <summary>
    /// Runs the real outcome and payout code for many spins and logs the statistics.
    /// Used to check that the RNG and paytable behave as designed (hit rate, return to player).
    /// </summary>
    public static class SlotSimulator
    {
        private const string ConfigPath = "Assets/Data/SlotConfig.asset";
        private const int SpinCount = 1_000_000;
        private const int Bet = 10;

        [MenuItem("Tools/Slot Game/Simulate 1,000,000 Spins")]
        public static void Simulate()
        {
            var config = AssetDatabase.LoadAssetAtPath<SlotConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("[SlotSimulator] SlotConfig not found. Run Tools > Slot Game > Rebuild Scene first.");
                return;
            }

            var random = new SystemRandomProvider(seed: 2024);
            var generator = new OutcomeGenerator(config.Symbols, random);
            var calculator = new PayoutCalculator(config.Symbols);
            var jackpot = new ProgressiveJackpot(config.JackpotSeed, config.JackpotContribution);

            long totalBet = 0;
            long totalWon = 0;
            int wins = 0;
            int jackpots = 0;
            var winsPerSymbol = new int[config.Symbols.Count];

            for (int spin = 0; spin < SpinCount; spin++)
            {
                totalBet += Bet;
                jackpot.Contribute(Bet);

                int[] outcome = generator.Generate(3);
                SpinEvaluation evaluation = calculator.Evaluate(outcome, Bet);
                if (!evaluation.IsWin)
                {
                    continue;
                }

                wins++;
                winsPerSymbol[evaluation.WinningSymbolIndex]++;
                totalWon += evaluation.LinePayout;

                if (evaluation.TriggersJackpot)
                {
                    jackpots++;
                    totalWon += jackpot.Award();
                }
            }

            var log = new System.Text.StringBuilder();
            log.AppendLine($"[SlotSimulator] {SpinCount:N0} spins at bet {Bet}");
            log.AppendLine($"Hit rate: {100.0 * wins / SpinCount:F2}%  (1 in {(double)SpinCount / Mathf.Max(1, wins):F1})");
            log.AppendLine($"Return to player (incl. jackpot): {100.0 * totalWon / totalBet:F2}%");
            log.AppendLine($"Theoretical line RTP: {100.0 * PayoutCalculator.CalculateLineRtp(config.Symbols, 3):F2}%");
            log.AppendLine($"Jackpots won: {jackpots}");
            for (int i = 0; i < winsPerSymbol.Length; i++)
            {
                log.AppendLine($"  {config.Symbols[i].DisplayName}: {winsPerSymbol[i]:N0} wins");
            }

            Debug.Log(log.ToString());
        }
    }
}
