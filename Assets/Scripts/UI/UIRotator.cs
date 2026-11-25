using UnityEngine;

namespace SlotGame.UI
{
    /// <summary>Spins a UI element at a constant speed. Used for the sunburst rays.</summary>
    public sealed class UIRotator : MonoBehaviour
    {
        [Tooltip("Degrees per second. Negative values spin clockwise.")]
        [SerializeField] private float degreesPerSecond = 8f;

        private void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
        }
    }
}
