using System.Numerics;

namespace FireworksApp.Math;

public sealed class Camera
{
    public Vector3 Position = new(0, 5, -15);
    public Vector3 Target = Vector3.Zero;

    public Matrix4x4 GetView(float aspect)
    {
        return Matrix4x4.CreateLookAt(Position, Target, Vector3.UnitY);
    }
}
