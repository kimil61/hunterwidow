using System;
using System.IO;
using HunterWidow.Domain.Combat;
using HunterWidow.Domain.Common;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class SwordWaveLogicTests
    {
        [Test]
        public void SweetWaveReturnsAndCanHitSameTargetOncePerLeg()
        {
            var logic = new SwordWaveLogic(CreateSettings());
            SwordWaveCompletion completion = null;
            logic.Completed += value => completion = value;

            Assert.That(logic.BeginCharging(), Is.True);
            Assert.That(logic.Release(new ChargeRelease(ChargeOutcome.SweetSpot, 0.6d, false), new Vec2(0d, 0d), new Vec2(1d, 0d)), Is.True);
            logic.Tick(0.25d, new Vec2(0d, 0d));

            WaveHit forwardHit;
            Assert.That(logic.TryRegisterHit("enm_shadow", out forwardHit), Is.True);
            Assert.That(forwardHit.Phase, Is.EqualTo(WaveHitPhase.Forward));
            Assert.That(forwardHit.Damage, Is.EqualTo(20d));
            logic.Tick(0.25d, new Vec2(0d, 0d));
            Assert.That(logic.State, Is.EqualTo(SwordWaveState.Returning));

            WaveHit returnHit;
            Assert.That(logic.TryRegisterHit("enm_shadow", out returnHit), Is.True);
            Assert.That(returnHit.Phase, Is.EqualTo(WaveHitPhase.Return));
            logic.Tick(0.4d, new Vec2(0d, 0d));

            Assert.That(logic.State, Is.EqualTo(SwordWaveState.Idle));
            Assert.That(completion.Reason, Is.EqualTo(WaveEndReason.Returned));
            Assert.That(completion.ForwardHitCount, Is.EqualTo(1));
            Assert.That(completion.ReturnHitCount, Is.EqualTo(1));
            Assert.That(completion.RoundTripTargetCount, Is.EqualTo(1));
        }

        [Test]
        public void WaveTimeoutReturnsToIdleAsSafetyNet()
        {
            var logic = new SwordWaveLogic(new SwordWaveSettings(100d, 10d, 10d, 20d, 0.65d, 1d, 0.1d));
            SwordWaveCompletion completion = null;
            logic.Completed += value => completion = value;
            logic.BeginCharging();
            logic.Release(new ChargeRelease(ChargeOutcome.Late, 1d, true), new Vec2(0d, 0d), new Vec2(1d, 0d));

            logic.Tick(0.1d, new Vec2(0d, 0d));

            Assert.That(logic.State, Is.EqualTo(SwordWaveState.Idle));
            Assert.That(completion.Reason, Is.EqualTo(WaveEndReason.TimedOut));
        }

        [Test]
        public void TelemetrySeparatesTimingAndRoundTripRates()
        {
            var telemetry = new CombatTelemetry();
            telemetry.Record(new ChargeRelease(ChargeOutcome.Cancelled, 0.1d, false));
            telemetry.Record(new ChargeRelease(ChargeOutcome.SweetSpot, 0.6d, false));
            telemetry.Record(new ChargeRelease(ChargeOutcome.Late, 1d, true));
            telemetry.Record(new SwordWaveCompletion(WaveEndReason.Returned, 1, 1, 1));
            telemetry.Record(new SwordWaveCompletion(WaveEndReason.Returned, 0, 0, 0));

            var state = telemetry.GetState();

            Assert.That(state.CancelledRatio, Is.EqualTo(1d / 3d));
            Assert.That(state.SweetSpotRatio, Is.EqualTo(1d / 3d));
            Assert.That(state.LateRatio, Is.EqualTo(1d / 3d));
            Assert.That(state.ForwardHitRate, Is.EqualTo(0.5d));
            Assert.That(state.RoundTripHitRate, Is.EqualTo(0.5d));
        }

        [Test]
        public void TelemetryLogAppendsCsvSnapshotsWithAHeader()
        {
            var directory = Path.Combine(Path.GetTempPath(), "hunterwidow-telemetry-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "combat-telemetry.csv");
            var store = new CombatTelemetryLogStore(path);
            var telemetry = new CombatTelemetry();
            telemetry.Record(new ChargeRelease(ChargeOutcome.SweetSpot, 0.6d, false));
            telemetry.Record(new SwordWaveCompletion(WaveEndReason.Returned, 1, 1, 1));
            var tuning = new CombatTuning(0.1d, 0.5d, 0.75d, 1d, 5d, 10d, 15d, 20d, 0.65d, 1d, 4d, 0.6d);

            try
            {
                store.Append(new DateTime(2026, 8, 15, 4, 30, 0, DateTimeKind.Utc), "session,one", "dive_finished", telemetry.GetState(), tuning);
                store.Append(new DateTime(2026, 8, 15, 4, 31, 0, DateTimeKind.Utc), "session,one", "application_quit", telemetry.GetState(), tuning);

                var lines = File.ReadAllLines(path);
                Assert.That(lines, Has.Length.EqualTo(3));
                Assert.That(lines[0], Does.StartWith("timestampUtc,sessionId,event,releaseCount"));
                Assert.That(lines[1], Does.Contain("\"session,one\""));
                Assert.That(lines[1], Does.Contain("\"dive_finished\""));
                Assert.That(lines[1], Does.Contain(",1,0,1,0,"));
                Assert.That(lines[2], Does.Contain("\"application_quit\""));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static SwordWaveSettings CreateSettings()
        {
            return new SwordWaveSettings(5d, 10d, 15d, 20d, 0.65d, 1d, 4d);
        }
    }
}
