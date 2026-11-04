// 월드 XZ 평면 좌표용 최소 벡터 (Unity 의존 없는 AI 계산에 사용)
using System;

namespace Ashveil.Domain
{
    public readonly struct Vec2
    {
        public readonly float X;
        public readonly float Z;

        public Vec2(float x, float z)
        {
            X = x;
            Z = z;
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Z * s);

        public float Length => (float)Math.Sqrt(X * X + Z * Z);

        public Vec2 Normalized
        {
            get
            {
                var len = Length;
                return len > 1e-5f ? new Vec2(X / len, Z / len) : new Vec2(0, 0);
            }
        }

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;

        public override string ToString() => $"({X:0.0}, {Z:0.0})";
    }
}
