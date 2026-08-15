using System;
using System.Globalization;
using System.IO;

namespace HunterWidow.Domain.Combat
{
    public sealed class CombatTelemetrySnapshot
    {
        public CombatTelemetrySnapshot(
            int cancelledCount,
            int sweetSpotCount,
            int lateCount,
            double averageChargeDuration,
            int launchedWaveCount,
            int forwardHitWaveCount,
            int roundTripHitWaveCount)
        {
            CancelledCount = cancelledCount;
            SweetSpotCount = sweetSpotCount;
            LateCount = lateCount;
            AverageChargeDuration = averageChargeDuration;
            LaunchedWaveCount = launchedWaveCount;
            ForwardHitWaveCount = forwardHitWaveCount;
            RoundTripHitWaveCount = roundTripHitWaveCount;
        }

        public int CancelledCount { get; }

        public int SweetSpotCount { get; }

        public int LateCount { get; }

        public double AverageChargeDuration { get; }

        public int LaunchedWaveCount { get; }

        public int ForwardHitWaveCount { get; }

        public int RoundTripHitWaveCount { get; }

        public int ReleaseCount => CancelledCount + SweetSpotCount + LateCount;

        public double CancelledRatio => Ratio(CancelledCount, ReleaseCount);

        public double SweetSpotRatio => Ratio(SweetSpotCount, ReleaseCount);

        public double LateRatio => Ratio(LateCount, ReleaseCount);

        public double ForwardHitRate => Ratio(ForwardHitWaveCount, LaunchedWaveCount);

        public double RoundTripHitRate => Ratio(RoundTripHitWaveCount, LaunchedWaveCount);

        private static double Ratio(int numerator, int denominator)
        {
            return denominator <= 0 ? 0d : (double)numerator / denominator;
        }
    }

    public sealed class CombatTelemetry
    {
        private int cancelledCount;
        private int sweetSpotCount;
        private int lateCount;
        private double totalChargeDuration;
        private int launchedWaveCount;
        private int forwardHitWaveCount;
        private int roundTripHitWaveCount;

        public void Record(ChargeRelease release)
        {
            if (release == null)
            {
                return;
            }

            totalChargeDuration += release.Duration;
            switch (release.Outcome)
            {
                case ChargeOutcome.Cancelled:
                    cancelledCount++;
                    break;
                case ChargeOutcome.SweetSpot:
                    sweetSpotCount++;
                    launchedWaveCount++;
                    break;
                case ChargeOutcome.Late:
                    lateCount++;
                    launchedWaveCount++;
                    break;
            }
        }

        public void Record(SwordWaveCompletion completion)
        {
            if (completion == null || completion.Reason == WaveEndReason.Cancelled)
            {
                return;
            }

            if (completion.ForwardHitCount > 0)
            {
                forwardHitWaveCount++;
            }

            if (completion.RoundTripTargetCount > 0)
            {
                roundTripHitWaveCount++;
            }
        }

        public CombatTelemetrySnapshot GetState()
        {
            var releaseCount = cancelledCount + sweetSpotCount + lateCount;
            var averageCharge = releaseCount <= 0 ? 0d : totalChargeDuration / releaseCount;
            return new CombatTelemetrySnapshot(
                cancelledCount,
                sweetSpotCount,
                lateCount,
                averageCharge,
                launchedWaveCount,
                forwardHitWaveCount,
                roundTripHitWaveCount);
        }
    }

    /// <summary>
    /// Appends human-readable, spreadsheet-friendly snapshots without depending on Unity APIs.
    /// Every row contains the aggregate state for one combat session, so its final row can be
    /// used directly as a playtest result.
    /// </summary>
    public sealed class CombatTelemetryLogStore
    {
        private const string Header = "timestampUtc,sessionId,event,releaseCount,cancelledCount,sweetSpotCount,lateCount,cancelledRatio,sweetSpotRatio,lateRatio,averageChargeSeconds,launchedWaveCount,forwardHitWaveCount,roundTripHitWaveCount,forwardHitRate,roundTripHitRate,minCharge,sweetStart,sweetEnd,maxCharge,maxRange,waveSpeed,returnSpeed,damage,lateDamageMultiplier,returnDamageMultiplier,waveTimeout,chargeMoveMultiplier";

        private readonly string filePath;

        public CombatTelemetryLogStore(string filePath)
        {
            this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public string FilePath => filePath;

        public void Append(
            DateTime timestampUtc,
            string sessionId,
            string eventName,
            CombatTelemetrySnapshot snapshot,
            CombatTuning tuning)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var needsHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0L;
            var prefix = needsHeader ? Header + Environment.NewLine : string.Empty;
            File.AppendAllText(filePath, prefix + ToCsv(timestampUtc, sessionId, eventName, snapshot, tuning) + Environment.NewLine);
        }

        private static string ToCsv(
            DateTime timestampUtc,
            string sessionId,
            string eventName,
            CombatTelemetrySnapshot snapshot,
            CombatTuning tuning)
        {
            return string.Join(",", new[]
            {
                Quote(timestampUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                Quote(sessionId),
                Quote(eventName),
                snapshot.ReleaseCount.ToString(CultureInfo.InvariantCulture),
                snapshot.CancelledCount.ToString(CultureInfo.InvariantCulture),
                snapshot.SweetSpotCount.ToString(CultureInfo.InvariantCulture),
                snapshot.LateCount.ToString(CultureInfo.InvariantCulture),
                Number(snapshot.CancelledRatio),
                Number(snapshot.SweetSpotRatio),
                Number(snapshot.LateRatio),
                Number(snapshot.AverageChargeDuration),
                snapshot.LaunchedWaveCount.ToString(CultureInfo.InvariantCulture),
                snapshot.ForwardHitWaveCount.ToString(CultureInfo.InvariantCulture),
                snapshot.RoundTripHitWaveCount.ToString(CultureInfo.InvariantCulture),
                Number(snapshot.ForwardHitRate),
                Number(snapshot.RoundTripHitRate),
                Number(tuning.MinCharge),
                Number(tuning.SweetStart),
                Number(tuning.SweetEnd),
                Number(tuning.MaxCharge),
                Number(tuning.MaxRange),
                Number(tuning.WaveSpeed),
                Number(tuning.ReturnSpeed),
                Number(tuning.Damage),
                Number(tuning.LateDamageMultiplier),
                Number(tuning.ReturnDamageMultiplier),
                Number(tuning.WaveTimeout),
                Number(tuning.ChargeMoveMultiplier)
            });
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
