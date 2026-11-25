using SlotGame.Game;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// The machine's side lever. Clicking it spins (like the SPIN button), and it plays a
    /// "pull" animation every time a spin starts, whatever started the spin.
    /// </summary>
    public sealed class LeverView : MonoBehaviour
    {
        private static readonly int PullTrigger = Animator.StringToHash("Pull");

        [SerializeField] private SlotMachine machine;
        [SerializeField] private Animator animator;
        [SerializeField] private Button hitArea;

        private void Awake()
        {
            hitArea.onClick.AddListener(machine.SpinOrStop);
        }

        private void OnEnable()
        {
            machine.SpinStarted += PlayPull;
        }

        private void OnDisable()
        {
            machine.SpinStarted -= PlayPull;
        }

        private void PlayPull()
        {
            animator.SetTrigger(PullTrigger);
        }
    }
}
