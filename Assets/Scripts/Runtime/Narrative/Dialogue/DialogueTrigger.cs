using Jagara.Runtime.UI;
using UnityEngine;

namespace Jagara.Runtime.Narrative.Dialogue
{
    /// <summary>
    /// Starts a conversation on the shared dialogue panel.
    /// </summary>
    /// <remarks>
    /// PLACEHOLDER: the GDD does not yet define how NPCs start conversations
    /// ([PENDIENTE]), so this is the smallest thing that makes the dialogue system
    /// verifiable - playOnStart for manual testing, plus a public Play() for
    /// whatever the real trigger turns out to be (interaction key, tile entry,
    /// hub cutscene). Do not treat this as the final design.
    /// </remarks>
    public class DialogueTrigger : MonoBehaviour
    {
        [SerializeField] private DialogueSO dialogue;
        [SerializeField] private DialoguePanelController panel;

        [Tooltip("Testing aid: play this dialogue as soon as the scene starts.")]
        [SerializeField] private bool playOnStart;

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        public void Play()
        {
            if (panel == null)
            {
                Debug.LogError($"DialogueTrigger on {name}: panel reference is not assigned; cannot play dialogue.");
                return;
            }

            panel.Play(dialogue);
        }
    }
}
