using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jagara.Runtime.Events
{
    /// <summary>
    /// Payload for a single popup spawn request: where it appears, what it
    /// says, and what colour it renders in. Immutable - a request is fully
    /// described at the moment it's raised.
    /// </summary>
    public readonly struct PopupRequest
    {
        public readonly Vector3 WorldPosition;
        public readonly string Text;
        public readonly Color Color;

        public PopupRequest(Vector3 worldPosition, string text, Color color)
        {
            WorldPosition = worldPosition;
            Text = text;
            Color = color;
        }
    }

    /// <summary>
    /// Shared, decoupled channel for "show a popup" requests. Any gameplay
    /// system (HP changes today; Paranoia, PP, item pickups later) raises a
    /// PopupRequest without knowing who's listening; PopupSpawner is the sole
    /// consumer in a gameplay scene. Uses a plain Action&lt;T&gt; listener list
    /// rather than the GameEventListener/UnityEvent component pattern, since
    /// there's exactly one consumer and no need for Inspector-wired responses.
    /// </summary>
    [CreateAssetMenu(fileName = "New Popup Request Event", menuName = "Events/Popup Request Event")]
    public class PopupRequestEventSO : ScriptableObject
    {
        private readonly List<Action<PopupRequest>> listeners = new();

        public void Raise(PopupRequest request)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                listeners[i](request);
            }
        }

        public void RegisterListener(Action<PopupRequest> listener) => listeners.Add(listener);
        public void UnregisterListener(Action<PopupRequest> listener) => listeners.Remove(listener);
    }
}
