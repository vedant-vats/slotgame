using SlotGame.Core;
using SlotGame.Game;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// Casino-style chase lights around the JACKPOT marquee.
    /// Idle: a slow chase. Spinning: a fast chase. After a win: every bulb flashes together.
    /// </summary>
    public sealed class MarqueeLights : MonoBehaviour
    {
        [SerializeField] private SlotMachine machine;
        [SerializeField] private Image[] bulbs;

        [Header("Look")]
        [SerializeField] private Color onColor = new Color(1f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color offColor = new Color(0.55f, 0.35f, 0.25f, 0.45f);
        [Tooltip("Every Nth bulb is lit, and the pattern shifts one bulb per step.")]
        [SerializeField, Min(2)] private int chaseSpacing = 3;

        [Header("Timing (seconds)")]
        [SerializeField] private float idleStepInterval = 0.2f;
        [SerializeField] private float spinStepInterval = 0.05f;
        [SerializeField] private float celebrateDuration = 2f;
        [SerializeField] private float celebrateBlinkInterval = 0.12f;

        private float stepTimer;
        private int step;
        private bool spinning;
        private float celebrateUntil;

        private void OnEnable()
        {
            machine.SpinStarted += HandleSpinStarted;
            machine.SpinCompleted += HandleSpinCompleted;
        }

        private void OnDisable()
        {
            machine.SpinStarted -= HandleSpinStarted;
            machine.SpinCompleted -= HandleSpinCompleted;
        }

        private void Start()
        {
            ApplyChase();
        }

        private void Update()
        {
            if (Time.time < celebrateUntil)
            {
                bool allOn = Mathf.FloorToInt(Time.time / celebrateBlinkInterval) % 2 == 0;
                for (int i = 0; i < bulbs.Length; i++)
                {
                    SetBulb(bulbs[i], allOn);
                }

                return;
            }

            stepTimer += Time.deltaTime;
            float interval = spinning ? spinStepInterval : idleStepInterval;
            if (stepTimer < interval)
            {
                return;
            }

            stepTimer = 0f;
            step++;
            ApplyChase();
        }

        private void HandleSpinStarted()
        {
            spinning = true;
            celebrateUntil = 0f;
        }

        private void HandleSpinCompleted(SpinReport report)
        {
            spinning = false;

            if (report.IsWin)
            {
                // Jackpots celebrate twice as long.
                celebrateUntil = Time.time + celebrateDuration * (report.IsJackpot ? 2f : 1f);
            }
        }

        private void ApplyChase()
        {
            for (int i = 0; i < bulbs.Length; i++)
            {
                SetBulb(bulbs[i], (i + step) % chaseSpacing == 0);
            }
        }

        private void SetBulb(Image bulb, bool isOn)
        {
            bulb.color = isOn ? onColor : offColor;
            float scale = isOn ? 1f : 0.8f;
            bulb.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
