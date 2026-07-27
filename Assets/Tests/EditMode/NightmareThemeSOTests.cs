using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Data;

namespace Jagara.Tests.EditMode
{
    public class NightmareThemeSOTests
    {
        private NightmareThemeSO theme;

        [SetUp]
        public void SetUp()
        {
            theme = ScriptableObject.CreateInstance<NightmareThemeSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(theme);
        }

        [Test]
        public void EnvironmentTint_DefaultsToDesaturatedDimTone()
        {
            var expected = new Color(140f / 255f, 140f / 255f, 153f / 255f, 1f);
            Assert.AreEqual(expected, theme.EnvironmentTint);
        }
    }
}
