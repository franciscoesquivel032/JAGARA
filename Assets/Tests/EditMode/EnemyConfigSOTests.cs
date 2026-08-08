using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Jagara.Runtime.Data;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// Covers EnemyConfigSO's OnValidate guards. These matter because the asset
    /// is the balancing surface: designers retune HP/damage by typing numbers
    /// into the Inspector, and a typo there must not be able to produce an
    /// enemy the rest of the systems cannot handle.
    /// </summary>
    public class EnemyConfigSOTests
    {
        [Test]
        public void OnValidate_ZeroOrNegativeBaseMaxHP_ClampsToOne()
        {
            // A 0 max-HP enemy would spawn with HealthState.IsDead already true,
            // but nothing calls Die() on it - it would sit on its tile, blocking
            // it, for the rest of the floor.
            var config = CreateConfig(baseMaxHP: 0, baseAttackDamage: 3);

            InvokeOnValidate(config);

            Assert.AreEqual(1, config.BaseMaxHP);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void OnValidate_NegativeBaseAttackDamage_ClampsToZero()
        {
            var config = CreateConfig(baseMaxHP: 30, baseAttackDamage: -5);

            InvokeOnValidate(config);

            Assert.AreEqual(0, config.BaseAttackDamage);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void OnValidate_ValidCombatValues_LeavesThemUntouched()
        {
            var config = CreateConfig(baseMaxHP: 35, baseAttackDamage: 4);

            InvokeOnValidate(config);

            Assert.AreEqual(35, config.BaseMaxHP, "A valid authored HP value must survive OnValidate unchanged.");
            Assert.AreEqual(4, config.BaseAttackDamage);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void OnValidate_ForgetRangeBelowDetectionRange_StillClampsUp()
        {
            // Pre-existing guard: re-asserted here so the combat clamps added
            // alongside it cannot quietly displace it.
            var config = CreateConfig(baseMaxHP: 30, baseAttackDamage: 3);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("detectionRange").intValue = 8;
            serialized.FindProperty("forgetRange").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            InvokeOnValidate(config);

            Assert.AreEqual(8, config.ForgetRange);

            Object.DestroyImmediate(config);
        }

        private static EnemyConfigSO CreateConfig(int baseMaxHP, int baseAttackDamage)
        {
            var config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            var serialized = new SerializedObject(config);
            SetInt(serialized, "baseMaxHP", baseMaxHP);
            SetInt(serialized, "baseAttackDamage", baseAttackDamage);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            var property = serialized.FindProperty(propertyName);
            Assert.IsNotNull(property, $"Expected serialized field '{propertyName}' on EnemyConfigSO.");
            property.intValue = value;
        }

        /// <summary>
        /// OnValidate is private and its automatic invocation depends on Editor
        /// message dispatch, which is unreliable in headless Edit Mode (the same
        /// reason EnemyControllerTests injects Awake's results by reflection).
        /// Calling it directly tests the guard itself, deterministically.
        /// </summary>
        private static void InvokeOnValidate(EnemyConfigSO config)
        {
            MethodInfo method = typeof(EnemyConfigSO).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "Expected a private OnValidate() on EnemyConfigSO.");
            method.Invoke(config, null);
        }
    }
}
