using Jagara.Runtime.Events;
using UnityEngine;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// The sole consumer of PopupRequestEventSO in a gameplay scene. Spawns
    /// the floating-text prefab wherever a PopupRequest asks for it.
    /// Producers (HpPopupBinder today; future Paranoia/PP/pickup popups)
    /// never reference this class directly - they only know about the shared
    /// event asset.
    /// </summary>
    public class PopupSpawner : MonoBehaviour
    {
        [SerializeField] private PopupRequestEventSO popupRequestEvent;
        [SerializeField] private FloatingPopupText popupPrefab;

        private void Awake()
        {
            if (popupRequestEvent == null)
            {
                Debug.LogError($"PopupSpawner on {name}: popupRequestEvent reference is not assigned; no popups will be shown.");
            }

            if (popupPrefab == null)
            {
                Debug.LogError($"PopupSpawner on {name}: popupPrefab reference is not assigned; no popups will be shown.");
            }
        }

        private void OnEnable()
        {
            if (popupRequestEvent != null)
            {
                popupRequestEvent.RegisterListener(HandlePopupRequested);
            }
        }

        private void OnDisable()
        {
            if (popupRequestEvent != null)
            {
                popupRequestEvent.UnregisterListener(HandlePopupRequested);
            }
        }

        private void HandlePopupRequested(PopupRequest request)
        {
            if (popupPrefab == null)
            {
                return;
            }

            FloatingPopupText popup = Instantiate(popupPrefab, request.WorldPosition, Quaternion.identity);
            popup.Play(request.Text, request.Color);
        }
    }
}
