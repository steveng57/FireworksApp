using System;
using System.Numerics;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace FireworksApp.Rendering;

public sealed partial class D3D11Renderer
{
    public void OnMouseDrag(float deltaX, float deltaY)
    {
        _camera.OnMouseDrag(deltaX, deltaY);
    }

    public void OnMouseWheel(float delta)
    {
        _camera.OnMouseWheel(delta);
    }

    public void PanCamera(float deltaX, float deltaY)
    {
        _camera.Pan(deltaX, deltaY);
    }

    public void Render(float scaledDt)
    {
        if (_context is null || _rtv is null || _swapChain is null)
            return;

        bool useInterpolatedState = _hasInterpolatedState;
        _hasInterpolatedState = false;

        if (scaledDt < 0.0f)
            scaledDt = 0.0f;
        if (scaledDt > 0.05f)
            scaledDt = 0.05f;

        UpdateParticles(scaledDt, useInterpolatedState);
        UpdateSceneConstants(scaledDt);

        _context.RSSetViewport(new Viewport(0, 0, _width, _height, 0.0f, 1.0f));

        if (_padRasterizerState != null)
            _context.RSSetState(_padRasterizerState);

        if (_depthStencilState != null)
            _context.OMSetDepthStencilState(_depthStencilState, 0);

        _context.OMSetRenderTargets(_rtv, _dsv);
        _context.ClearRenderTargetView(_rtv, new Color4(0.02f, 0.02f, 0.05f, 1.0f));
        if (_dsv != null)
            _context.ClearDepthStencilView(_dsv, DepthStencilClearFlags.Depth, 1.0f, 0);

        DrawGround();
        DrawLaunchPad();
        DrawBleachers();
        DrawCanisters(useInterpolatedState);

        DrawParticles(additive: true);
        DrawParticles(additive: false);

        _swapChain.Present(1, PresentFlags.None);

        _perf.Tick("Render", appendDetails: () =>
        {
            var counters = _particlesPipeline.GetPerfCountersSummary();
            if (!string.IsNullOrEmpty(counters))
                System.Diagnostics.Debug.Write($" kinds[{counters}]");
        });
    }

    public void SetShells(System.Collections.Generic.IReadOnlyList<ShellRenderState> shells)
    {
        _prevShells = _shells;
        _shells = shells ?? Array.Empty<ShellRenderState>();
        _hasInterpolatedState = false;
    }

    public void SetCanisters(System.Collections.Generic.IReadOnlyList<CanisterRenderState> canisters)
    {
        _prevCanisters = _canisters;
        _canisters = canisters ?? Array.Empty<CanisterRenderState>();
        _hasInterpolatedState = false;
    }

    public void ApplyInterpolation(float alpha)
    {
        alpha = Math.Clamp(alpha, 0.0f, 1.0f);

        if (alpha <= 0.0f)
            return;

        if (_prevShells.Count == 0 || _prevCanisters.Count == 0)
            return;

        if (_prevShells.Count != _shells.Count || _prevCanisters.Count != _canisters.Count)
            return;

        int shellCount = _shells.Count;
        if (_interpShells is null || _interpShells.Length != shellCount)
            _interpShells = shellCount == 0 ? null : new ShellRenderState[shellCount];

        if (_interpShells is not null)
        {
            for (int i = 0; i < shellCount; i++)
            {
                var aPos = _prevShells[i].Position;
                var bPos = _shells[i].Position;
                var aVel = _prevShells[i].Velocity;
                var bVel = _shells[i].Velocity;
                _interpShells[i] = new ShellRenderState(
                    Vector3.Lerp(aPos, bPos, alpha),
                    Vector3.Lerp(aVel, bVel, alpha));
            }
        }

        int canisterCount = _canisters.Count;
        if (_interpCanisters is null || _interpCanisters.Length != canisterCount)
            _interpCanisters = canisterCount == 0 ? null : new CanisterRenderState[canisterCount];

        if (_interpCanisters is not null)
        {
            for (int i = 0; i < canisterCount; i++)
            {
                var aPos = _prevCanisters[i].Position;
                var bPos = _canisters[i].Position;
                _interpCanisters[i] = new CanisterRenderState(Vector3.Lerp(aPos, bPos, alpha), _canisters[i].Direction);
            }
        }

        _hasInterpolatedState = true;
    }

    private void UpdateSceneConstants(float dt)
    {
        if (_context is null || _sceneCB is null)
            return;

        _camera.Update(dt, _width, _height);

        _view = _camera.View;
        _proj = _camera.Projection;
        CameraPosition = _camera.Position;

        var world = Matrix4x4.Identity;
        var wvp = Matrix4x4.Transpose(world * _view * _proj);

        var scene = new SceneCBData
        {
            WorldViewProjection = wvp,
            World = Matrix4x4.Transpose(world)
        };

        var mapped = _context.Map(_sceneCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        unsafe
        {
            *(SceneCBData*)mapped.DataPointer = scene;
        }
        _context.Unmap(_sceneCB, 0);

        if (_lightingCB != null)
        {
            var light = new LightingCBData
            {
                LightDirectionWS = Vector3.Normalize(new Vector3(-0.3f, -1.0f, -0.2f)),
                LightColor = new Vector3(1.0f, 1.0f, 1.0f),
                AmbientColor = new Vector3(0.15f, 0.15f, 0.18f)
            };

            var mappedLight = _context.Map(_lightingCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            unsafe
            {
                *(LightingCBData*)mappedLight.DataPointer = light;
            }
            _context.Unmap(_lightingCB, 0);
        }
    }

    private void DrawGround()
    {
        if (_context is null)
            return;

        _groundPipeline.Draw(_context, _sceneCB, _lightingCB, _padRasterizerState);
    }

    private void DrawLaunchPad()
    {
        if (_context is null)
            return;

        _padPipeline.Draw(_context, _sceneCB);
    }

    private void DrawCanisters(bool useInterpolatedState)
    {
        if (_context is null)
            return;

        var canisters = (useInterpolatedState && _interpCanisters is not null)
            ? (System.Collections.Generic.IReadOnlyList<CanisterRenderState>)_interpCanisters
            : _canisters;

        _canisterPipeline.Draw(
            _context,
            canisters,
            _view,
            _proj,
            _sceneCB,
            _lightingCB,
            _objectCB);
    }

    private void DrawShells(bool useInterpolatedState)
    {
        if (_context is null)
            return;

        var shells = (useInterpolatedState && _interpShells is not null)
            ? (System.Collections.Generic.IReadOnlyList<ShellRenderState>)_interpShells
            : _shells;

        _shellPipeline.Draw(_context, shells, _view, _proj, _sceneCB, _objectCB);
    }
}
