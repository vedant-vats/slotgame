using System;
using System.Collections;
using System.Collections.Generic;
using SlotGame.Core;
using SlotGame.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.Reels
{
    /// <summary>
    /// Animates one reel and lands it on a symbol chosen by the game logic.
    ///
    /// How the reel works:
    ///   The reel is modelled as an endless vertical strip. Every whole number k on the strip
    ///   holds one symbol. <see cref="position"/> is a float measured in symbol cells, and the
    ///   symbol at index k is drawn at y = (k - position) * cellHeight. y = 0 is the payline.
    ///   Increasing position makes the symbols fall, like a real reel.
    ///
    ///   Only a few Image cells exist (enough to cover the window). Each frame they are
    ///   repositioned and re-skinned for the indices currently visible. This is object pooling
    ///   with zero allocations during a spin.
    ///
    ///   Strip indices that have never been seen get a random cosmetic symbol. When the reel
    ///   is told to stop, the target symbol is written into an index a few cells above the
    ///   payline, and the reel decelerates exactly onto it.
    ///
    /// Motion phases: wind-up (small pull back) -> accelerate -> full speed -> decelerate -> bounce.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReelController : MonoBehaviour
    {
        [Tooltip("Pooled symbol images, ordered bottom to top. Five covers a three-row window.")]
        [SerializeField] private Image[] cells;

        [Tooltip("Distance between two symbols on the strip, in local UI units.")]
        [SerializeField] private float cellHeight = 84f;

        [Tooltip("Colour applied to symbols that are not on the winning line during a win.")]
        [SerializeField] private Color dimmedColor = new Color(0.45f, 0.45f, 0.55f, 1f);

        private readonly Dictionary<int, int> strip = new Dictionary<int, int>();

        private SlotConfig config;
        private IRandomProvider cosmeticRandom;
        private float position;
        private float currentSpeed;
        private int landedIndex;
        private bool stopRequested;
        private int requestedSymbol;
        private bool requestedQuickStop;
        private Coroutine motionRoutine;
        private Coroutine highlightRoutine;

        /// <summary>True from <see cref="StartSpin"/> until the reel has fully settled.</summary>
        public bool IsSpinning { get; private set; }

        /// <summary>Symbol currently sitting on the payline.</summary>
        public int PaylineSymbol => GetSymbolAt(landedIndex);

        /// <summary>Raised when the reel has settled after a spin.</summary>
        public event Action<ReelController> Stopped;

        /// <param name="slotConfig">Shared machine settings (symbols and motion tuning).</param>
        /// <param name="random">RNG used only for cosmetic filler symbols, never for results.</param>
        /// <param name="initialSymbol">Symbol shown on the payline before the first spin.</param>
        public void Initialize(SlotConfig slotConfig, IRandomProvider random, int initialSymbol)
        {
            config = slotConfig;
            cosmeticRandom = random;
            strip.Clear();
            position = 0f;
            landedIndex = 0;
            strip[0] = initialSymbol;
            RefreshCells();
        }

        public void StartSpin()
        {
            if (IsSpinning)
            {
                return;
            }

            ClearHighlight();
            stopRequested = false;
            IsSpinning = true;
            motionRoutine = StartCoroutine(SpinRoutine());
        }

        /// <summary>
        /// Asks the reel to decelerate and land <paramref name="symbolIndex"/> on the payline.
        /// </summary>
        /// <param name="quick">Use the short stopping distance (player pressed STOP).</param>
        public void RequestStop(int symbolIndex, bool quick)
        {
            if (!IsSpinning)
            {
                return;
            }

            requestedSymbol = symbolIndex;
            requestedQuickStop = quick;
            stopRequested = true;
        }

        /// <summary>Pulses the payline symbol and dims the others to make the win readable.</summary>
        public void PlayWinHighlight()
        {
            ClearHighlight();
            highlightRoutine = StartCoroutine(HighlightRoutine());
        }

        public void ClearHighlight()
        {
            if (highlightRoutine != null)
            {
                StopCoroutine(highlightRoutine);
                highlightRoutine = null;
            }

            foreach (Image cell in cells)
            {
                cell.color = Color.white;
                cell.rectTransform.localScale = Vector3.one;
            }
        }

        private IEnumerator SpinRoutine()
        {
            // 1) Wind-up: pull the reel back slightly for anticipation, like a real handle pull.
            float start = position;
            for (float t = 0f; t < config.WindUpDuration; t += Time.deltaTime)
            {
                position = start - config.WindUpDistance * Easing.OutQuad(t / config.WindUpDuration);
                currentSpeed = 0f;
                RefreshCells();
                yield return null;
            }

            // 2) Accelerate up to full speed.
            for (float t = 0f; t < config.AccelerationDuration; t += Time.deltaTime)
            {
                currentSpeed = config.ReelSpeed * Easing.InQuad(t / config.AccelerationDuration);
                Advance(currentSpeed * Time.deltaTime);
                yield return null;
            }

            // 3) Spin at full speed until the game logic tells us where to land.
            currentSpeed = config.ReelSpeed;
            while (!stopRequested)
            {
                Advance(currentSpeed * Time.deltaTime);
                yield return null;
            }

            // 4) Decelerate onto the target. Plant the result a few cells above the payline,
            //    out of view, then ease into it with no visible pop.
            int distanceInCells = requestedQuickStop ? config.QuickStopDistance : config.StopDistance;
            int targetIndex = Mathf.CeilToInt(position) + distanceInCells;
            strip[targetIndex] = requestedSymbol;

            float decelStart = position;
            float overshootTarget = targetIndex + config.BounceOvershoot;
            float decelDistance = overshootTarget - decelStart;

            // OutCubic starts at slope 3, so duration = 3 * distance / speed keeps velocity continuous.
            float decelDuration = 3f * decelDistance / Mathf.Max(0.01f, config.ReelSpeed);

            for (float t = 0f; t < decelDuration; t += Time.deltaTime)
            {
                float normalized = t / decelDuration;
                position = decelStart + decelDistance * Easing.OutCubic(normalized);
                currentSpeed = config.ReelSpeed * (1f - normalized);
                RefreshCells();
                yield return null;
            }

            // 5) Bounce: overshoot slightly past the payline, then settle back. Gives a mechanical "clunk".
            currentSpeed = 0f;
            for (float t = 0f; t < config.BounceDuration; t += Time.deltaTime)
            {
                position = Mathf.Lerp(overshootTarget, targetIndex, Easing.InOutSine(t / config.BounceDuration));
                RefreshCells();
                yield return null;
            }

            position = targetIndex;
            landedIndex = targetIndex;
            RefreshCells();
            TrimStrip();

            IsSpinning = false;
            motionRoutine = null;
            Stopped?.Invoke(this);
        }

        private IEnumerator HighlightRoutine()
        {
            Image paylineCell = null;
            int baseIndex = Mathf.FloorToInt(position) - cells.Length / 2;

            for (int i = 0; i < cells.Length; i++)
            {
                bool onPayline = baseIndex + i == landedIndex;
                cells[i].color = onPayline ? Color.white : dimmedColor;
                if (onPayline)
                {
                    paylineCell = cells[i];
                }
            }

            if (paylineCell == null)
            {
                yield break;
            }

            // Three gentle scale pulses on the winning symbol.
            const float pulseDuration = 0.45f;
            const int pulseCount = 3;
            for (int pulse = 0; pulse < pulseCount; pulse++)
            {
                for (float t = 0f; t < pulseDuration; t += Time.deltaTime)
                {
                    float scale = 1f + 0.18f * Mathf.Sin(Mathf.PI * (t / pulseDuration));
                    paylineCell.rectTransform.localScale = new Vector3(scale, scale, 1f);
                    yield return null;
                }
            }

            paylineCell.rectTransform.localScale = Vector3.one;
            highlightRoutine = null;
        }

        private void Advance(float deltaCells)
        {
            position += deltaCells;
            RefreshCells();
        }

        /// <summary>Places the pooled cells on the strip indices around the current position.</summary>
        private void RefreshCells()
        {
            if (config == null || cells == null)
            {
                return;
            }

            int half = cells.Length / 2;
            int baseIndex = Mathf.FloorToInt(position) - half;

            // Fake motion blur: stretch symbols vertically in proportion to speed.
            float speedRatio = config.ReelSpeed > 0f ? currentSpeed / config.ReelSpeed : 0f;
            float stretch = 1f + config.MotionStretch * speedRatio;

            for (int i = 0; i < cells.Length; i++)
            {
                int stripIndex = baseIndex + i;
                Image cell = cells[i];

                Sprite sprite = config.Symbols[GetSymbolAt(stripIndex)].Sprite;
                if (cell.sprite != sprite)
                {
                    cell.sprite = sprite;
                }

                cell.rectTransform.anchoredPosition = new Vector2(0f, (stripIndex - position) * cellHeight);

                if (highlightRoutine == null)
                {
                    cell.rectTransform.localScale = new Vector3(1f, stretch, 1f);
                }
            }
        }

        private int GetSymbolAt(int stripIndex)
        {
            if (!strip.TryGetValue(stripIndex, out int symbol))
            {
                // Cosmetic filler only. The real result is injected in RequestStop.
                symbol = cosmeticRandom.Next(config.Symbols.Count);
                strip[stripIndex] = symbol;
            }

            return symbol;
        }

        /// <summary>Drops strip entries far below the window so the dictionary never grows unbounded.</summary>
        private void TrimStrip()
        {
            int lowestVisible = Mathf.FloorToInt(position) - cells.Length;
            var expired = new List<int>();

            foreach (int index in strip.Keys)
            {
                if (index < lowestVisible)
                {
                    expired.Add(index);
                }
            }

            foreach (int index in expired)
            {
                strip.Remove(index);
            }
        }
    }
}
