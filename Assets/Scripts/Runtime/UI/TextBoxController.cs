using System;
using System.Collections.Generic;
using System.Text;
using Jagara.Runtime.Narrative;
using TMPro;
using UnityEngine;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Sole owner of the shared text box at the bottom of the screen. Two
    /// unrelated things want to write there - the menus' "what does the option
    /// under the cursor do" description, and the running action log - so this
    /// class arbitrates between them instead of letting both assign the same
    /// TMP_Text and overwrite each other.
    /// <para>
    /// Priority: while any menu has a description set, that wins. Otherwise the
    /// current window of log messages is shown, newest at the bottom.
    /// </para>
    /// <para>
    /// The window is deliberately NOT a view onto the log's history: it is its
    /// own short-lived list, emptied both by <see cref="clearAfterSeconds"/> of
    /// silence and by <see cref="ClearDescription"/>. Once cleared, those lines
    /// are gone for good - the next message starts a fresh window rather than
    /// dragging minutes-old lines back onto the screen with it.
    /// </para>
    /// <para>
    /// With nothing to show - no description and an empty window - the whole box
    /// is hidden via <see cref="canvasGroup"/> rather than left on screen empty.
    /// Alpha is used instead of SetActive because this component lives on the box
    /// it controls: deactivating that GameObject would stop its own Update() and
    /// unsubscribe it from the log, and it could never bring itself back.
    /// </para>
    /// </summary>
    public class TextBoxController : MonoBehaviour
    {
        [Serializable]
        private struct CategoryColor
        {
            public MessageCategory category;
            public Color color;
        }

        [SerializeField] private TMP_Text label;
        [SerializeField] private MessageLogSO messageLog;

        [Tooltip("Faded out whenever the box has nothing to show. Must be on the box's own root.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("How many recent log lines fit in the box at once.")]
        [SerializeField] private int visibleLineCount = 3;

        [Tooltip("Seconds without a new message before the log window clears.")]
        [SerializeField] private float clearAfterSeconds = 4f;

        [Tooltip("Optional per-category tinting. Categories with no entry render in the label's own color.")]
        [SerializeField] private CategoryColor[] categoryColors;

        // null means "no menu is describing anything right now" - distinct from
        // an empty string, which is a menu deliberately showing a blank line.
        private string description;

        // The lines currently on screen. Owned here, not read from the log's
        // history, so clearing the box actually forgets them.
        private readonly List<GameMessage> window = new();
        private float lastMessageTime;

        private readonly StringBuilder builder = new();

        private void OnEnable()
        {
            if (label == null)
            {
                Debug.LogError($"TextBoxController on {name}: label reference is not assigned; no text will ever be shown.");
            }

            if (canvasGroup == null)
            {
                Debug.LogError($"TextBoxController on {name}: canvasGroup reference is not assigned; the box will stay visible even with nothing to show.");
            }

            if (messageLog != null)
            {
                messageLog.OnMessagePosted += HandleMessagePosted;
            }
            else
            {
                Debug.LogError($"TextBoxController on {name}: messageLog reference is not assigned; action log messages will not appear.");
            }

            Render();
        }

        private void OnDisable()
        {
            if (messageLog != null)
            {
                messageLog.OnMessagePosted -= HandleMessagePosted;
            }
        }

        /// <summary>Takes over the box with a menu description until <see cref="ClearDescription"/>.</summary>
        public void SetDescription(string text)
        {
            description = text ?? string.Empty;
            Render();
        }

        /// <summary>
        /// Releases the box. Any messages still on screen are discarded with it -
        /// by the time a menu has been opened and closed over them they are stale,
        /// and restoring them would put lines back on screen out of step with what
        /// the player is doing.
        /// </summary>
        public void ClearDescription()
        {
            description = null;
            ClearWindow();
        }

        private void HandleMessagePosted(GameMessage message)
        {
            window.Add(message);

            // Only the newest lines are ever rendered, so older ones are dropped
            // here rather than letting the window grow for a whole session.
            int maxLines = Mathf.Max(1, visibleLineCount);
            if (window.Count > maxLines)
            {
                window.RemoveRange(0, window.Count - maxLines);
            }

            lastMessageTime = Time.time;
            Render();
        }

        private void Update()
        {
            if (window.Count == 0 || Time.time - lastMessageTime < clearAfterSeconds)
            {
                return;
            }

            ClearWindow();
        }

        private void ClearWindow()
        {
            window.Clear();
            Render();
        }

        private void Render()
        {
            bool hasContent = description != null || window.Count > 0;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = hasContent ? 1f : 0f;
            }

            if (label == null)
            {
                return;
            }

            if (description != null)
            {
                label.text = description;
                return;
            }

            label.text = window.Count > 0 ? BuildLogWindow() : string.Empty;
        }

        private string BuildLogWindow()
        {
            builder.Clear();
            for (int i = 0; i < window.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                AppendMessage(window[i]);
            }

            return builder.ToString();
        }

        private void AppendMessage(GameMessage message)
        {
            if (!TryGetCategoryColor(message.Category, out Color color))
            {
                builder.Append(message.Text);
                return;
            }

            // Nests correctly around any per-name tint the log already applied -
            // TMP keeps a colour stack, so the inner </color> pops back to this one.
            builder.Append(MessageFormatter.Colorize(message.Text, color));
        }

        private bool TryGetCategoryColor(MessageCategory category, out Color color)
        {
            if (categoryColors != null)
            {
                for (int i = 0; i < categoryColors.Length; i++)
                {
                    if (categoryColors[i].category == category)
                    {
                        color = categoryColors[i].color;
                        return true;
                    }
                }
            }

            color = default;
            return false;
        }
    }
}
