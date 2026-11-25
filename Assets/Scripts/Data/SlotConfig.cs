using System.Collections.Generic;
using UnityEngine;

namespace SlotGame.Data
{
    /// <summary>
    /// All tunable numbers for the machine in one asset: symbols, economy and reel feel.
    /// Gameplay classes read from here instead of hard-coding values.
    /// </summary>
    [CreateAssetMenu(fileName = "SlotConfig", menuName = "Slot Game/Slot Config")]
    public sealed class SlotConfig : ScriptableObject
    {
        [Header("Symbols")]
        [SerializeField] private List<SymbolData> symbols = new List<SymbolData>();

        [Header("Economy")]
        [Tooltip("Bets the player can cycle through with the arrow buttons.")]
        [SerializeField] private int[] betOptions = { 10, 25, 50, 100 };
        [SerializeField, Min(0)] private int defaultBetIndex;
        [SerializeField, Min(0)] private int startingCredits = 1000;
        [Tooltip("Credits granted when the player runs out and accepts a refill.")]
        [SerializeField, Min(0)] private int refillCredits = 1000;
        [Tooltip("Value the progressive jackpot resets to after it is won.")]
        [SerializeField, Min(0)] private int jackpotSeed = 500;
        [Tooltip("Share of every bet added to the progressive jackpot (0.05 = 5%).")]
        [SerializeField, Range(0f, 0.5f)] private float jackpotContribution = 0.05f;
        [Tooltip("A win of at least this many times the bet shows the BIG WIN popup.")]
        [SerializeField, Min(1)] private int bigWinMultiplier = 20;

        [Header("Reel Motion (distances in symbol cells)")]
        [Tooltip("Top speed of a spinning reel, in symbol cells per second.")]
        [SerializeField, Min(1f)] private float reelSpeed = 16f;
        [Tooltip("How far the reel pulls back before it starts spinning (anticipation).")]
        [SerializeField, Min(0f)] private float windUpDistance = 0.3f;
        [SerializeField, Min(0.01f)] private float windUpDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float accelerationDuration = 0.25f;
        [Tooltip("Minimum time all reels spin before the first one starts stopping.")]
        [SerializeField, Min(0f)] private float minSpinDuration = 0.9f;
        [Tooltip("Delay between consecutive reels stopping (left to right).")]
        [SerializeField, Min(0f)] private float reelStopInterval = 0.32f;
        [Tooltip("Cells a reel travels while decelerating into its result.")]
        [SerializeField, Min(1)] private int stopDistance = 3;
        [Tooltip("Shorter stopping distance used when the player slams STOP.")]
        [SerializeField, Min(1)] private int quickStopDistance = 1;
        [Tooltip("How far the reel overshoots the payline before settling back.")]
        [SerializeField, Range(0f, 0.5f)] private float bounceOvershoot = 0.22f;
        [SerializeField, Min(0.01f)] private float bounceDuration = 0.16f;
        [Tooltip("Vertical stretch applied to symbols at full speed to fake motion blur.")]
        [SerializeField, Range(0f, 0.5f)] private float motionStretch = 0.12f;

        [Header("Flow")]
        [SerializeField, Min(0f)] private float autoSpinDelay = 0.5f;
        [Tooltip("How long a win is shown before auto spin continues.")]
        [SerializeField, Min(0f)] private float winPresentationDuration = 1.6f;

        public IReadOnlyList<SymbolData> Symbols => symbols;
        public IReadOnlyList<int> BetOptions => betOptions;
        public int DefaultBetIndex => Mathf.Clamp(defaultBetIndex, 0, Mathf.Max(0, betOptions.Length - 1));
        public int MinimumBet => betOptions.Length > 0 ? betOptions[0] : 0;
        public int StartingCredits => startingCredits;
        public int RefillCredits => refillCredits;
        public int JackpotSeed => jackpotSeed;
        public float JackpotContribution => jackpotContribution;
        public int BigWinMultiplier => bigWinMultiplier;

        public float ReelSpeed => reelSpeed;
        public float WindUpDistance => windUpDistance;
        public float WindUpDuration => windUpDuration;
        public float AccelerationDuration => accelerationDuration;
        public float MinSpinDuration => minSpinDuration;
        public float ReelStopInterval => reelStopInterval;
        public int StopDistance => stopDistance;
        public int QuickStopDistance => quickStopDistance;
        public float BounceOvershoot => bounceOvershoot;
        public float BounceDuration => bounceDuration;
        public float MotionStretch => motionStretch;

        public float AutoSpinDelay => autoSpinDelay;
        public float WinPresentationDuration => winPresentationDuration;

        /// <summary>Replaces the symbol list. Used by the editor scene builder.</summary>
        public void SetSymbols(IEnumerable<SymbolData> newSymbols)
        {
            symbols = new List<SymbolData>(newSymbols);
        }
    }
}
