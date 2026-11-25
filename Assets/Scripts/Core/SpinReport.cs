using System.Collections.Generic;

namespace SlotGame.Core
{
    /// <summary>Everything the presentation layer needs to know about a finished spin.</summary>
    public sealed class SpinReport
    {
        public IReadOnlyList<int> PaylineSymbols { get; }
        public SpinEvaluation Evaluation { get; }
        public int Bet { get; }
        public int JackpotWon { get; }
        public int TotalWin => Evaluation.LinePayout + JackpotWon;
        public bool IsWin => TotalWin > 0;
        public bool IsJackpot => JackpotWon > 0;

        public SpinReport(IReadOnlyList<int> paylineSymbols, SpinEvaluation evaluation, int bet, int jackpotWon)
        {
            PaylineSymbols = paylineSymbols;
            Evaluation = evaluation;
            Bet = bet;
            JackpotWon = jackpotWon;
        }
    }
}
