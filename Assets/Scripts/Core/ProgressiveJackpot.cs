using System;

namespace SlotGame.Core
{
    /// <summary>
    /// Bonus feature: a progressive jackpot, like classic casino machines.
    /// A share of every bet grows the pool; three jackpot symbols win the whole pool,
    /// which then resets to its seed value.
    /// </summary>
    public sealed class ProgressiveJackpot
    {
        private readonly int seedAmount;
        private readonly float contributionRate;

        // Fractions of a credit are carried over so small bets still grow the pool fairly
        // instead of being rounded away every spin.
        private float pendingFraction;

        public int Pool { get; private set; }

        public event Action<int> PoolChanged;

        public ProgressiveJackpot(int seedAmount, float contributionRate)
        {
            this.seedAmount = Math.Max(0, seedAmount);
            this.contributionRate = Math.Max(0f, contributionRate);
            Pool = this.seedAmount;
        }

        public void Contribute(int bet)
        {
            pendingFraction += bet * contributionRate;
            int wholeCredits = (int)pendingFraction;

            if (wholeCredits <= 0)
            {
                return;
            }

            pendingFraction -= wholeCredits;
            Pool += wholeCredits;
            PoolChanged?.Invoke(Pool);
        }

        /// <summary>Pays out the whole pool and resets it to the seed value.</summary>
        public int Award()
        {
            int amount = Pool;
            Pool = seedAmount;
            pendingFraction = 0f;
            PoolChanged?.Invoke(Pool);
            return amount;
        }
    }
}
