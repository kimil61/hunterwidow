using System;

namespace HunterWidow.Domain.Combat
{
    public enum ChargeOutcome
    {
        Cancelled,
        SweetSpot,
        Late
    }

    public sealed class ChargeSettings
    {
        public ChargeSettings(double minCharge, double sweetStart, double sweetEnd, double maxCharge)
        {
            if (minCharge < 0d || sweetStart < minCharge || sweetEnd < sweetStart || maxCharge < sweetEnd)
            {
                throw new ArgumentException("Charge timing thresholds must be ordered and non-negative.");
            }

            MinCharge = minCharge;
            SweetStart = sweetStart;
            SweetEnd = sweetEnd;
            MaxCharge = maxCharge;
        }

        public double MinCharge { get; }

        public double SweetStart { get; }

        public double SweetEnd { get; }

        public double MaxCharge { get; }
    }

    public sealed class ChargeRelease
    {
        public ChargeRelease(ChargeOutcome outcome, double duration, bool isAutomatic)
        {
            Outcome = outcome;
            Duration = duration;
            IsAutomatic = isAutomatic;
        }

        public ChargeOutcome Outcome { get; }

        public double Duration { get; }

        public bool IsAutomatic { get; }

        public bool LaunchesWave => Outcome != ChargeOutcome.Cancelled;
    }

    public sealed class ChargeSnapshot
    {
        public ChargeSnapshot(bool isCharging, double duration, double normalizedDuration)
        {
            IsCharging = isCharging;
            Duration = duration;
            NormalizedDuration = normalizedDuration;
        }

        public bool IsCharging { get; }

        public double Duration { get; }

        public double NormalizedDuration { get; }
    }

    public sealed class ChargeLogic
    {
        private ChargeSettings settings;
        private bool isCharging;
        private double duration;

        public ChargeLogic(ChargeSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public event Action<ChargeRelease> Released;

        public bool IsCharging => isCharging;

        public ChargeSnapshot GetState()
        {
            var normalized = settings.MaxCharge <= 0d ? 0d : Math.Min(1d, duration / settings.MaxCharge);
            return new ChargeSnapshot(isCharging, duration, normalized);
        }

        public void SetSettings(ChargeSettings newSettings)
        {
            settings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
            duration = Math.Min(duration, settings.MaxCharge);
        }

        public bool Begin()
        {
            if (isCharging)
            {
                return false;
            }

            isCharging = true;
            duration = 0d;
            return true;
        }

        public ChargeRelease Tick(double deltaTime)
        {
            if (!isCharging || deltaTime <= 0d)
            {
                return null;
            }

            duration += deltaTime;
            if (duration < settings.MaxCharge)
            {
                return null;
            }

            duration = settings.MaxCharge;
            return End(true);
        }

        public ChargeRelease Release()
        {
            return isCharging ? End(false) : null;
        }

        private ChargeRelease End(bool isAutomatic)
        {
            isCharging = false;
            var release = new ChargeRelease(Evaluate(duration), duration, isAutomatic);
            Released?.Invoke(release);
            return release;
        }

        private ChargeOutcome Evaluate(double value)
        {
            if (value < settings.MinCharge)
            {
                return ChargeOutcome.Cancelled;
            }

            return value >= settings.SweetStart && value <= settings.SweetEnd
                ? ChargeOutcome.SweetSpot
                : ChargeOutcome.Late;
        }
    }
}
