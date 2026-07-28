using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Data;

namespace Jagara.Tests.EditMode
{
    public class InventorySOTests
    {
        private InventorySO inventory;

        [SetUp]
        public void SetUp()
        {
            inventory = ScriptableObject.CreateInstance<InventorySO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(inventory);
        }

        [Test]
        public void TryAddItem_FillsSlotsInOrder_UntilFull()
        {
            for (int i = 0; i < inventory.MaxSlots; i++)
            {
                Assert.IsTrue(inventory.TryAddItem(CreateItem()), $"add #{i}");
            }

            Assert.IsTrue(inventory.IsFull());
            Assert.IsFalse(inventory.TryAddItem(CreateItem()), "adding beyond capacity should fail");
        }

        [Test]
        public void RemoveAt_ClearsSlot_AndAllowsSubsequentAdd()
        {
            var item = CreateItem();
            inventory.TryAddItem(item);

            Assert.IsTrue(inventory.RemoveAt(0));
            Assert.IsNull(inventory.Slots[0]);

            Assert.IsTrue(inventory.TryAddItem(CreateItem()), "slot should be reusable after removal");
        }

        [Test]
        public void RemoveAt_OnEmptySlot_ReturnsFalse()
        {
            Assert.IsFalse(inventory.RemoveAt(0));
        }

        [Test]
        public void TryUseItem_ConsumedOnUse_AppliesEffectAndClearsSlot()
        {
            var effect = ScriptableObject.CreateInstance<FakeItemEffectSO>();
            var item = CreateItem(effect, consumedOnUse: true);
            inventory.TryAddItem(item);

            var user = new GameObject("user");
            try
            {
                Assert.IsTrue(inventory.TryUseItem(0, user));
                Assert.AreSame(user, effect.LastUser);
                Assert.IsNull(inventory.Slots[0], "consumable item should be removed after use");
            }
            finally
            {
                Object.DestroyImmediate(user);
                Object.DestroyImmediate(effect);
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void TryUseItem_NotConsumedOnUse_AppliesEffectButKeepsItem()
        {
            var effect = ScriptableObject.CreateInstance<FakeItemEffectSO>();
            var item = CreateItem(effect, consumedOnUse: false);
            inventory.TryAddItem(item);

            Assert.IsTrue(inventory.TryUseItem(0, null));
            Assert.AreSame(item, inventory.Slots[0], "non-consumable item should remain in its slot");

            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(item);
        }

        [Test]
        public void TryUseItem_OnEmptySlot_ReturnsFalse()
        {
            Assert.IsFalse(inventory.TryUseItem(0, null));
        }

        [Test]
        public void OnInventoryChanged_FiresOnAddRemoveAndUse()
        {
            int changeCount = 0;
            inventory.OnInventoryChanged += () => changeCount++;

            var item = CreateItem();
            inventory.TryAddItem(item);
            Assert.AreEqual(1, changeCount, "add should notify");

            inventory.TryUseItem(0, null);
            Assert.AreEqual(2, changeCount, "use should notify");

            var second = CreateItem();
            inventory.TryAddItem(second);
            inventory.RemoveAt(0);
            Assert.AreEqual(4, changeCount, "add and remove should each notify");

            Object.DestroyImmediate(item);
            Object.DestroyImmediate(second);
        }

        private static ItemSO CreateItem(ItemEffectSO effect = null, bool consumedOnUse = true)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            SetPrivateField(item, "effect", effect);
            SetPrivateField(item, "consumedOnUse", consumedOnUse);
            return item;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"field '{name}' not found");
            field.SetValue(target, value);
        }

        private class FakeItemEffectSO : ItemEffectSO
        {
            public GameObject LastUser { get; private set; }

            public override void Apply(GameObject user)
            {
                LastUser = user;
            }
        }
    }
}
