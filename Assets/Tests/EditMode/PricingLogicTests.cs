using System.Collections.Generic;
using HunterWidow.Domain.Economy;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class PricingLogicTests
    {
        [Test]
        public void ProcessedPriceUsesThreeQualityTiers()
        {
            var settings = new PricingSettings(
                0.3d,
                new List<double> { 1d, 2d, 3d },
                new List<double> { 1d, 1.5d, 2.2d });

            var low = PricingLogic.QuoteProcessed(100, new List<int> { 1 }, 0d, settings);
            var middle = PricingLogic.QuoteProcessed(100, new List<int> { 2 }, 0d, settings);
            var high = PricingLogic.QuoteProcessed(100, new List<int> { 3 }, 0d, settings);

            Assert.That(low.Price, Is.EqualTo(100));
            Assert.That(middle.Price, Is.EqualTo(150));
            Assert.That(high.Price, Is.EqualTo(220));
        }

        [Test]
        public void RawMaterialSaleUsesConfiguredThirtyPercentRatio()
        {
            var settings = new PricingSettings(
                0.3d,
                new List<double> { 1d, 2d, 3d },
                new List<double> { 1d, 1.5d, 2.2d });

            var quote = PricingLogic.QuoteRaw(100, settings);

            Assert.That(quote.Price, Is.EqualTo(30));
            Assert.That(quote.Multiplier, Is.EqualTo(0.3d));
        }
    }
}
