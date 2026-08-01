using UnityEngine;

namespace Jagara.Runtime.Narrative.Dialogue
{
    /// <summary>
    /// Who is talking. Split from the dialogue itself so a character's name and
    /// portrait are authored once and reused across every conversation they
    /// appear in.
    /// </summary>
    [CreateAssetMenu(fileName = "New Speaker", menuName = "Jagara/Speaker")]
    public class SpeakerSO : ScriptableObject
    {
        [SerializeField] private string displayName;

        [Tooltip("Optional. There is no portrait art yet - leaving this empty hides the portrait slot.")]
        [SerializeField] private Sprite portrait;

        public string DisplayName => displayName;
        public Sprite Portrait => portrait;
    }
}
