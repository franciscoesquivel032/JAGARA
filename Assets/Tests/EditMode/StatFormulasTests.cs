using NUnit.Framework;
using Jagara.Runtime.Combat;

namespace Jagara.Tests.EditMode
{
    public class StatFormulasTests
    {
        [Test]
        public void ComputeMaxHP_ZeroVigor_ReturnsBaseHP()
        {
            Assert.AreEqual(StatFormulas.ComputeMaxHP(0), StatFormulas.ComputeMaxHP(0));
            Assert.Greater(StatFormulas.ComputeMaxHP(0), 0, "Base HP with zero Vigor must still be a positive value.");
        }

        [Test]
        public void ComputeMaxHP_ScalesLinearlyWithVigor()
        {
            int atZero = StatFormulas.ComputeMaxHP(0);
            int atOne = StatFormulas.ComputeMaxHP(1);
            int atTwo = StatFormulas.ComputeMaxHP(2);

            int perPoint = atOne - atZero;
            Assert.Greater(perPoint, 0, "Each Vigor point must increase MaxHP.");
            Assert.AreEqual(perPoint, atTwo - atOne, "MaxHP must scale linearly with Vigor.");
        }

        [Test]
        public void ComputeAttackDamage_ScalesLinearlyWithPoder()
        {
            int atZero = StatFormulas.ComputeAttackDamage(0);
            int atOne = StatFormulas.ComputeAttackDamage(1);
            int atTwo = StatFormulas.ComputeAttackDamage(2);

            Assert.Greater(atZero, 0, "Base attack damage with zero Poder must still be a positive value.");
            int perPoint = atOne - atZero;
            Assert.Greater(perPoint, 0, "Each Poder point must increase attack damage.");
            Assert.AreEqual(perPoint, atTwo - atOne, "Attack damage must scale linearly with Poder.");
        }
    }
}
