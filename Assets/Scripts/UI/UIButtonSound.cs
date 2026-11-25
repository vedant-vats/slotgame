using System;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// Put this on any button that should make a click sound. Buttons don't need to know about
    /// the audio system: they raise a static event and the AudioManager listens to it.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonSound : MonoBehaviour
    {
        public static event Action AnyButtonClicked;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            button.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(HandleClick);
        }

        private static void HandleClick()
        {
            AnyButtonClicked?.Invoke();
        }
    }
}
