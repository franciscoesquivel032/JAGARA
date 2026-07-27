using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Gameplay;

namespace Jagara.Tests.EditMode
{
    public class EntityOutlineTests
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        private GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Awake_WritesConfiguredOutlineColorIntoSpriteRendererPropertyBlock()
        {
            go = new GameObject("Entity");
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            var outline = go.AddComponent<EntityOutline>();

            var expected = new Color(1f, 0.231f, 0.231f, 1f);
            SetPrivateField(outline, "outlineColor", expected);
            InvokeAwake(outline);

            var block = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(block);
            Assert.AreEqual(expected, block.GetColor(OutlineColorId));
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"field '{name}' not found");
            field.SetValue(target, value);
        }

        private static void InvokeAwake(MonoBehaviour behaviour)
        {
            var method = behaviour.GetType().GetMethod("Awake",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "Awake method not found");
            method.Invoke(behaviour, null);
        }
    }
}
