using System;
using System.Collections;
using SlotGame.Core;
using SlotGame.Data;
using SlotGame.Reels;
using UnityEngine;

namespace SlotGame.Game
{
    public enum SlotState
    {
        Idle,
        Spinning,
        Presenting
    }

    /// <summary>
    /// Central game controller. Owns the rules and state (wallet, bet, jackpot, spin flow)
    /// and tells the reels what to do. It knows nothing about UI or audio: those listen to
    /// its events (observer pattern), so presentation can change without touching game logic.
    ///
    /// Spin flow:
    ///   1. Debit the bet and feed the progressive jackpot.
    ///   2. Roll the outcome with the RNG (before any animation).
    ///   3. Spin all reels, then stop them left to right on the rolled symbols.
    ///   4. Evaluate the payline, pay out, and raise <see cref="SpinCompleted"/>.
    /// </summary>
    public sealed class SlotMachine : MonoBehaviour
    {
        [SerializeField] private SlotConfig config;
        [SerializeField] private ReelController[] reels;

        [Header("Debug")]
        [Tooltip("Use a fixed RNG seed so a sequence of spins can be reproduced while debugging.")]
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 12345;

        private Wallet wallet;
        private ProgressiveJackpot jackpot;
        private PayoutCalculator payoutCalculator;
        private OutcomeGenerator outcomeGenerator;
        private int betIndex;
        private bool quickStopRequested;

        public SlotState State { get; private set; } = SlotState.Idle;
        public SlotConfig Config => config;
        public int Balance => wallet.Balance;
        public int CurrentBet => config.BetOptions[betIndex];
        public int JackpotPool => jackpot.Pool;
        public bool AutoSpinEnabled { get; private set; }
        public bool CanIncreaseBet => State == SlotState.Idle && betIndex < config.BetOptions.Count - 1;
        public bool CanDecreaseBet => State == SlotState.Idle && betIndex > 0;

        public event Action<int> BalanceChanged;
        public event Action<int> BetChanged;
        public event Action<int> JackpotChanged;
        public event Action<bool> AutoSpinChanged;
        public event Action<SlotState> StateChanged;
        public event Action SpinStarted;
        public event Action<int> ReelStopped;
        public event Action<SpinReport> SpinCompleted;

        /// <summary>Balance is below the smallest bet. The player needs a refill.</summary>
        public event Action OutOfCredits;

        /// <summary>The player can still play, but not at the current bet.</summary>
        public event Action InsufficientCreditsForBet;

        private void Awake()
        {
            if (config == null || reels == null || reels.Length == 0)
            {
                Debug.LogError("SlotMachine is missing its config or reels.", this);
                enabled = false;
                return;
            }

            IRandomProvider outcomeRandom = useFixedSeed
                ? new SystemRandomProvider(fixedSeed)
                : new SystemRandomProvider();

            // Reels get their own RNG for cosmetic filler symbols, so how long the animation
            // runs never consumes numbers from the outcome stream.
            IRandomProvider cosmeticRandom = new SystemRandomProvider();

            wallet = new Wallet(config.StartingCredits);
            jackpot = new ProgressiveJackpot(config.JackpotSeed, config.JackpotContribution);
            payoutCalculator = new PayoutCalculator(config.Symbols);
            outcomeGenerator = new OutcomeGenerator(config.Symbols, outcomeRandom);
            betIndex = config.DefaultBetIndex;

            wallet.BalanceChanged += value => BalanceChanged?.Invoke(value);
            jackpot.PoolChanged += value => JackpotChanged?.Invoke(value);

            for (int i = 0; i < reels.Length; i++)
            {
                int reelIndex = i;
                reels[i].Initialize(config, cosmeticRandom, outcomeGenerator.PickSymbol());
                reels[i].Stopped += _ => ReelStopped?.Invoke(reelIndex);
            }
        }

        /// <summary>Main button behaviour: spin when idle, slam-stop when already spinning.</summary>
        public void SpinOrStop()
        {
            if (State == SlotState.Spinning)
            {
                quickStopRequested = true;
                return;
            }

            TrySpin();
        }

