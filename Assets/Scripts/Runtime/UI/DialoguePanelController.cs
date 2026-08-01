using System;
using System.Threading;
using Jagara.Runtime.Data;
using Jagara.Runtime.Narrative.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Plays a DialogueSO as a modal panel: speaker name, optional portrait, and
    /// the line revealed character by character. Confirm completes the line if it
    /// is still typing, otherwise advances; past the last line the panel closes
    /// itself. While it is open it holds a block on GameplayInputGateSO, which is
    /// what stops the player from walking and the action menu from opening.
    /// </summary>
    /// <remarks>
    /// Runs its Update() ahead of ActionMenuController (-100) so a Confirm press
    /// consumed by a dialogue can never also be read as a menu confirmation in the
    /// same frame.
    /// </remarks>
    [DefaultExecutionOrder(-200)]
    public class DialoguePanelController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset controlsAsset;
        [SerializeField] private GameplayInputGateSO inputGate;
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text lineText;
        [SerializeField] private Image portraitImage;

        [Tooltip("Shown once a line has finished typing, to signal that Confirm advances.")]
        [SerializeField] private GameObject advanceIndicator;

        [Tooltip("Characters revealed per second. 0 or less shows each line instantly.")]
        [SerializeField] private float charactersPerSecond = 40f;

        private InputAction confirmAction;

        private DialogueSO dialogue;
        private int lineIndex;
        private bool isTyping;
        private bool blockPushed;

        private CancellationTokenSource typingCts;

        /// <summary>True from Play() until the panel closes. Readable while the panel is inactive.</summary>
        public bool IsPlaying { get; private set; }

        public event Action OnDialogueFinished = delegate { };

        private void Awake()
        {
            var map = controlsAsset.FindActionMap("Menu", throwIfNotFound: true);
            confirmAction = map.FindAction("Confirm", throwIfNotFound: true);

            if (inputGate == null)
            {
                Debug.LogError($"DialoguePanelController on {name}: inputGate reference is not assigned; the player will be able to move during dialogue.");
            }
        }

        /// <summary>
        /// Opens the panel on the first line of <paramref name="dialogue"/>. Safe
        /// to call while the panel is inactive - activating it runs Awake/OnEnable
        /// synchronously, before the rest of this method needs them.
        /// </summary>
        public void Play(DialogueSO dialogue)
        {
            if (dialogue == null)
            {
                Debug.LogError($"DialoguePanelController on {name}: Play called with a null dialogue.");
                return;
            }

            if (dialogue.LineCount == 0)
            {
                Debug.LogError($"DialoguePanelController on {name}: dialogue '{dialogue.name}' has no lines; nothing to play.");
                return;
            }

            this.dialogue = dialogue;
            lineIndex = 0;

            gameObject.SetActive(true);
            IsPlaying = true;

            // Enabled here rather than left to ActionMenuController: a dialogue
            // must also work in scenes that have no action menu (the hub). Never
            // disabled again - the Menu map's actions are shared, and disabling
            // Confirm on close would break every menu that reads the same instance.
            confirmAction.Enable();

            if (inputGate != null && !blockPushed)
            {
                inputGate.PushBlock();
                blockPushed = true;
            }

            ShowCurrentLine();
        }

        /// <summary>Closes the panel early (teardown happens in OnDisable).</summary>
        public void Close()
        {
            if (!IsPlaying)
            {
                return;
            }

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            CancelTyping();

            if (blockPushed)
            {
                inputGate.PopBlock();
                blockPushed = false;
            }

            // Also covers the panel being disabled from outside (scene teardown),
            // so the gate can never be left closed with no dialogue on screen.
            if (IsPlaying)
            {
                IsPlaying = false;
                OnDialogueFinished();
            }
        }

        private void Update()
        {
            if (!IsPlaying || !confirmAction.WasPressedThisFrame())
            {
                return;
            }

            if (isTyping)
            {
                CancelTyping();
                CompleteLine();
                return;
            }

            Advance();
        }

        private void Advance()
        {
            lineIndex++;
            if (lineIndex >= dialogue.LineCount)
            {
                Close();
                return;
            }

            ShowCurrentLine();
        }

        private void ShowCurrentLine()
        {
            DialogueLine line = dialogue.Lines[lineIndex];
            SpeakerSO speaker = line.Speaker;

            if (speakerNameText != null)
            {
                speakerNameText.text = speaker != null ? speaker.DisplayName : string.Empty;
            }

            ApplyPortrait(speaker);

            string text = line.Text ?? string.Empty;
            if (lineText != null)
            {
                // The full string is laid out once and revealed via
                // maxVisibleCharacters, so typing costs no re-layout per frame.
                lineText.text = text;
                lineText.maxVisibleCharacters = 0;
            }

            if (advanceIndicator != null)
            {
                advanceIndicator.SetActive(false);
            }

            if (charactersPerSecond <= 0f || text.Length == 0)
            {
                CompleteLine();
                return;
            }

            // Cancel any still-running typing pass from the previous line before
            // starting this one, so two passes can't drive maxVisibleCharacters.
            CancelTyping();
            isTyping = true;
            typingCts = new CancellationTokenSource();
            _ = TypeLineAsync(text.Length, typingCts.Token);
        }

        private async Awaitable TypeLineAsync(int totalCharacters, CancellationToken token)
        {
            float elapsed = 0f;
            int shown = 0;

            try
            {
                while (shown < totalCharacters)
                {
                    await Awaitable.NextFrameAsync(token);

                    elapsed += Time.deltaTime;
                    int target = Mathf.Min(totalCharacters, Mathf.FloorToInt(elapsed * charactersPerSecond));
                    if (target != shown)
                    {
                        shown = target;
                        lineText.maxVisibleCharacters = shown;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Line was completed early by Confirm, or the panel closed.
                return;
            }

            CompleteLine();
        }

        private void CompleteLine()
        {
            isTyping = false;

            if (lineText != null)
            {
                lineText.maxVisibleCharacters = int.MaxValue;
            }

            if (advanceIndicator != null)
            {
                advanceIndicator.SetActive(true);
            }
        }

        private void ApplyPortrait(SpeakerSO speaker)
        {
            if (portraitImage == null)
            {
                return;
            }

            Sprite portrait = speaker != null ? speaker.Portrait : null;
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        private void CancelTyping()
        {
            if (typingCts == null)
            {
                return;
            }

            typingCts.Cancel();
            typingCts.Dispose();
            typingCts = null;
        }
    }
}
