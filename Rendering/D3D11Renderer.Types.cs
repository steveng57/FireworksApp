using System.Numerics;

namespace FireworksApp.Rendering;

public sealed partial class D3D11Renderer
{
    public readonly record struct ShellRenderState(Vector3 Position, Vector3 Velocity);

    public readonly record struct CanisterRenderState(Vector3 Position, Vector3 Direction);
}
