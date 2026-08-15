using System;
using System.Collections.Generic;
using HunterWidow.Domain.Common;

namespace HunterWidow.Domain.Combat
{
    public enum SwordWaveState
    {
        Idle,
        Charging,
        Flying,
        Returning
    }

    public enum WaveHitPhase
    {
        Forward,
        Return
    }

    public enum WaveEndReason
    {
        Returned,
        TimedOut,
        Cancelled
    }

    public sealed class SwordWaveSettings
    {
        public SwordWaveSettings(
            double maxRange,
            double speed,
            double returnSpeed,
            double damage,
            double lateDamageMultiplier,
            double returnDamageMultiplier,
            double timeoutSeconds)
        {
            if (maxRange < 0d || speed <= 0d || returnSpeed <= 0d || damage < 0d || timeoutSeconds <= 0d)
            {
                throw new ArgumentException("Sword wave settings must be in valid ranges.");
            }

            MaxRange = maxRange;
            Speed = speed;
            ReturnSpeed = returnSpeed;
            Damage = damage;
            LateDamageMultiplier = lateDamageMultiplier;
            ReturnDamageMultiplier = returnDamageMultiplier;
            TimeoutSeconds = timeoutSeconds;
        }

        public double MaxRange { get; }

        public double Speed { get; }

        public double ReturnSpeed { get; }

        public double Damage { get; }

        public double LateDamageMultiplier { get; }

        public double ReturnDamageMultiplier { get; }

        public double TimeoutSeconds { get; }
    }

    public sealed class SwordWaveSnapshot
    {
        public SwordWaveSnapshot(SwordWaveState state, Vec2 position, double traveledDistance, double lifetime)
        {
            State = state;
            Position = position;
            TraveledDistance = traveledDistance;
            Lifetime = lifetime;
        }

        public SwordWaveState State { get; }

        public Vec2 Position { get; }

        public double TraveledDistance { get; }

        public double Lifetime { get; }
    }

    public sealed class WaveHit
    {
        public WaveHit(string targetId, WaveHitPhase phase, double damage)
        {
            TargetId = targetId;
            Phase = phase;
            Damage = damage;
        }

        public string TargetId { get; }

        public WaveHitPhase Phase { get; }

        public double Damage { get; }
    }

    public sealed class SwordWaveCompletion
    {
        public SwordWaveCompletion(WaveEndReason reason, int forwardHitCount, int returnHitCount, int roundTripTargetCount)
        {
            Reason = reason;
            ForwardHitCount = forwardHitCount;
            ReturnHitCount = returnHitCount;
            RoundTripTargetCount = roundTripTargetCount;
        }

        public WaveEndReason Reason { get; }

        public int ForwardHitCount { get; }

        public int ReturnHitCount { get; }

        public int RoundTripTargetCount { get; }
    }

    public sealed class SwordWaveLogic
    {
        private readonly HashSet<string> forwardHits = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> returnHits = new HashSet<string>(StringComparer.Ordinal);
        private SwordWaveSettings settings;
        private SwordWaveState state;
        private Vec2 position;
        private Vec2 direction;
        private double traveledDistance;
        private double lifetime;
        private double forwardDamage;

        public SwordWaveLogic(SwordWaveSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public event Action<WaveHit> HitRegistered;

        public event Action<SwordWaveCompletion> Completed;

        public SwordWaveState State => state;

        public SwordWaveSnapshot GetState()
        {
            return new SwordWaveSnapshot(state, position, traveledDistance, lifetime);
        }

        public void SetSettings(SwordWaveSettings newSettings)
        {
            settings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
        }

        public bool BeginCharging()
        {
            if (state != SwordWaveState.Idle)
            {
                return false;
            }

            ResetWaveState();
            state = SwordWaveState.Charging;
            return true;
        }

        public bool Release(ChargeRelease release, Vec2 origin, Vec2 aimDirection)
        {
            if (state != SwordWaveState.Charging || release == null)
            {
                return false;
            }

            if (!release.LaunchesWave)
            {
                Finish(WaveEndReason.Cancelled);
                return true;
            }

            position = origin;
            direction = aimDirection.Normalized;
            if (direction.Length <= 0d)
            {
                direction = new Vec2(1d, 0d);
            }

            forwardDamage = release.Outcome == ChargeOutcome.SweetSpot
                ? settings.Damage
                : settings.Damage * settings.LateDamageMultiplier;
            state = SwordWaveState.Flying;
            return true;
        }

        public void Tick(double deltaTime, Vec2 ownerPosition)
        {
            if ((state != SwordWaveState.Flying && state != SwordWaveState.Returning) || deltaTime <= 0d)
            {
                return;
            }

            lifetime += deltaTime;
            if (lifetime >= settings.TimeoutSeconds)
            {
                Finish(WaveEndReason.TimedOut);
                return;
            }

            if (state == SwordWaveState.Flying)
            {
                TickForward(deltaTime);
                return;
            }

            TickReturn(deltaTime, ownerPosition);
        }

        public bool TryRegisterHit(string targetId, out WaveHit hit)
        {
            hit = null;
            if (string.IsNullOrEmpty(targetId))
            {
                return false;
            }

            if (state == SwordWaveState.Flying && forwardHits.Add(targetId))
            {
                hit = new WaveHit(targetId, WaveHitPhase.Forward, forwardDamage);
            }
            else if (state == SwordWaveState.Returning && returnHits.Add(targetId))
            {
                hit = new WaveHit(targetId, WaveHitPhase.Return, settings.Damage * settings.ReturnDamageMultiplier);
            }

            if (hit == null)
            {
                return false;
            }

            HitRegistered?.Invoke(hit);
            return true;
        }

        private void TickForward(double deltaTime)
        {
            var remainingDistance = Math.Max(0d, settings.MaxRange - traveledDistance);
            var travelDistance = Math.Min(remainingDistance, settings.Speed * deltaTime);
            position += direction * travelDistance;
            traveledDistance += travelDistance;

            if (traveledDistance >= settings.MaxRange)
            {
                state = SwordWaveState.Returning;
            }
        }

        private void TickReturn(double deltaTime, Vec2 ownerPosition)
        {
            var offset = ownerPosition - position;
            var distance = offset.Length;
            var travelDistance = settings.ReturnSpeed * deltaTime;
            if (distance <= travelDistance)
            {
                position = ownerPosition;
                Finish(WaveEndReason.Returned);
                return;
            }

            position += offset.Normalized * travelDistance;
        }

        private void Finish(WaveEndReason reason)
        {
            var roundTripTargetCount = 0;
            foreach (var targetId in forwardHits)
            {
                if (returnHits.Contains(targetId))
                {
                    roundTripTargetCount++;
                }
            }

            var completion = new SwordWaveCompletion(reason, forwardHits.Count, returnHits.Count, roundTripTargetCount);
            ResetWaveState();
            Completed?.Invoke(completion);
        }

        private void ResetWaveState()
        {
            forwardHits.Clear();
            returnHits.Clear();
            position = new Vec2(0d, 0d);
            direction = new Vec2(0d, 0d);
            traveledDistance = 0d;
            lifetime = 0d;
            forwardDamage = 0d;
            state = SwordWaveState.Idle;
        }
    }
}
