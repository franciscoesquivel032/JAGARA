using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jagara.Runtime.Narrative
{
    /// <summary>
    /// The game's single message channel. Any system can post to it through a
    /// [SerializeField] reference without knowing that a UI exists at all, and
    /// TextBoxController is the only thing listening - the same ScriptableObject
    /// decoupling the rest of the project uses, so no separate GameEventSO layer
    /// is needed here.
    /// <para>
    /// History is a fixed-capacity ring buffer: floors are generated and torn
    /// down repeatedly within one session, so an unbounded list would grow for as
    /// long as the game runs. Nothing displays the backlog yet, but keeping it
    /// means a future log viewer can be added without touching a single emitter.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "New Message Log", menuName = "Jagara/Message Log")]
    public class MessageLogSO : ScriptableObject
    {
        [Serializable]
        private struct NameRoleColor
        {
            public NameRole role;
            public Color color;
        }

        [Tooltip("How many past messages are kept in memory. Older ones are discarded.")]
        [SerializeField] private int capacity = 100;

        [Tooltip("Tint applied to StyledName arguments by what they refer to. Roles with no entry render untinted.")]
        [SerializeField]
        private NameRoleColor[] nameColors =
        {
            new NameRoleColor { role = NameRole.Item, color = new Color32(0xFD, 0xC0, 0x90, 0xFF) },
            new NameRoleColor { role = NameRole.Enemy, color = new Color32(0xFF, 0x5D, 0x61, 0xFF) }
        };

        // Runtime-only, like FloatVariableSO's RuntimeValue: the log must start
        // empty every Play Mode session rather than persisting into the asset.
        [NonSerialized] private GameMessage[] buffer;
        [NonSerialized] private int count;
        [NonSerialized] private int head;

        // Reused by Recent() so per-turn message rendering doesn't allocate.
        [NonSerialized] private List<GameMessage> recentBuffer;

        public event Action<GameMessage> OnMessagePosted = delegate { };

        /// <summary>Number of messages currently held, capped at <see cref="Capacity"/>.</summary>
        public int Count => count;

        public int Capacity => capacity;

        private void OnEnable() => Initialize();

        private void Initialize()
        {
            if (capacity < 1)
            {
                Debug.LogError($"MessageLogSO '{name}': capacity must be at least 1 (was {capacity}). Clamping to 1.");
                capacity = 1;
            }

            buffer = new GameMessage[capacity];
            recentBuffer = new List<GameMessage>(capacity);
            count = 0;
            head = 0;
        }

        public void Post(MessageTemplateSO template, params object[] args)
        {
            if (template == null)
            {
                Debug.LogError($"MessageLogSO '{name}': Post called with a null template; nothing was logged. Check the emitter's [SerializeField] wiring.");
                return;
            }

            PostMessage(template.Build(ApplyNameColors(args)));
        }

        /// <summary>
        /// Replaces every StyledName argument with its tinted rich-text form,
        /// leaving other arguments (numbers, plain strings) untouched.
        /// <para>
        /// This runs here rather than in the UI because the role is only known at
        /// the call site: once the template has been formatted, "Old Compass" is
        /// just letters in the middle of a sentence and nothing downstream can
        /// tell it apart from the rest of the line.
        /// </para>
        /// </summary>
        private object[] ApplyNameColors(object[] args)
        {
            if (args == null)
            {
                return null;
            }

            // Almost every message carries at least one name, but allocating is
            // still avoided outright when none of them are styled.
            object[] resolved = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is not StyledName styled)
                {
                    continue;
                }

                resolved ??= (object[])args.Clone();
                resolved[i] = TryGetNameColor(styled.Role, out Color color)
                    ? MessageFormatter.Colorize(styled.Text, color)
                    : styled.Text;
            }

            return resolved ?? args;
        }

        private bool TryGetNameColor(NameRole role, out Color color)
        {
            if (nameColors != null && role != NameRole.None)
            {
                for (int i = 0; i < nameColors.Length; i++)
                {
                    if (nameColors[i].role == role)
                    {
                        color = nameColors[i].color;
                        return true;
                    }
                }
            }

            color = default;
            return false;
        }

        /// <summary>
        /// Posts a message built in code rather than from a template asset. Use
        /// sparingly - templates keep player-facing wording out of C#.
        /// </summary>
        public void PostRaw(string text, MessageCategory category = MessageCategory.System)
        {
            PostMessage(new GameMessage(text, category));
        }

        /// <summary>
        /// The most recent <paramref name="requested"/> messages, oldest first.
        /// Returns fewer if the log holds fewer. The returned list is a shared
        /// buffer overwritten by the next call - read it, don't store it.
        /// </summary>
        public IReadOnlyList<GameMessage> Recent(int requested)
        {
            EnsureInitialized();
            recentBuffer.Clear();

            int take = Mathf.Clamp(requested, 0, count);
            for (int i = take; i > 0; i--)
            {
                // head is the next write slot, so the newest entry sits at head-1.
                // i <= count <= buffer.Length, so this stays non-negative.
                recentBuffer.Add(buffer[(head - i + buffer.Length) % buffer.Length]);
            }

            return recentBuffer;
        }

        /// <summary>Drops all history. Called when a new floor starts.</summary>
        public void Clear()
        {
            EnsureInitialized();
            Array.Clear(buffer, 0, buffer.Length);
            count = 0;
            head = 0;
        }

        private void PostMessage(GameMessage message)
        {
            EnsureInitialized();

            buffer[head] = message;
            head = (head + 1) % buffer.Length;
            if (count < buffer.Length)
            {
                count++;
            }

            OnMessagePosted(message);
        }

        /// <summary>
        /// Guards against a post arriving before OnEnable has run for this asset -
        /// possible during domain reload, when [NonSerialized] state is already
        /// gone but callers may still be executing.
        /// </summary>
        private void EnsureInitialized()
        {
            if (buffer == null)
            {
                Initialize();
            }
        }
    }
}
