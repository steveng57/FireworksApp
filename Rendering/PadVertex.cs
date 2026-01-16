using System.Numerics;
using System.Runtime.InteropServices;

namespace FireworksApp.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct PadVertex
{
    public Vector3 Position;
    public Vector4 Color;

    public PadVertex(float x, float y, float z)
    {
        Position = new Vector3(x, y, z);
        Color = Vector4.One;
    }

    public PadVertex(float x, float y, float z, Vector4 color)
    {
        Position = new Vector3(x, y, z);
        Color = color;
    }
}
