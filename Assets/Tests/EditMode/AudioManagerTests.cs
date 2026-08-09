using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Jagara.Runtime.Audio;
using Jagara.Runtime.Data;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// AudioManager's pool is a hard ceiling by design - combat can fire many
    /// simultaneous hit/impact sounds in one frame, and a leak or exception here
    /// would either balloon AudioSource count or crash the turn resolver mid-combat.
    /// </summary>
    public class AudioManagerTests
    {
        private GameObject managerGO;
        private AudioManager manager;
        private SoundEventSO soundEvent;

        [SetUp]
        public void SetUp()
        {
            managerGO = new GameObject("AudioManager");
            manager = managerGO.AddComponent<AudioManager>();

            soundEvent = ScriptableObject.CreateInstance<SoundEventSO>();
            var so = new SerializedObject(soundEvent);
            var clipsProp = so.FindProperty("clips");
            clipsProp.arraySize = 1;
            clipsProp.GetArrayElementAtIndex(0).objectReferenceValue =
                AudioClip.Create("silent", 1, 1, 44100, false);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(managerGO);
            Object.DestroyImmediate(soundEvent);
        }

        [Test]
        public void Play_MoreTimesThanPoolSize_NeverGrowsPoolAndDoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    manager.Play(soundEvent);
                }
            });

            Assert.AreEqual(16, manager.PooledSourceCount);
        }
    }
}
