using Sandbox.Graphics;
using System;
using VRage.Utils;
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

    public static Vector3 ComputeWorldRayDir(Vector2 uv, Matrix viewMatrix, Matrix projMatrix)
    {
        float ray_x = 1.0f / projMatrix.M11;
        float ray_y = 1.0f / projMatrix.M22;
        Vector3 projOffset = new Vector3(projMatrix.M31 / projMatrix.M11, projMatrix.M32 / projMatrix.M22, 0);
        Vector3 screenRay = projOffset + new Vector3(MathHelper.Lerp(-ray_x, ray_x, uv.X), -MathHelper.Lerp(-ray_y, ray_y, uv.Y), -1.0f);
        screenRay = Vector3.Normalize(screenRay);

        Vector3 worldRay = Vector3.TransformNormal(screenRay, Matrix.Transpose(viewMatrix));
        return worldRay;
    }

    public static void DrawMouseCursor(string mouseCursorTexture, Vector2 uv, float sizeInPx)
    {
        if (mouseCursorTexture != null)
        {
            Rectangle fullscreenRect = MyGuiManager.GetFullscreenRectangle();
            Vector2 normalizedCoord = MyGuiManager.GetNormalizedCoordinateFromScreenCoordinate(uv * new Vector2(fullscreenRect.Width, fullscreenRect.Height));

            Vector2 normalizedSize = MyGuiManager.GetNormalizedSize(new Vector2(sizeInPx), 1f);

            MyGuiManager.DrawSpriteBatch(mouseCursorTexture, normalizedCoord, normalizedSize, Color.White, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, useFullClientArea: false, waitTillLoaded: false, null, 0f, 0f, ignoreBounds: true);
        }
    }

}