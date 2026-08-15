using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HunterWidow.Domain.Content;

namespace HunterWidow.Domain.Combat
{
    public sealed class CombatTuning
    {
        public CombatTuning(
            double minCharge,
            double sweetStart,
            double sweetEnd,
            double maxCharge,
            double maxRange,
            double waveSpeed,
            double returnSpeed,
            double damage,
            double lateDamageMultiplier,
            double returnDamageMultiplier,
            double waveTimeout,
            double chargeMoveMultiplier)
        {
            MinCharge = minCharge;
            SweetStart = sweetStart;
            SweetEnd = sweetEnd;
            MaxCharge = maxCharge;
            MaxRange = maxRange;
            WaveSpeed = waveSpeed;
            ReturnSpeed = returnSpeed;
            Damage = damage;
            LateDamageMultiplier = lateDamageMultiplier;
            ReturnDamageMultiplier = returnDamageMultiplier;
            WaveTimeout = waveTimeout;
            ChargeMoveMultiplier = chargeMoveMultiplier;
        }

        public double MinCharge { get; }

        public double SweetStart { get; }

        public double SweetEnd { get; }

        public double MaxCharge { get; }

        public double MaxRange { get; }

        public double WaveSpeed { get; }

        public double ReturnSpeed { get; }

        public double Damage { get; }

        public double LateDamageMultiplier { get; }

        public double ReturnDamageMultiplier { get; }

        public double WaveTimeout { get; }

        public double ChargeMoveMultiplier { get; }

        public ChargeSettings ToChargeSettings()
        {
            return new ChargeSettings(MinCharge, SweetStart, SweetEnd, MaxCharge);
        }

        public SwordWaveSettings ToWaveSettings()
        {
            return new SwordWaveSettings(
                MaxRange,
                WaveSpeed,
                ReturnSpeed,
                Damage,
                LateDamageMultiplier,
                ReturnDamageMultiplier,
                WaveTimeout);
        }

        public static CombatTuning FromContent(ContentDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            var combatConfig = ContentConfigRoles.Require(database, ContentConfigRoles.Combat);
            var starterWeaponId = combatConfig.GetString("starterWeaponId");
            ContentItem weapon;
            if (string.IsNullOrEmpty(starterWeaponId)
                || !database.TryGet(starterWeaponId, out weapon)
                || !string.Equals(weapon.Type, "weapon", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A playable combat configuration requires a valid starter weapon ID.");
            }

            IWeaponBehavior behavior;
            if (!WeaponBehaviorRegistry.TryGet(weapon.GetString("behavior"), out behavior))
            {
                throw new InvalidOperationException("Starter weapon behavior is not registered: " + weapon.GetString("behavior"));
            }

            return behavior.CreateTuning(weapon, combatConfig);
        }

        public CombatTuning WithOverrides(IDictionary<string, object> values)
        {
            return new CombatTuning(
                NumberOr(values, "minCharge", MinCharge),
                NumberOr(values, "sweetStart", SweetStart),
                NumberOr(values, "sweetEnd", SweetEnd),
                NumberOr(values, "maxCharge", MaxCharge),
                NumberOr(values, "maxRange", MaxRange),
                NumberOr(values, "waveSpeed", WaveSpeed),
                NumberOr(values, "returnSpeed", ReturnSpeed),
                NumberOr(values, "damage", Damage),
                NumberOr(values, "lateDamageMultiplier", LateDamageMultiplier),
                NumberOr(values, "returnDamageMultiplier", ReturnDamageMultiplier),
                NumberOr(values, "waveTimeout", WaveTimeout),
                NumberOr(values, "chargeMoveMultiplier", ChargeMoveMultiplier));
        }

        private static double NumberOr(IDictionary<string, object> values, string key, double fallback)
        {
            object value;
            return values != null && values.TryGetValue(key, out value) && value is double ? (double)value : fallback;
        }
    }

    public sealed class TuningOverrideStore
    {
        private readonly string filePath;

        public TuningOverrideStore(string filePath)
        {
            this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public CombatTuning Load(CombatTuning defaults)
        {
            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            if (!File.Exists(filePath))
            {
                return defaults;
            }

            var parsed = ContentValues.AsObject(MiniJson.Parse(File.ReadAllText(filePath)));
            return parsed == null ? defaults : defaults.WithOverrides(parsed);
        }

        public void Save(CombatTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = filePath + ".tmp";
            File.WriteAllText(temporaryPath, ToJson(tuning));
            File.Copy(temporaryPath, filePath, true);
            File.Delete(temporaryPath);
        }

        private static string ToJson(CombatTuning tuning)
        {
            return "{\n"
                + "  \"minCharge\": " + Number(tuning.MinCharge) + ",\n"
                + "  \"sweetStart\": " + Number(tuning.SweetStart) + ",\n"
                + "  \"sweetEnd\": " + Number(tuning.SweetEnd) + ",\n"
                + "  \"maxCharge\": " + Number(tuning.MaxCharge) + ",\n"
                + "  \"maxRange\": " + Number(tuning.MaxRange) + ",\n"
                + "  \"waveSpeed\": " + Number(tuning.WaveSpeed) + ",\n"
                + "  \"returnSpeed\": " + Number(tuning.ReturnSpeed) + ",\n"
                + "  \"damage\": " + Number(tuning.Damage) + ",\n"
                + "  \"lateDamageMultiplier\": " + Number(tuning.LateDamageMultiplier) + ",\n"
                + "  \"returnDamageMultiplier\": " + Number(tuning.ReturnDamageMultiplier) + ",\n"
                + "  \"waveTimeout\": " + Number(tuning.WaveTimeout) + ",\n"
                + "  \"chargeMoveMultiplier\": " + Number(tuning.ChargeMoveMultiplier) + "\n"
                + "}\n";
        }

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
