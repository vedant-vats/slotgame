using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// Overlay for big wins and jackpots. The pop-in animation comes from an Animator whose
    /// default state plays when the object is activated. The amount rolls up with <see cref="CountUpText"/>.
    /// </summary>
    public sealed class WinPopup : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private CountUpText amountText;
        [SerializeField] private Button closeButton;
        [Tooltip("Clicking the dimmed background also closes the popup.")]
        [SerializeField] private Button backgroundButton;
        [SerializeField] private float countDuration = 1.2f;
        [SerializeField] private float autoHideAfter = 3f;

        private Coroutine autoHideRoutine;

        public bool IsVisible => gameObject.activeSelf;

        private void Awake()
        {
            closeButton.onClick.AddListener(Hide);
            if (backgroundButton != null)
            {
                backgroundButton.onClick.AddListener(Hide);
            }
        }

        public void Show(string title, int amount)
        {
            gameObject.SetActive(true);
            titleText.text = title;
            amountText.AnimateFromZero(amount, countDuration);

            if (autoHideRoutine != null)
            {
                StopCoroutine(autoHideRoutine);
            }

            autoHideRoutine = StartCoroutine(AutoHide());
        }

        public void Hide()
        {
            if (autoHideRoutine != null)
            {
                StopCoroutine(autoHideRoutine);
                autoHideRoutine = null;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator AutoHide()
        {
            yield return new WaitForSeconds(autoHideAfter);
            autoHideRoutine = null;
            Hide();
        }
    }
}
