using System;
using VRageMath;

namespace TargetView;

public static class Utils
{
    public static float CubicEaseIn(float val)
    {
        return (float)Math.Pow(val, 3);
    }

    public static float CubicEaseOut(float val)
    {
        return (float)(1 - Math.Pow(1 - val, 3));
    }

    public static Vector2I Lerp(Vector2I a, Vector2I b, float s)
    {
        return new Vector2I
        {
            X = (int)Math.Round(MathHelper.Lerp(a.X, b.X, s)),
            Y = (int)Math.Round(MathHelper.Lerp(a.Y, b.Y, s)),
        };
    }
}