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

        private Transform target;
        private Vector3 velocity;
        private float fixedZ;

        private void Awake() => fixedZ = transform.position.z;

        public void SetTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            if (snap && target != null)
            {
                transform.position = new Vector3(target.position.x, target.position.y, fixedZ);
                velocity = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desired = new Vector3(target.position.x, target.position.y, fixedZ);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }
    }
}
