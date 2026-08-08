using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Mirrors this transform's local X offset when the tracked
    /// SpriteRenderer's flipX is set. Character facing (see
    /// GridVisualAnimator) is done by flipping the SpriteRenderer, which
    /// mirrors the drawn sprite but not descendant transforms - without
    /// this, a torch-cup particle emitter offset to one side would stay
    /// there when the character turns around, drifting away from the drawn
    /// torch. Deliberately separate from GridVisualAnimator, which never
    /// touches its own children.
    /// </summary>
    public class TorchFlameFlipMirror : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer trackedSpriteRenderer;

        private float baseLocalX;
        private bool lastFlipX;
        private bool initialized;

        private void Awake()
        {
            baseLocalX = transform.localPosition.x;

            if (trackedSpriteRenderer == null)
            {
                Debug.LogError("TorchFlameFlipMirror: trackedSpriteRenderer reference is not assigned.");
            }
        }

        private void LateUpdate()
        {
            if (trackedSpriteRenderer == null)
            {
                return;
            }

            bool flipX = trackedSpriteRenderer.flipX;
            if (initialized && flipX == lastFlipX)
            {
                return;
            }

            Vector3 pos = transform.localPosition;
            pos.x = flipX ? -baseLocalX : baseLocalX;
            transform.localPosition = pos;

            lastFlipX = flipX;
            initialized = true;
        }
    }
}
