using HunterWidow.Domain.Common;
using HunterWidow.Domain.Enemy;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class EnemyBehaviorTests
    {
        [Test]
        public void RegisteredEnemyBehaviorsChooseMovementWithoutUnityState()
        {
            var context = new EnemyBehaviorContext(
                new Vec2(0d, 0d),
                new Vec2(3d, 0d),
                2d,
                2d,
                0.25d,
                4d,
                0d);

            IEnemyBehavior chaser;
            IEnemyBehavior wanderer;
            IEnemyBehavior ranged;
            IEnemyBehavior training;
            Assert.That(EnemyBehaviorRegistry.TryGet("chaser", out chaser), Is.True);
            Assert.That(EnemyBehaviorRegistry.TryGet("wanderer", out wanderer), Is.True);
            Assert.That(EnemyBehaviorRegistry.TryGet("ranged", out ranged), Is.True);
            Assert.That(EnemyBehaviorRegistry.TryGet("training", out training), Is.True);

            Assert.That(chaser.GetVelocity(context).X, Is.EqualTo(2d));
            Assert.That(wanderer.GetVelocity(context).Y, Is.EqualTo(0.5d));
            Assert.That(ranged.GetVelocity(context).X, Is.EqualTo(-2d));
            Assert.That(training.GetVelocity(context).Length, Is.EqualTo(0d));
        }
    }
}
