using System;
using System.Collections.Generic;

namespace HunterWidow.Domain.Economy
{
    public sealed class PricingSettings
    {
        public PricingSettings(double rawSaleRatio, IReadOnlyList<double> tierThresholds, IReadOnlyList<double> tierMultipliers)
        {
            if (rawSaleRatio < 0d || tierThresholds == null || tierMultipliers == null || tierThresholds.Count == 0 || tierThresholds.Count != tierMultipliers.Count)
            {
                throw new ArgumentException("Pricing settings are invalid.");
            }

            RawSaleRatio = rawSaleRatio;
            TierThresholds = tierThresholds;
            TierMultipliers = tierMultipliers;
        }

        public double RawSaleRatio { get; }

        public IReadOnlyList<double> TierThresholds { get; }

        public IReadOnlyList<double> TierMultipliers { get; }
    }

    public sealed class PriceQuote
    {
        public PriceQuote(double qualityScore, int tierIndex, double multiplier, int price)
        {
            QualityScore = qualityScore;
            TierIndex = tierIndex;
            Multiplier = multiplier;
            Price = price;
        }

        public double QualityScore { get; }

        public int TierIndex { get; }

        public double Multiplier { get; }

        public int Price { get; }
    }

    public static class PricingLogic
    {
        public static PriceQuote QuoteProcessed(int basePrice, IReadOnlyList<int> materialGrades, double refineryBonus, PricingSettings settings)
        {
            if (basePrice < 0 || materialGrades == null || materialGrades.Count == 0 || settings == null)
            {
                throw new ArgumentException("Processed price inputs are invalid.");
            }

            var totalGrade = 0d;
            for (var gradeIndex = 0; gradeIndex < materialGrades.Count; gradeIndex++)
            {
                totalGrade += materialGrades[gradeIndex];
            }

            var score = (totalGrade / materialGrades.Count) + refineryBonus;
            var tierIndex = 0;
            for (var thresholdIndex = 0; thresholdIndex < settings.TierThresholds.Count; thresholdIndex++)
            {
                if (score >= settings.TierThresholds[thresholdIndex])
                {
                    tierIndex = thresholdIndex;
                }
            }

            var multiplier = settings.TierMultipliers[tierIndex];
            return new PriceQuote(score, tierIndex, multiplier, (int)Math.Round(basePrice * multiplier, MidpointRounding.AwayFromZero));
        }

        public static PriceQuote QuoteRaw(int basePrice, PricingSettings settings)
        {
            if (basePrice < 0 || settings == null)
            {
                throw new ArgumentException("Raw price inputs are invalid.");
            }

            return new PriceQuote(0d, 0, settings.RawSaleRatio, (int)Math.Round(basePrice * settings.RawSaleRatio, MidpointRounding.AwayFromZero));
        }
    }
}