        public bool TrySpin()
        {
            if (State != SlotState.Idle)
            {
                return false;
            }

            int bet = CurrentBet;

            if (!wallet.TryDebit(bet))
            {
                SetAutoSpin(false);

                if (wallet.Balance < config.MinimumBet)
                {
                    OutOfCredits?.Invoke();
                }
                else
                {
                    InsufficientCreditsForBet?.Invoke();
                }

                return false;
            }

            jackpot.Contribute(bet);

            // The result is decided here, before a single frame of animation.
            int[] outcome = outcomeGenerator.Generate(reels.Length);
            StartCoroutine(SpinRoutine(outcome, bet));
            return true;
        }

        public void ChangeBet(int direction)
        {
            if (State != SlotState.Idle)
            {
                return;
            }

            int newIndex = Mathf.Clamp(betIndex + Math.Sign(direction), 0, config.BetOptions.Count - 1);
            if (newIndex == betIndex)
            {
                return;
            }

            betIndex = newIndex;
            BetChanged?.Invoke(CurrentBet);
        }

        public void ToggleAutoSpin()
        {
            SetAutoSpin(!AutoSpinEnabled);
        }

        public void SetAutoSpin(bool enabledValue)
        {
            if (AutoSpinEnabled == enabledValue)
            {
                return;
            }

            AutoSpinEnabled = enabledValue;
            AutoSpinChanged?.Invoke(AutoSpinEnabled);

            if (AutoSpinEnabled && State == SlotState.Idle)
            {
                TrySpin();
            }
        }

        /// <summary>Free credits so the demo never dead-ends. Only allowed when the player is broke.</summary>
        public void Refill()
        {
            if (wallet.Balance >= config.MinimumBet)
            {
                return;
            }

            wallet.Credit(config.RefillCredits);
        }

        private IEnumerator SpinRoutine(int[] outcome, int bet)
        {
            quickStopRequested = false;
            SetState(SlotState.Spinning);
            SpinStarted?.Invoke();

            foreach (ReelController reel in reels)
            {
                reel.StartSpin();
            }

            yield return WaitUnlessQuickStop(config.MinSpinDuration);

            // Stop reels left to right. The staggered stops build suspense.
            for (int i = 0; i < reels.Length; i++)
            {
                reels[i].RequestStop(outcome[i], quickStopRequested);

                if (i < reels.Length - 1)
                {
                    float interval = quickStopRequested ? config.ReelStopInterval * 0.25f : config.ReelStopInterval;
                    yield return WaitUnlessQuickStop(interval);
                }
            }

            while (AnyReelSpinning())
            {
                yield return null;
            }

            SpinReport report = ResolveSpin(outcome, bet);

            SetState(SlotState.Presenting);
            SpinCompleted?.Invoke(report);

            if (AutoSpinEnabled)
            {
                yield return new WaitForSeconds(report.IsWin ? config.WinPresentationDuration : config.AutoSpinDelay);
            }

            SetState(SlotState.Idle);

            if (AutoSpinEnabled)
            {
                TrySpin();
            }
        }

        private SpinReport ResolveSpin(int[] outcome, int bet)
        {
            SpinEvaluation evaluation = payoutCalculator.Evaluate(outcome, bet);
            int jackpotWon = evaluation.TriggersJackpot ? jackpot.Award() : 0;
            var report = new SpinReport(outcome, evaluation, bet, jackpotWon);

            if (report.IsWin)
            {
                wallet.Credit(report.TotalWin);

                foreach (ReelController reel in reels)
                {
                    reel.PlayWinHighlight();
                }
            }

            return report;
        }

        private IEnumerator WaitUnlessQuickStop(float seconds)
        {
            for (float t = 0f; t < seconds && !quickStopRequested; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private bool AnyReelSpinning()
        {
            foreach (ReelController reel in reels)
            {
                if (reel.IsSpinning)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetState(SlotState newState)
        {
            State = newState;
            StateChanged?.Invoke(State);
        }
    }
}
