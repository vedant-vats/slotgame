using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// Twinkling stars scattered over the background. Each star fades and scales with its own
    /// speed and phase, so the field never pulses in sync.
    /// </summary>
    public sealed class SparkleField : MonoBehaviour
    {
        [SerializeField] private Image[] sparkles;
        [SerializeField] private Vector2 speedRange = new Vector2(0.4f, 1.2f);
        [SerializeField] private float maxAlpha = 0.85f;

        private float[] speeds;
        private float[] phases;
        private float[] baseScales;

        private void Awake()
        {
            speeds = new float[sparkles.Length];
            phases = new float[sparkles.Length];
            baseScales = new float[sparkles.Length];

            for (int i = 0; i < sparkles.Length; i++)
            {
                // Cosmetic randomness only.
                speeds[i] = Random.Range(speedRange.x, speedRange.y);
                phases[i] = Random.Range(0f, Mathf.PI * 2f);
                baseScales[i] = sparkles[i].rectTransform.localScale.x;
            }
        }

        private void Update()
        {
            float time = Time.time;

            for (int i = 0; i < sparkles.Length; i++)
            {
                // Sharpened sine: mostly dim, with short bright twinkles.
                float wave = Mathf.Sin(time * speeds[i] * Mathf.PI + phases[i]) * 0.5f + 0.5f;
                float twinkle = wave * wave * wave;

                Color color = sparkles[i].color;
                color.a = twinkle * maxAlpha;
                sparkles[i].color = color;

                float scale = baseScales[i] * (0.6f + 0.6f * twinkle);
                sparkles[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
