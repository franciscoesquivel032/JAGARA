using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Gameplay;

namespace Jagara.Tests.EditMode
{
    public class CardinalDirectionResolverTests
    {
        [TestCase(0f, 1f, 0, 1)]
        [TestCase(0f, -1f, 0, -1)]
        [TestCase(1f, 0f, 1, 0)]
        [TestCase(-1f, 0f, -1, 0)]
        [TestCase(1f, 1f, 0, 1)]      // diagonal input: vertical wins, never diagonal output
        [TestCase(0.1f, 0.1f, 0, 0)]  // inside deadzone: no move
        public void Resolve_ReturnsExpectedCardinalDirection(float x, float y, int expectedX, int expectedY)
        {
            Assert.AreEqual(new Vector2Int(expectedX, expectedY),
                CardinalDirectionResolver.Resolve(new Vector2(x, y)));
        }
    }
}
