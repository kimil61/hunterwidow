using HunterWidow.Domain.Combat;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class ChargeLogicTests
    {
        [TestCase(0.14d, ChargeOutcome.Cancelled)]
        [TestCase(0.55d, ChargeOutcome.SweetSpot)]
        [TestCase(0.8d, ChargeOutcome.SweetSpot)]
        [TestCase(0.81d, ChargeOutcome.Late)]
        public void ReleaseUsesDocumentedTimingBoundaries(double duration, ChargeOutcome expected)
        {
            var logic = new ChargeLogic(CreateSettings());
            logic.Begin();
            logic.Tick(duration);

            var release = logic.Release();

            Assert.That(release.Outcome, Is.EqualTo(expected));
            Assert.That(release.IsAutomatic, Is.False);
        }

        [Test]
        public void MaxChargeAutoReleasesAndLeavesChargingState()
        {
            var logic = new ChargeLogic(CreateSettings());
            logic.Begin();

            var release = logic.Tick(2d);

            Assert.That(release, Is.Not.Null);
            Assert.That(release.IsAutomatic, Is.True);
            Assert.That(release.Outcome, Is.EqualTo(ChargeOutcome.Late));
            Assert.That(logic.IsCharging, Is.False);
        }

        private static ChargeSettings CreateSettings()
        {
            return new ChargeSettings(0.15d, 0.55d, 0.8d, 1.2d);
        }
    }
}
