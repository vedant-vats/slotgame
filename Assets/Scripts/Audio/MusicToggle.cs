using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.Audio
{
    /// <summary>Button that turns the background music on and off.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class MusicToggle : MonoBehaviour
    {
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private Text label;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Toggle);
        }

        private void Start()
        {
            Refresh();
        }

        private void Toggle()
        {
            audioManager.SetMusicEnabled(!audioManager.MusicEnabled);
            Refresh();
        }

        private void Refresh()
        {
            label.text = audioManager.MusicEnabled ? "MUSIC: ON" : "MUSIC: OFF";
        }
    }
}
