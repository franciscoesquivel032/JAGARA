using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    [ExecuteAlways]
    public class FogOverlaySizer : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float margin = 2f;

        private float lastOrthoSize;
        private float lastAspect;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponentInParent<Camera>();
            }

            if (targetCamera == null)
            {
                Debug.LogError("FogOverlaySizer: no Camera assigned and none found in parents.");
                enabled = false;
                return;
            }

            Resize();
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                return;
            }

            if (!Mathf.Approximately(targetCamera.orthographicSize, lastOrthoSize) ||
                !Mathf.Approximately(targetCamera.aspect, lastAspect))
            {
                Resize();
            }
        }

        private void Resize()
        {
            lastOrthoSize = targetCamera.orthographicSize;
            lastAspect = targetCamera.aspect;

            float height = lastOrthoSize * 2f + margin;
            float width = lastOrthoSize * 2f * lastAspect + margin;
            transform.localScale = new Vector3(width, height, 1f);
        }
    }
}
