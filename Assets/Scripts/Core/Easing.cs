using UnityEngine;

namespace SlotGame.Core
{
    /// <summary>Small set of easing curves used by the reel and UI tweens. Input t is 0..1.</summary>
    public static class Easing
    {
        public static float OutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        public static float InQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        /// <summary>
        /// Cubic ease-out. Its starting slope is 3, which the reel uses to match velocity:
        /// covering distance d in time T starts at speed 3d/T, so T = 3d / currentSpeed
        /// gives a seamless hand-off from full speed into the stop.
        /// </summary>
        public static float OutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        public static float InOutSine(float t)
        {
            t = Mathf.Clamp01(t);
            return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
        }
    }
}
