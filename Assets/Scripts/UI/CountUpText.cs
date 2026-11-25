using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>Displays a number and animates it rolling up or down, like a casino meter.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class CountUpText : MonoBehaviour
    {
        [SerializeField] private string prefix = "";
        [SerializeField] private string suffix = "";

        private Text label;
        private Coroutine routine;
        private int displayedValue;

        public int DisplayedValue => displayedValue;

        private void Awake()
        {
            label = GetComponent<Text>();
        }

        public void SetImmediate(int value)
        {
            StopRoutine();
            displayedValue = value;
            Render();
        }

        public void AnimateTo(int value, float duration)
        {
            StopRoutine();

            if (!isActiveAndEnabled || duration <= 0f)
            {
                SetImmediate(value);
                return;
            }

            routine = StartCoroutine(AnimateRoutine(displayedValue, value, duration));
        }

        /// <summary>Starts from 0, for win amounts.</summary>
        public void AnimateFromZero(int value, float duration)
        {
            displayedValue = 0;
            AnimateTo(value, duration);
        }

        private IEnumerator AnimateRoutine(int from, int to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                // Ease-out so the meter slows down as it reaches the final value.
                float eased = 1f - Mathf.Pow(1f - t / duration, 3f);
                displayedValue = Mathf.RoundToInt(Mathf.Lerp(from, to, eased));
                Render();
                yield return null;
            }

            displayedValue = to;
            Render();
            routine = null;
        }

        private void StopRoutine()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private void Render()
        {
            if (label == null)
            {
                label = GetComponent<Text>();
            }

            label.text = prefix + displayedValue.ToString("N0") + suffix;
        }
    }
}
