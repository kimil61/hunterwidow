using System;
using System.Collections.Generic;
using HunterWidow.Domain.Common;
using HunterWidow.Domain.Content;

namespace HunterWidow.Domain.Enemy
{
    /// <summary>
    /// Inputs supplied by Unity to a deterministic enemy movement behavior.
    /// Contact detection and rendering stay in Unity; the movement decision stays
    /// pure and can be checked without a scene.
    /// </summary>
    public sealed class EnemyBehaviorContext
    {
        public EnemyBehaviorContext(
            Vec2 position,
            Vec2 playerPosition,
            double moveSpeed,
            double wanderDistance,
            double wanderMoveMultiplier,
            double retreatDistance,
            double elapsedSeconds)
        {
            Position = position;
            PlayerPosition = playerPosition;
            MoveSpeed = moveSpeed;
            WanderDistance = wanderDistance;
            WanderMoveMultiplier = wanderMoveMultiplier;
            RetreatDistance = retreatDistance;
            ElapsedSeconds = elapsedSeconds;
        }

        public Vec2 Position { get; }

        public Vec2 PlayerPosition { get; }

        public double MoveSpeed { get; }

        public double WanderDistance { get; }

        public double WanderMoveMultiplier { get; }

        public double RetreatDistance { get; }

        public double ElapsedSeconds { get; }

        public Vec2 OffsetToPlayer => PlayerPosition - Position;
    }

    /// <summary>
    /// An enemy behavior declares its own content schema and returns movement only.
    /// This keeps the same behavior implementation reusable across colored/stat
    /// variants authored in enemies.json.
    /// </summary>
    public interface IEnemyBehavior : IContentBehaviorSchema
    {
        Vec2 GetVelocity(EnemyBehaviorContext context);
    }

    public static class EnemyBehaviorRegistry
    {
        private static readonly IEnemyBehavior[] behaviors =
        {
            new ChaserEnemyBehavior(),
            new WandererEnemyBehavior(),
            new RangedEnemyBehavior(),
            new TrainingEnemyBehavior()
        };

        public static IReadOnlyList<IEnemyBehavior> All => behaviors;

        public static bool TryGet(string behaviorId, out IEnemyBehavior behavior)
        {
            for (var behaviorIndex = 0; behaviorIndex < behaviors.Length; behaviorIndex++)
            {
                if (string.Equals(behaviors[behaviorIndex].BehaviorId, behaviorId, StringComparison.Ordinal))
                {
                    behavior = behaviors[behaviorIndex];
                    return true;
                }
            }

            behavior = null;
            return false;
        }
    }

    public abstract class EnemyBehaviorBase : IEnemyBehavior
    {
        private static readonly string[] Parameters =
        {
            "maxHealth",
            "moveSpeed",
            "contactDamage",
            "wanderDistance",
            "wanderMoveMultiplier",
            "retreatDistance"
        };

        public string ContentType => "enemy";

        public abstract string BehaviorId { get; }

        public IReadOnlyList<string> RequiredNumericParameters => Parameters;

        public abstract Vec2 GetVelocity(EnemyBehaviorContext context);

        protected static Vec2 Chase(EnemyBehaviorContext context)
        {
            return context.OffsetToPlayer.Normalized * context.MoveSpeed;
        }
    }

    public sealed class ChaserEnemyBehavior : EnemyBehaviorBase
    {
        public override string BehaviorId => "chaser";

        public override Vec2 GetVelocity(EnemyBehaviorContext context)
        {
            return Chase(context);
        }
    }

    public sealed class WandererEnemyBehavior : EnemyBehaviorBase
    {
        public override string BehaviorId => "wanderer";

        public override Vec2 GetVelocity(EnemyBehaviorContext context)
        {
            if (context.OffsetToPlayer.Length > context.WanderDistance)
            {
                return new Vec2(Math.Sin(context.ElapsedSeconds), Math.Cos(context.ElapsedSeconds))
                    * context.MoveSpeed
                    * context.WanderMoveMultiplier;
            }

            return Chase(context);
        }
    }

    public sealed class RangedEnemyBehavior : EnemyBehaviorBase
    {
        public override string BehaviorId => "ranged";

        public override Vec2 GetVelocity(EnemyBehaviorContext context)
        {
            if (context.OffsetToPlayer.Length < context.RetreatDistance)
            {
                return context.OffsetToPlayer.Normalized * -context.MoveSpeed;
            }

            return Chase(context);
        }
    }

    public sealed class TrainingEnemyBehavior : EnemyBehaviorBase
    {
        public override string BehaviorId => "training";

        public override Vec2 GetVelocity(EnemyBehaviorContext context)
        {
            return new Vec2(0d, 0d);
        }
    }
}
