using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Smoothly follows a target's XY position while preserving the camera's own
    /// starting Z depth. SetTarget snaps immediately by default so entering Play
    /// Mode doesn't produce a long pan-in from the camera's original scene position.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private float smoothTime = 0.15f;

        [Tooltip("Optional. When assigned, its CurrentOffset is added on top of the smoothed follow position every frame.")]
        [SerializeField] private CameraShake shake;

        private Transform target;
        private Vector3 velocity;
        private float fixedZ;

        // The follow logic's own idea of "where the camera should be" before
        // shake is applied. SmoothDamp reads/writes this instead of
        // transform.position directly - otherwise a shake offset written to
        // transform.position one frame would be read back as this frame's
        // "current" value next frame, corrupting velocity and turning a clean
        // decaying shake into drift.
        private Vector3 smoothedPosition;

        private void Awake()
        {
            fixedZ = transform.position.z;
            smoothedPosition = transform.position;
        }

        public void SetTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            if (snap && target != null)
            {
                smoothedPosition = new Vector3(target.position.x, target.position.y, fixedZ);
                velocity = Vector3.zero;
                transform.position = smoothedPosition + ShakeOffset();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desired = new Vector3(target.position.x, target.position.y, fixedZ);
            smoothedPosition = Vector3.SmoothDamp(smoothedPosition, desired, ref velocity, smoothTime);
            transform.position = smoothedPosition + ShakeOffset();
        }

        private Vector3 ShakeOffset() => shake != null ? shake.CurrentOffset : Vector3.zero;
    }
}
