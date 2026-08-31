namespace Maniac.Core.Geometry;

public readonly struct Vec2
{
    public static readonly Vec2 PlayfieldCentre = new(256f, 192f);

    public readonly float X;
    public readonly float Y;

    public Vec2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, float t) => new(a.X * t, a.Y * t);

    public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => a + (b - a) * t;
}
