using HunterWidow.Domain.Erosion;
using System.Collections.Generic;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class ErosionLogicTests
    {
        [Test]
        public void TimeDamageAndPurificationRespectBandsAndMaximum()
        {
            var logic = new ErosionLogic(new ErosionSettings(100d, 100d, 0.2d, 60d, 30d));
            string changedBand = null;
            logic.BandChanged += band => changedBand = band;

            logic.Tick(201d);
            Assert.That(logic.GetState().BandId, Is.EqualTo("sensitive"));
            Assert.That(changedBand, Is.EqualTo("sensitive"));
            logic.ApplyHit(40d);
            Assert.That(logic.GetState().CurrentValue, Is.EqualTo(19.8d).Within(0.0001d));
            Assert.That(logic.GetState().BandId, Is.EqualTo("severe"));
            logic.Purify(200d);

            Assert.That(logic.GetState().CurrentValue, Is.EqualTo(100d));
            Assert.That(logic.GetState().BandId, Is.EqualTo("calm"));
        }

        [Test]
        public void DepletionClampsAtZero()
        {
            var logic = new ErosionLogic(new ErosionSettings(100d, 10d, 0d, 60d, 30d));

            logic.ApplyHit(20d);

            Assert.That(logic.GetState().CurrentValue, Is.EqualTo(0d));
            Assert.That(logic.GetState().IsDepleted, Is.True);
        }

        [Test]
        public void DataCanAddBandsWithoutChangingErosionLogic()
        {
            var logic = new ErosionLogic(new ErosionSettings(
                100d,
                100d,
                0d,
                new List<ErosionBandDefinition>
                {
                    new ErosionBandDefinition("clear", 80d),
                    new ErosionBandDefinition("uneasy", 60d),
                    new ErosionBandDefinition("strained", 40d),
                    new ErosionBandDefinition("critical", 20d, 0.7d),
                    new ErosionBandDefinition("empty", 0d, 1d)
                }));

            logic.ApplyHit(45d);

            Assert.That(logic.GetState().BandId, Is.EqualTo("strained"));
        }

        [Test]
        public void SnapshotExposesTheCurrentBandDropUpgradeChance()
        {
            var logic = new ErosionLogic(new ErosionSettings(
                100d,
                100d,
                0d,
                new List<ErosionBandDefinition>
                {
                    new ErosionBandDefinition("calm", 60d),
                    new ErosionBandDefinition("severe", 0d, 0.7d)
                }));

            logic.ApplyHit(80d);

            Assert.That(logic.GetState().DropUpgradeChance, Is.EqualTo(0.7d));
        }
    }
}
