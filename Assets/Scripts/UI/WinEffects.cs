using System.Collections;
using System.Collections.Generic;
using SlotGame.Core;
using SlotGame.Game;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// Win "juice": the payline flashes, coins burst out of the reels, and the cabinet
    /// shakes on big wins. Everything is cosmetic and driven by SlotMachine events.
    ///
    /// Coins are a fixed pool of UI Images simulated in Update (velocity + gravity), so a
    /// burst allocates nothing. The flip effect is a scale on X following |cos(t)|.
    /// </summary>
    public sealed class WinEffects : MonoBehaviour
    {
        private sealed class Coin
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Age;
            public float Lifetime;
            public float FlipSpeed;
            public float Size;
            public bool Active;
        }

        [SerializeField] private SlotMachine machine;

        [Header("Payline flash")]
        [SerializeField] private Image payline;
        [SerializeField] private Color flashColor = new Color(1f, 0.92f, 0.35f, 1f);
        [SerializeField] private float flashInterval = 0.12f;

        [Header("Coins")]
        [SerializeField] private RectTransform coinLayer;
        [SerializeField] private Image coinTemplate;
        [Tooltip("Where coins spawn, in coin layer local space (the centre of the reels).")]
        [SerializeField] private Vector2 burstOrigin;
        [SerializeField] private int coinsSmallWin = 14;
        [SerializeField] private int coinsBigWin = 36;
        [SerializeField] private int coinsJackpot = 70;
        [SerializeField] private float gravity = 1800f;

        [Header("Shake (big wins only)")]
        [SerializeField] private RectTransform shakeTarget;
        [SerializeField] private float shakeDuration = 0.5f;
        [SerializeField] private float shakeStrength = 14f;

        private readonly List<Coin> coins = new List<Coin>();
        private Color paylineBaseColor;
        private Vector2 shakeBasePosition;
        private Coroutine flashRoutine;
        private Coroutine shakeRoutine;

        private void Awake()
        {
            paylineBaseColor = payline.color;
            shakeBasePosition = shakeTarget.anchoredPosition;
            coinTemplate.gameObject.SetActive(false);

            // Pre-build the pool once so a jackpot burst never allocates.
            for (int i = 0; i < coinsJackpot; i++)
            {
                Image image = Instantiate(coinTemplate, coinLayer);
                image.name = "Coin" + i;
                coins.Add(new Coin { Rect = image.rectTransform, Image = image });
            }
        }

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

        private void HandleSpinStarted()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            payline.color = paylineBaseColor;
        }

        private void HandleSpinCompleted(SpinReport report)
        {
            if (!report.IsWin)
            {
                return;
            }

            bool isBig = report.IsJackpot || report.TotalWin >= report.Bet * machine.Config.BigWinMultiplier;

            flashRoutine = StartCoroutine(FlashPayline(isBig ? 12 : 6));
            Burst(report.IsJackpot ? coinsJackpot : isBig ? coinsBigWin : coinsSmallWin);

            if (isBig)
            {
                if (shakeRoutine != null)
                {
                    StopCoroutine(shakeRoutine);
                }

                shakeRoutine = StartCoroutine(Shake());
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            foreach (Coin coin in coins)
            {
                if (!coin.Active)
                {
                    continue;
                }

                coin.Age += deltaTime;
                if (coin.Age >= coin.Lifetime)
                {
                    coin.Active = false;
                    coin.Image.gameObject.SetActive(false);
                    continue;
                }

                coin.Velocity.y -= gravity * deltaTime;
                coin.Rect.anchoredPosition += coin.Velocity * deltaTime;

                // Fake 3D flip by squashing on X.
                float flip = Mathf.Abs(Mathf.Cos(coin.Age * coin.FlipSpeed));
                coin.Rect.localScale = new Vector3(coin.Size * Mathf.Max(0.15f, flip), coin.Size, 1f);

                // Fade out over the last 30% of the coin's life.
                float lifeRatio = coin.Age / coin.Lifetime;
                Color color = coin.Image.color;
                color.a = lifeRatio < 0.7f ? 1f : 1f - (lifeRatio - 0.7f) / 0.3f;
                coin.Image.color = color;
            }
        }

        private void Burst(int count)
        {
            // UnityEngine.Random is fine here: this is purely cosmetic, never gameplay.
            int spawned = 0;
            foreach (Coin coin in coins)
            {
                if (spawned >= count)
                {
                    break;
                }

                if (coin.Active)
                {
                    continue;
                }

                coin.Active = true;
                coin.Age = 0f;
                coin.Lifetime = Random.Range(1.3f, 2f);
                coin.FlipSpeed = Random.Range(6f, 14f);
                coin.Size = Random.Range(0.7f, 1.15f);
                coin.Velocity = new Vector2(Random.Range(-520f, 520f), Random.Range(650f, 1150f));
                coin.Rect.anchoredPosition = burstOrigin + new Vector2(Random.Range(-160f, 160f), Random.Range(-40f, 40f));
                coin.Image.color = Color.white;
                coin.Image.gameObject.SetActive(true);
                spawned++;
            }
        }

        private IEnumerator FlashPayline(int flashes)
        {
            for (int i = 0; i < flashes * 2; i++)
            {
                payline.color = i % 2 == 0 ? flashColor : paylineBaseColor;
                yield return new WaitForSeconds(flashInterval);
            }

            payline.color = paylineBaseColor;
            flashRoutine = null;
        }

        private IEnumerator Shake()
        {
            for (float t = 0f; t < shakeDuration; t += Time.deltaTime)
            {
                float strength = shakeStrength * (1f - t / shakeDuration);
                shakeTarget.anchoredPosition = shakeBasePosition + Random.insideUnitCircle * strength;
                yield return null;
            }

            shakeTarget.anchoredPosition = shakeBasePosition;
            shakeRoutine = null;
        }
    }
}
