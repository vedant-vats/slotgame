namespace SlotGame.Core
{
    /// <summary>Result of checking one payline against the paytable. Immutable.</summary>
    public readonly struct SpinEvaluation
    {
        public static readonly SpinEvaluation Loss = new SpinEvaluation(-1, 0, false);

        /// <summary>Index of the matched symbol, or -1 when there was no win.</summary>
        public int WinningSymbolIndex { get; }

        /// <summary>Line payout in credits (bet x multiplier). Excludes the progressive jackpot.</summary>
        public int LinePayout { get; }

        /// <summary>True when the matched symbol also awards the progressive jackpot.</summary>
        public bool TriggersJackpot { get; }

        public bool IsWin => WinningSymbolIndex >= 0;

        public SpinEvaluation(int winningSymbolIndex, int linePayout, bool triggersJackpot)
        {
            WinningSymbolIndex = winningSymbolIndex;
            LinePayout = linePayout;
            TriggersJackpot = triggersJackpot;
        }
    }
}
