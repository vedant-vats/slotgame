using System;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>Generic Yes / No dialog. Used for the "out of credits, refill?" prompt.</summary>
    public sealed class ConfirmPopup : MonoBehaviour
    {
        [SerializeField] private Text messageText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private Action onConfirm;

        public bool IsVisible => gameObject.activeSelf;

        private void Awake()
        {
            yesButton.onClick.AddListener(Confirm);
            noButton.onClick.AddListener(Hide);
        }

        public void Show(string message, Action confirmAction)
        {
            messageText.text = message;
            onConfirm = confirmAction;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            onConfirm = null;
            gameObject.SetActive(false);
        }

        private void Confirm()
        {
            // Store and clear the callback before hiding, because Hide clears it.
            Action action = onConfirm;
            Hide();
            action?.Invoke();
        }
    }
}
