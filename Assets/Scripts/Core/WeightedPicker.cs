using System;
using System.Collections.Generic;

namespace SlotGame.Core
{
    /// <summary>
    /// Picks an index from a list of integer weights.
    /// A symbol with weight 4 is four times as likely as a symbol with weight 1.
    ///
    /// Implementation: build a cumulative table once (e.g. weights 1,2,3,4 -> 1,3,6,10),
    /// roll a number in [0, total) and return the first bucket whose cumulative value exceeds it.
    /// </summary>
    public sealed class WeightedPicker
    {
        private readonly int[] cumulativeWeights;

        public int TotalWeight { get; }
        public int Count => cumulativeWeights.Length;

        public WeightedPicker(IReadOnlyList<int> weights)
        {
            if (weights == null || weights.Count == 0)
            {
                throw new ArgumentException("At least one weight is required.", nameof(weights));
            }

            cumulativeWeights = new int[weights.Count];
            int runningTotal = 0;

            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] < 0)
                {
                    throw new ArgumentException($"Weight at index {i} is negative.", nameof(weights));
                }

                runningTotal += weights[i];
                cumulativeWeights[i] = runningTotal;
            }

            if (runningTotal == 0)
            {
                throw new ArgumentException("Weights must not all be zero.", nameof(weights));
            }

            TotalWeight = runningTotal;
        }

        public int Pick(IRandomProvider random)
        {
            int roll = random.Next(TotalWeight);

            // Linear scan is fine: a slot has a handful of symbols.
            for (int i = 0; i < cumulativeWeights.Length; i++)
            {
                if (roll < cumulativeWeights[i])
                {
                    return i;
                }
            }

            // Unreachable while roll < TotalWeight, kept for safety.
            return cumulativeWeights.Length - 1;
        }

        /// <summary>Probability (0..1) of a single pick landing on the given index.</summary>
        public double GetProbability(int index)
        {
            int previous = index == 0 ? 0 : cumulativeWeights[index - 1];
            return (double)(cumulativeWeights[index] - previous) / TotalWeight;
        }
    }
}
