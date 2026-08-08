using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Additive screen-shake overlay for the camera. Holds no transform state
    /// of its own and never touches transform.position directly - CameraFollow
    /// queries CurrentOffset each LateUpdate and adds it on top of its own
    /// smoothed follow position, so this component can't fight CameraFollow's
    /// SmoothDamp velocity. Math lives in CameraShakeMath (Edit Mode testable).
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private float defaultDuration = 0.2f;
        [SerializeField] private float defaultMagnitude = 0.12f;

        private float shakeStartTime = float.NegativeInfinity;
        private float duration;
        private float magnitude;

        public Vector3 CurrentOffset
        {
            get
            {
                if (duration <= 0f)
                {
                    return Vector3.zero;
                }

                float elapsed = Time.time - shakeStartTime;
                if (elapsed >= duration)
                {
                    return Vector3.zero;
                }

                Vector2 offset = CameraShakeMath.ComputeOffset(elapsed / duration, magnitude);
                return new Vector3(offset.x, offset.y, 0f);
            }
        }

        public void Shake() => Shake(defaultDuration, defaultMagnitude);

        public void Shake(float shakeDuration, float shakeMagnitude)
        {
            duration = shakeDuration;
            magnitude = shakeMagnitude;
            shakeStartTime = Time.time;
        }
    }
}
