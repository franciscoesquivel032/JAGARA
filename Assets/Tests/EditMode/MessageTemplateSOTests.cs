using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Narrative;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// Covers the combat message templates too. Combat itself does not exist yet
    /// (EnemyController has no attack action), so these are the only tests that
    /// will guard those templates' wording contract until it does.
    /// </summary>
    public class MessageTemplateSOTests
    {
        [Test]
        public void Build_AppliesArgumentsAndCarriesCategory()
        {
            var template = CreateTemplate("{0} takes {1} damage.", MessageCategory.Combat);

            GameMessage message = template.Build("Shade", 7);

            Assert.AreEqual("Shade takes 7 damage.", message.Text);
            Assert.AreEqual(MessageCategory.Combat, message.Category);

            Object.DestroyImmediate(template);
        }

        [Test]
        public void Build_WithNoArguments_UsesTemplateVerbatim()
        {
            var template = CreateTemplate("You sink deeper into the nightmare.", MessageCategory.Floor);

            GameMessage message = template.Build();

            Assert.AreEqual("You sink deeper into the nightmare.", message.Text);
            Assert.AreEqual(MessageCategory.Floor, message.Category);

            Object.DestroyImmediate(template);
        }

        [Test]
        public void Build_AbilityTemplate_FormatsBothOperands()
        {
            var template = CreateTemplate("{0} uses {1}!", MessageCategory.Combat);

            Assert.AreEqual("Wraith uses Whisper!", template.Build("Wraith", "Whisper").Text);

            Object.DestroyImmediate(template);
        }

        internal static MessageTemplateSO CreateTemplate(string text, MessageCategory category)
        {
            var template = ScriptableObject.CreateInstance<MessageTemplateSO>();
            SetPrivateField(template, "template", text);
            SetPrivateField(template, "category", category);
            return template;
        }

        internal static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"field '{name}' not found");
            field.SetValue(target, value);
        }
    }
}
