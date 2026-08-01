using System;
using UnityEngine;

namespace Jagara.Runtime.Narrative
{
    /// <summary>
    /// Substitutes arguments into a message template. Pure and static so it can
    /// be tested without the Unity Editor lifecycle.
    /// </summary>
    public static class MessageFormatter
    {
        /// <summary>
        /// Formats <paramref name="template"/> with <paramref name="args"/> using
        /// standard {0}/{1} placeholders.
        /// <para>
        /// A mismatch between placeholders and arguments (a mis-authored template
        /// asset, or a caller passing the wrong number of values) is a real bug,
        /// so it is reported with Debug.LogError - but it must not throw: these
        /// calls sit inside turn resolution, and a FormatException there would
        /// abort the turn and leave the game in a half-resolved state. The raw
        /// template is returned instead, which makes the mistake visible on
        /// screen without breaking play.
        /// </para>
        /// </summary>
        public static string Format(string template, params object[] args)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            if (args == null || args.Length == 0)
            {
                return template;
            }

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException e)
            {
                Debug.LogError($"MessageFormatter.Format: template \"{template}\" does not match its {args.Length} argument(s) ({e.Message}). Returning the raw template.");
                return template;
            }
        }

        /// <summary>
        /// Wraps <paramref name="text"/> in a TMP colour tag. Nesting is safe -
        /// TMP keeps a colour stack, so a tinted name inside a tinted line closes
        /// back to the outer colour.
        /// </summary>
        public static string Colorize(string text, Color color) =>
            $"<color=#{ToRichTextHex(color)}>{text}</color>";

        /// <summary>
        /// Six-digit hex for a TMP colour tag.
        /// <para>
        /// Rounds rather than truncating, unlike ColorUtility.ToHtmlStringRGB: a
        /// colour authored as an exact byte (0xC0 -> 192/255f) can come back from
        /// float as 191.99998, and truncation would silently shift the palette by
        /// one - #FDC090 rendering as #FDBF90.
        /// </para>
        /// </summary>
        public static string ToRichTextHex(Color color)
        {
            int r = Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
            int g = Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
            int b = Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);
            return $"{r:X2}{g:X2}{b:X2}";
        }
    }
}
