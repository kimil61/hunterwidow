using System;
using System.Collections.Generic;

namespace HunterWidow.Domain.Erosion
{
    public sealed class ErosionBandDefinition
    {
        public ErosionBandDefinition(string id, double minimumValue, double dropUpgradeChance = 0d)
        {
            if (dropUpgradeChance < 0d || dropUpgradeChance > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(dropUpgradeChance));
            }

            Id = id;
            MinimumValue = minimumValue;
            DropUpgradeChance = dropUpgradeChance;
        }

        public string Id { get; }

        public double MinimumValue { get; }

        public double DropUpgradeChance { get; }
    }

    public sealed class ErosionSettings
    {
        public ErosionSettings(double maximum, double startingValue, double decayPerSecond, double calmThreshold, double sensitiveThreshold)
            : this(
                maximum,
                startingValue,
                decayPerSecond,
                new List<ErosionBandDefinition>
                {
                    new ErosionBandDefinition("calm", calmThreshold),
                    new ErosionBandDefinition("sensitive", sensitiveThreshold),
                    new ErosionBandDefinition("severe", 0d)
                })
        {
        }

        public ErosionSettings(double maximum, double startingValue, double decayPerSecond, IReadOnlyList<ErosionBandDefinition> bands)
        {
            if (maximum <= 0d || startingValue < 0d || startingValue > maximum || decayPerSecond < 0d || bands == null || bands.Count == 0)
            {
                throw new ArgumentException("Erosion settings are outside their valid range.");
            }

            var previousMinimum = maximum;
            for (var bandIndex = 0; bandIndex < bands.Count; bandIndex++)
            {
                var band = bands[bandIndex];
                if (band == null || string.IsNullOrEmpty(band.Id) || band.MinimumValue < 0d || band.MinimumValue > previousMinimum)
                {
                    throw new ArgumentException("Erosion bands must be non-empty and sorted from high to low.");
                }

                previousMinimum = band.MinimumValue;
            }

            Maximum = maximum;
            StartingValue = startingValue;
            DecayPerSecond = decayPerSecond;
            Bands = new List<ErosionBandDefinition>(bands);
        }

        public double Maximum { get; }

        public double StartingValue { get; }

        public double DecayPerSecond { get; }

        public IReadOnlyList<ErosionBandDefinition> Bands { get; }
    }

    public sealed class ErosionSnapshot
    {
        public ErosionSnapshot(double currentValue, double maximum, string bandId, double dropUpgradeChance, bool isDepleted)
        {
            CurrentValue = currentValue;
            Maximum = maximum;
            BandId = bandId;
            DropUpgradeChance = dropUpgradeChance;
            IsDepleted = isDepleted;
        }

        public double CurrentValue { get; }

        public double Maximum { get; }

        public string BandId { get; }

        public double DropUpgradeChance { get; }

        public bool IsDepleted { get; }
    }

    public sealed class ErosionLogic
    {
        private ErosionSettings settings;
        private double currentValue;
        private string currentBandId;

        public ErosionLogic(ErosionSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Reset();
        }

        public event Action<string> BandChanged;

        public ErosionSnapshot GetState()
        {
            return new ErosionSnapshot(
                currentValue,
                settings.Maximum,
                currentBandId,
                GetBand(currentBandId).DropUpgradeChance,
                currentValue <= 0d);
        }

        public void SetSettings(ErosionSettings newSettings, bool reset)
        {
            settings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
            if (reset)
            {
                Reset();
                return;
            }

            currentValue = Math.Min(settings.Maximum, currentValue);
            UpdateBand();
        }

        public void Reset()
        {
            currentValue = settings.StartingValue;
            currentBandId = GetBandId(currentValue);
        }

        public void Tick(double deltaTime)
        {
            if (deltaTime > 0d)
            {
                ChangeBy(-(settings.DecayPerSecond * deltaTime));
            }
        }

        public void ApplyHit(double damage)
        {
            if (damage > 0d)
            {
                ChangeBy(-damage);
            }
        }

        public void Purify(double amount)
        {
            if (amount > 0d)
            {
                ChangeBy(amount);
            }
        }

        private void ChangeBy(double amount)
        {
            currentValue = Math.Max(0d, Math.Min(settings.Maximum, currentValue + amount));
            UpdateBand();
        }

        private void UpdateBand()
        {
            var nextBandId = GetBandId(currentValue);
            if (string.Equals(nextBandId, currentBandId, StringComparison.Ordinal))
            {
                return;
            }

            currentBandId = nextBandId;
            BandChanged?.Invoke(currentBandId);
        }

        private string GetBandId(double value)
        {
            for (var bandIndex = 0; bandIndex < settings.Bands.Count; bandIndex++)
            {
                var band = settings.Bands[bandIndex];
                if (value >= band.MinimumValue)
                {
                    return band.Id;
                }
            }

            return settings.Bands[settings.Bands.Count - 1].Id;
        }

        private ErosionBandDefinition GetBand(string bandId)
        {
            for (var bandIndex = 0; bandIndex < settings.Bands.Count; bandIndex++)
            {
                if (string.Equals(settings.Bands[bandIndex].Id, bandId, StringComparison.Ordinal))
                {
                    return settings.Bands[bandIndex];
                }
            }

            return settings.Bands[settings.Bands.Count - 1];
        }
    }
}
