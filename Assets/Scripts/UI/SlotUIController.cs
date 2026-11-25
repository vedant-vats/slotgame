using SlotGame.Core;
using SlotGame.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// Connects the HUD (meters, buttons, popups) to the <see cref="SlotMachine"/>.
    /// The UI only reads state and forwards player input. All rules live in SlotMachine.
    /// </summary>
    public sealed class SlotUIController : MonoBehaviour
    {
        [SerializeField] private SlotMachine machine;

        [Header("Meters")]
        [SerializeField] private CountUpText creditsMeter;
        [SerializeField] private CountUpText betMeter;
        [SerializeField] private CountUpText winMeter;
        [SerializeField] private CountUpText jackpotMeter;
        [SerializeField] private Text messageText;

        [Header("Buttons")]
        [SerializeField] private Button spinButton;
        [SerializeField] private Text spinButtonLabel;
        [SerializeField] private Button betUpButton;
        [SerializeField] private Button betDownButton;
        [SerializeField] private Button autoSpinButton;
        [SerializeField] private Text autoSpinLabel;

        [Header("Popups")]
        [SerializeField] private WinPopup winPopup;
        [SerializeField] private ConfirmPopup confirmPopup;

        [Header("Feel")]
        [SerializeField] private float meterCountDuration = 0.6f;

        private void Awake()
        {
            spinButton.onClick.AddListener(machine.SpinOrStop);
            betUpButton.onClick.AddListener(() => machine.ChangeBet(+1));
            betDownButton.onClick.AddListener(() => machine.ChangeBet(-1));
            autoSpinButton.onClick.AddListener(machine.ToggleAutoSpin);
        }

        private void OnEnable()
        {
            machine.BalanceChanged += HandleBalanceChanged;
            machine.BetChanged += HandleBetChanged;
            machine.JackpotChanged += HandleJackpotChanged;
            machine.AutoSpinChanged += HandleAutoSpinChanged;
            machine.StateChanged += HandleStateChanged;
            machine.SpinStarted += HandleSpinStarted;
            machine.SpinCompleted += HandleSpinCompleted;
            machine.OutOfCredits += HandleOutOfCredits;
            machine.InsufficientCreditsForBet += HandleInsufficientCreditsForBet;
        }

        private void OnDisable()
        {
            machine.BalanceChanged -= HandleBalanceChanged;
            machine.BetChanged -= HandleBetChanged;
            machine.JackpotChanged -= HandleJackpotChanged;
            machine.AutoSpinChanged -= HandleAutoSpinChanged;
            machine.StateChanged -= HandleStateChanged;
            machine.SpinStarted -= HandleSpinStarted;
            machine.SpinCompleted -= HandleSpinCompleted;
            machine.OutOfCredits -= HandleOutOfCredits;
            machine.InsufficientCreditsForBet -= HandleInsufficientCreditsForBet;
        }

        private void Start()
        {
            // Pull the initial values once. After this, everything is event driven.
            creditsMeter.SetImmediate(machine.Balance);
            betMeter.SetImmediate(machine.CurrentBet);
            winMeter.SetImmediate(0);
            jackpotMeter.SetImmediate(machine.JackpotPool);
            HandleAutoSpinChanged(machine.AutoSpinEnabled);
            HandleStateChanged(machine.State);
            ShowMessage("Line up 3 matching symbols to win!");

            winPopup.gameObject.SetActive(false);
            confirmPopup.gameObject.SetActive(false);
        }

        private void Update()
        {
            // Space = spin / stop, unless a dialog is open.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || confirmPopup.IsVisible)
            {
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                if (winPopup.IsVisible)
                {
                    winPopup.Hide();
                }

                machine.SpinOrStop();
            }
        }

        private void HandleBalanceChanged(int balance)
        {
            creditsMeter.AnimateTo(balance, meterCountDuration);
        }

        private void HandleBetChanged(int bet)
        {
            betMeter.SetImmediate(bet);
            RefreshButtons(machine.State);
        }

        private void HandleJackpotChanged(int pool)
        {
            jackpotMeter.AnimateTo(pool, meterCountDuration);
        }

        private void HandleAutoSpinChanged(bool isOn)
        {
            autoSpinLabel.text = isOn ? "AUTO\nON" : "AUTO\nOFF";
        }

        private void HandleStateChanged(SlotState state)
        {
            spinButtonLabel.text = state == SlotState.Spinning ? "STOP" : "SPIN";
            RefreshButtons(state);
        }

        private void RefreshButtons(SlotState state)
        {
            spinButton.interactable = state != SlotState.Presenting || !machine.AutoSpinEnabled;
            betUpButton.interactable = machine.CanIncreaseBet;
            betDownButton.interactable = machine.CanDecreaseBet;
        }

        private void HandleSpinStarted()
        {
            winMeter.SetImmediate(0);
            ShowMessage("Good luck!");
        }

        private void HandleSpinCompleted(SpinReport report)
        {
            if (!report.IsWin)
            {
                ShowMessage("No match. Try again!");
                return;
            }

            winMeter.AnimateTo(report.TotalWin, meterCountDuration);

            string symbolName = machine.Config.Symbols[report.Evaluation.WinningSymbolIndex].DisplayName;
            ShowMessage(report.IsJackpot
                ? $"Three {symbolName}! {report.Evaluation.LinePayout:N0} + {report.JackpotWon:N0} jackpot = {report.TotalWin:N0}"
                : $"Three {symbolName}! You win {report.TotalWin:N0}");

            if (report.IsJackpot)
            {
                winPopup.Show("JACKPOT!", report.TotalWin);
            }
            else if (report.TotalWin >= report.Bet * machine.Config.BigWinMultiplier)
            {
                winPopup.Show("BIG WIN!", report.TotalWin);
            }
        }

        private void HandleOutOfCredits()
        {
            ShowMessage("You're out of credits.");
            confirmPopup.Show(
                $"Out of credits!\nTake {machine.Config.RefillCredits:N0} free credits?",
                machine.Refill);
        }

        private void HandleInsufficientCreditsForBet()
        {
            ShowMessage("Not enough credits for this bet. Lower your bet.");
        }

        private void ShowMessage(string message)
        {
            messageText.text = message;
        }
    }
}
