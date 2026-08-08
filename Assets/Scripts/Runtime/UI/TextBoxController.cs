using System;
using System.Collections.Generic;
using Jagara.Runtime.Narrative;
using TMPro;
using UnityEngine;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Sole owner of the shared text box at the top of the screen. Two unrelated
    /// things want to write there - the menus' "what does the option under the
    /// cursor do" description, and the running action log - so this class
    /// arbitrates between them instead of letting both assign the same TMP_Text
    /// and overwrite each other.
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
    /// Presentation: the log renders one line per <see cref="LogLineView"/> slot
    /// rather than one multi-line string, so each line can carry its own opacity.
    /// A message arriving pushes the whole stack up by one line height while the
    /// new line fades in from below and the displaced one fades out above - which
    /// is what makes two identical messages in a row read as two events instead of
    /// a frozen screen. The box's own height follows the number of lines, so a
    /// single message does not sit alone in a three-line panel.
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

        [Header("References")]
        [Tooltip("Shows menu descriptions only. The action log renders into logLines instead.")]
        [SerializeField] private TMP_Text label;

        [SerializeField] private MessageLogSO messageLog;

        [Tooltip("Faded out whenever the box has nothing to show. Must be on the box's own root.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("The box itself, whose height is animated. Defaults to this GameObject's RectTransform.")]
        [SerializeField] private RectTransform boxRect;

        [Tooltip("Parent of the log line slots. Slid upwards by one line height whenever a line is pushed off the top.")]
        [SerializeField] private RectTransform lineContainer;

        [Tooltip("One more slot than visibleLineCount: slot 0 is the outgoing line, slots 1..n the visible window, oldest first.")]
        [SerializeField] private LogLineView[] logLines;

        [Header("Log window")]
        [Tooltip("How many recent log lines fit in the box at once.")]
        [SerializeField] private int visibleLineCount = 3;

        [Tooltip("Seconds without a new message before the log window clears.")]
        [SerializeField] private float clearAfterSeconds = 4f;

        [Tooltip("Optional per-category tinting. Categories with no entry render in the label's own color.")]
        [SerializeField] private CategoryColor[] categoryColors;

        [Header("Animation")]
        [Tooltip("Height of one log line, in canvas units. Must match the slots' own height.")]
        [SerializeField] private float lineHeight = 30f;

        [Tooltip("Space above and below the text, added to the box's animated height.")]
        [SerializeField] private float verticalPadding = 24f;

        [Tooltip("Seconds the stack takes to slide up by one line when a message pushes the oldest one out.")]
        [SerializeField] private float slideDuration = 0.14f;

        [Tooltip("How briskly the box's height settles on its target. Higher is snappier.")]
        [SerializeField] private float heightSpeed = 18f;

        [Tooltip("How briskly each line's opacity settles on its target. Higher is snappier.")]
        [SerializeField] private float alphaSpeed = 14f;

        [Tooltip("Opacity by age, newest first. Ages past the end reuse the last entry.")]
        [SerializeField] private float[] lineAlphas = { 1f, 0.6f, 0.35f };

        // null means "no menu is describing anything right now" - distinct from
        // an empty string, which is a menu deliberately showing a blank line.
        private string description;

        // The lines currently on screen. Owned here, not read from the log's
        // history, so clearing the box actually forgets them.
        private readonly List<GameMessage> window = new();
        private float lastMessageTime;

        // 1 means "at rest"; a message that pushes a line out restarts it at 0.
        private float slideProgress = 1f;

        private float currentHeight;
        private float targetHeight;

        /// <summary>
        /// Visible lines the box can actually show: the serialized count, capped by
        /// how many slots exist so a mis-sized array clips the window instead of
        /// silently dropping messages into slots that aren't there.
        /// </summary>
        private int MaxLines
        {
            get
            {
                int max = Mathf.Max(1, visibleLineCount);
                if (logLines != null && logLines.Length > 1)
                {
                    max = Mathf.Min(max, logLines.Length - 1);
                }

                return max;
            }
        }

        private void OnEnable()
        {
            if (boxRect == null)
            {
                boxRect = transform as RectTransform;
            }

            if (label == null)
            {
                Debug.LogError($"TextBoxController on {name}: label reference is not assigned; menu descriptions will never be shown.");
            }

            if (canvasGroup == null)
            {
                Debug.LogError($"TextBoxController on {name}: canvasGroup reference is not assigned; the box will stay visible even with nothing to show.");
            }

            if (lineContainer == null)
            {
                Debug.LogError($"TextBoxController on {name}: lineContainer reference is not assigned; log lines will not slide when a message arrives.");
            }

            if (logLines == null || logLines.Length < 2)
            {
                Debug.LogError($"TextBoxController on {name}: logLines needs visibleLineCount + 1 slots (slot 0 is the outgoing line); action log messages will not appear.");
            }
            else if (logLines.Length != Mathf.Max(1, visibleLineCount) + 1)
            {
                Debug.LogError($"TextBoxController on {name}: logLines has {logLines.Length} slots but visibleLineCount is {visibleLineCount}; expected {Mathf.Max(1, visibleLineCount) + 1}. The window will be capped at {MaxLines} lines.");
            }

            if (messageLog != null)
            {
                messageLog.OnMessagePosted += HandleMessagePosted;
            }
            else
            {
                Debug.LogError($"TextBoxController on {name}: messageLog reference is not assigned; action log messages will not appear.");
            }

            slideProgress = 1f;
            ApplySlide();
            Render();
            SnapToTargets();
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
            int maxLines = MaxLines;

            // Captured before the trim below, because the line being pushed out is
            // what slot 0 shows on its way off the top of the box.
            bool pushedOneOut = window.Count >= maxLines;
            GameMessage outgoing = pushedOneOut ? window[0] : default;

            window.Add(message);

            // Only the newest lines are ever rendered, so older ones are dropped
            // here rather than letting the window grow for a whole session.
            if (window.Count > maxLines)
            {
                window.RemoveRange(0, window.Count - maxLines);
            }

            lastMessageTime = Time.time;

            // Several messages can land in one turn. Finishing the previous slide
            // before starting this one keeps each message a discrete visible step
            // instead of letting the last arrival swallow the others' movement.
            if (slideProgress < 1f)
            {
                CompleteSlide();
            }

            Render();

            // A message posted under an open menu is only recorded, not shown, so
            // there is nothing to animate until the menu releases the box.
            if (description == null)
            {
                BeginEntryAnimation(pushedOneOut, outgoing);
            }
        }

        private void Update()
        {
            if (window.Count > 0 && Time.time - lastMessageTime >= clearAfterSeconds)
            {
                ClearWindow();
            }

            AdvanceSlide(Time.deltaTime);
            AdvanceAlphas(Time.deltaTime);
            AdvanceHeight(Time.deltaTime);
        }

        private void ClearWindow()
        {
            window.Clear();
            slideProgress = 1f;
            ApplySlide();
            Render();
        }

        private void Render()
        {
            bool hasContent = description != null || window.Count > 0;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = hasContent ? 1f : 0f;
            }

            if (description != null)
            {
                FillSlots(0);
                ClearSlot(0);

                if (label != null)
                {
                    label.text = description;
                }

                targetHeight = ComputeDescriptionHeight();
                return;
            }

            if (label != null)
            {
                label.text = string.Empty;
            }

            FillSlots(window.Count);
            targetHeight = TextBoxAnimator.ComputeTargetHeight(window.Count, MaxLines, lineHeight, verticalPadding);

            if (!hasContent)
            {
                // Nothing on screen and nothing on its way in: collapse now rather
                // than animating a box nobody can see, so the next message grows the
                // box out of a sliver instead of revealing it already expanded.
                ClearSlot(0);
                SnapToTargets();
            }
        }

        /// <summary>
        /// Writes the window into slots 1..<paramref name="filledCount"/> and blanks
        /// the rest. Slot 0 is left alone: it belongs to the slide animation, which
        /// owns both its text and the moment it is cleared.
        /// </summary>
        private void FillSlots(int filledCount)
        {
            if (logLines == null)
            {
                return;
            }

            for (int slot = 1; slot < logLines.Length; slot++)
            {
                if (logLines[slot] == null)
                {
                    continue;
                }

                logLines[slot].SetText(slot <= filledCount ? FormatLine(window[slot - 1]) : string.Empty);
            }
        }

        private void ClearSlot(int slot)
        {
            if (logLines != null && slot < logLines.Length && logLines[slot] != null)
            {
                logLines[slot].Clear();
            }
        }

        /// <summary>
        /// Kicks off the movement for a freshly posted message: the new line fades
        /// up from nothing, and if it displaced one, the whole stack slides up while
        /// the displaced line fades out above the mask.
        /// </summary>
        private void BeginEntryAnimation(bool pushedOneOut, GameMessage outgoing)
        {
            SetSlotAlpha(window.Count, 0f);

            if (!pushedOneOut || logLines == null || logLines.Length == 0)
            {
                return;
            }

            if (logLines[0] != null)
            {
                logLines[0].SetText(FormatLine(outgoing));
                // Starts at the opacity it had as the oldest visible line, so it
                // fades out from where the eye last saw it rather than flashing.
                logLines[0].Alpha = TextBoxAnimator.ComputeLineAlpha(1, window.Count, lineAlphas);
            }

            slideProgress = 0f;
            ApplySlide();
        }

        private void AdvanceSlide(float deltaTime)
        {
            if (slideProgress >= 1f)
            {
                return;
            }

            if (slideDuration <= 0f)
            {
                CompleteSlide();
                return;
            }

            slideProgress = Mathf.Min(1f, slideProgress + deltaTime / slideDuration);
            ApplySlide();

            if (slideProgress >= 1f)
            {
                ClearSlot(0);
            }
        }

        private void CompleteSlide()
        {
            slideProgress = 1f;
            ApplySlide();
            ClearSlot(0);
        }

        private void ApplySlide()
        {
            if (lineContainer == null)
            {
                return;
            }

            Vector2 position = lineContainer.anchoredPosition;
            position.y = TextBoxAnimator.ComputeSlideOffset(slideProgress, lineHeight);
            lineContainer.anchoredPosition = position;
        }

        private void AdvanceAlphas(float deltaTime)
        {
            if (logLines == null)
            {
                return;
            }

            int filledCount = description != null ? 0 : window.Count;

            for (int slot = 0; slot < logLines.Length; slot++)
            {
                if (logLines[slot] == null)
                {
                    continue;
                }

                float target = TextBoxAnimator.ComputeLineAlpha(slot, filledCount, lineAlphas);
                logLines[slot].Alpha = TextBoxAnimator.Approach(logLines[slot].Alpha, target, alphaSpeed, deltaTime);
            }
        }

        private void AdvanceHeight(float deltaTime)
        {
            currentHeight = TextBoxAnimator.Approach(currentHeight, targetHeight, heightSpeed, deltaTime);
            ApplyHeight();
        }

        private void SnapToTargets()
        {
            currentHeight = targetHeight;
            ApplyHeight();

            if (logLines == null)
            {
                return;
            }

            int filledCount = description != null ? 0 : window.Count;
            for (int slot = 0; slot < logLines.Length; slot++)
            {
                if (logLines[slot] != null)
                {
                    logLines[slot].Alpha = TextBoxAnimator.ComputeLineAlpha(slot, filledCount, lineAlphas);
                }
            }
        }

        private void ApplyHeight()
        {
            if (boxRect == null)
            {
                return;
            }

            Vector2 size = boxRect.sizeDelta;
            size.y = currentHeight;
            boxRect.sizeDelta = size;
        }

        private void SetSlotAlpha(int slot, float alpha)
        {
            if (logLines != null && slot >= 0 && slot < logLines.Length && logLines[slot] != null)
            {
                logLines[slot].Alpha = alpha;
            }
        }

        /// <summary>
        /// Height a wrapped description needs. Falls back to a single line when the
        /// label has no laid-out width yet - during the first frame, or in Edit Mode
        /// tests - rather than asking TMP to wrap into zero width.
        /// </summary>
        private float ComputeDescriptionHeight()
        {
            if (label == null)
            {
                return verticalPadding + lineHeight;
            }

            float width = label.rectTransform.rect.width;
            if (width <= 0f)
            {
                return verticalPadding + lineHeight;
            }

            return verticalPadding + label.GetPreferredValues(description, width, 0f).y;
        }

        private string FormatLine(GameMessage message)
        {
            if (!TryGetCategoryColor(message.Category, out Color color))
            {
                return message.Text;
            }

            // Nests correctly around any per-name tint the log already applied -
            // TMP keeps a colour stack, so the inner </color> pops back to this one.
            return MessageFormatter.Colorize(message.Text, color);
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
