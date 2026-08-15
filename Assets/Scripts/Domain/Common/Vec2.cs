using System;

namespace HunterWidow.Domain.Common
{
    public readonly struct Vec2
    {
        public Vec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public double Length => Math.Sqrt((X * X) + (Y * Y));

        public Vec2 Normalized
        {
            get
            {
                var length = Length;
                return length <= 0d ? new Vec2(0d, 0d) : new Vec2(X / length, Y / length);
            }
        }

        public static Vec2 operator +(Vec2 left, Vec2 right)
        {
            return new Vec2(left.X + right.X, left.Y + right.Y);
        }

        public static Vec2 operator -(Vec2 left, Vec2 right)
        {
            return new Vec2(left.X - right.X, left.Y - right.Y);
        }

        public static Vec2 operator *(Vec2 value, double scalar)
        {
            return new Vec2(value.X * scalar, value.Y * scalar);
        }

        public static double Distance(Vec2 left, Vec2 right)
        {
            return (left - right).Length;
        }
    }
}
