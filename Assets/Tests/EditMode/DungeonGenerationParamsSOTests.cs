using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class DungeonGenerationParamsSOTests
    {
        [Test]
        public void ToParams_MapsEverySerializedFieldOntoDungeonGenerationParams()
        {
            var so = ScriptableObject.CreateInstance<DungeonGenerationParamsSO>();
            var serialized = new SerializedObject(so);

            SetInt(serialized, "gridWidth", 42);
            SetInt(serialized, "gridHeight", 37);
            SetInt(serialized, "minRoomCount", 3);
            SetInt(serialized, "maxRoomCount", 9);
            SetInt(serialized, "minRoomWidth", 2);
            SetInt(serialized, "maxRoomWidth", 6);
            SetInt(serialized, "minRoomHeight", 5);
            SetInt(serialized, "maxRoomHeight", 11);
            SetInt(serialized, "minEnemyCount", 1);
            SetInt(serialized, "maxEnemyCount", 2);
            SetInt(serialized, "minItemCount", 13);
            SetInt(serialized, "maxItemCount", 14);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var result = so.ToParams();

            Assert.AreEqual(42, result.GridWidth);
            Assert.AreEqual(37, result.GridHeight);
            Assert.AreEqual(3, result.MinRoomCount);
            Assert.AreEqual(9, result.MaxRoomCount);
            Assert.AreEqual(2, result.MinRoomWidth);
            Assert.AreEqual(6, result.MaxRoomWidth);
            Assert.AreEqual(5, result.MinRoomHeight);
            Assert.AreEqual(11, result.MaxRoomHeight);
            Assert.AreEqual(1, result.MinEnemyCount);
            Assert.AreEqual(2, result.MaxEnemyCount);
            Assert.AreEqual(13, result.MinItemCount);
            Assert.AreEqual(14, result.MaxItemCount);

            Object.DestroyImmediate(so);
        }

        [Test]
        public void CreateInstance_DefaultValuesProduceAFloorSuccessfully()
        {
            var so = ScriptableObject.CreateInstance<DungeonGenerationParamsSO>();

            var floor = new DungeonGenerator().GenerateFloor(1234, so.ToParams());

            Assert.IsNotNull(floor);
            Assert.IsTrue(floor.PlayerSpawn.HasValue);
            Assert.IsTrue(floor.StairsDownPosition.HasValue);

            Object.DestroyImmediate(so);
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            var property = serialized.FindProperty(propertyName);
            Assert.IsNotNull(property, $"Expected serialized field '{propertyName}' on DungeonGenerationParamsSO.");
            property.intValue = value;
        }
    }
}
