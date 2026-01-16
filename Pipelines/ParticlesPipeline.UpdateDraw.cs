using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using FireworksApp.Simulation;

namespace FireworksApp.Rendering;

internal sealed partial class ParticlesPipeline
{
    public void Update(ID3D11DeviceContext context, Matrix4x4 view, Matrix4x4 proj, Vector3 schemeTint, float scaledDt)
    {
        if (_cs is null || _particleUAV is null || _frameCB is null || _perKindCountersUAV is null)
            return;

        DispatchPendingSpawns(context);

        var right = new Vector3(view.M11, view.M21, view.M31);
        var up = new Vector3(view.M12, view.M22, view.M32);
        var vp = Matrix4x4.Transpose(view * proj);

        var frame = new FrameCBData
        {
            ViewProjection = vp,
            CameraRightWS = right,
            DeltaTime = scaledDt,
            CameraUpWS = up,
            Time = (float)(Environment.TickCount64 / 1000.0),

            SmokeFadeInFraction = Tunables.SmokeFadeInFraction,
            SmokeFadeOutStartFraction = Tunables.SmokeFadeOutStartFraction,

            CrackleBaseColor = ParticleConstants.CrackleBaseColor,
            CrackleBaseSize = ParticleConstants.CrackleBaseSize,
            CracklePeakColor = ParticleConstants.CracklePeakColor,
            CrackleFlashSizeMul = ParticleConstants.CrackleFlashSizeMul,
            CrackleFadeColor = ParticleConstants.CrackleFadeColor,
            CrackleTau = ParticleConstants.CrackleTau,

            SchemeTint = schemeTint
        };

        var mapped = context.Map(_frameCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        Marshal.StructureToPtr(frame, mapped.DataPointer, false);
        context.Unmap(_frameCB, 0);

        context.UpdateSubresource(s_counterZeros, _perKindCountersBuffer);

        Array.Clear(_uavs);
        Array.Clear(_initialCounts);

        _uavs[0] = _particleUAV;
        _initialCounts[0] = uint.MaxValue;

        var kinds = s_allKinds;
        for (int k = 0; k < kinds.Length; k++)
        {
            if (_aliveUAVByKind.TryGetValue(kinds[k], out var kindUav))
            {
                _uavs[k + 1] = kindUav;
                _initialCounts[k + 1] = 0;
            }
        }

        _uavs[6] = _perKindCountersUAV;
        _initialCounts[6] = uint.MaxValue;

        if (_detonationUAV is not null)
        {
            _uavs[7] = _detonationUAV;
            _initialCounts[7] = 0;
        }

        context.CSSetShader(_cs);
        context.CSSetConstantBuffer(0, _frameCB);
        context.CSSetUnorderedAccessViews(0, _uavs, _initialCounts);

        uint groups = (uint)((_capacity + 255) / 256);
        context.Dispatch(groups, 1, 1);

        context.CSSetUnorderedAccessViews(0, _nullUavs, null);
        context.CSSetShader(null);

        if (_enableCounterReadback && _perKindCountersReadback is not null && _perKindCountersBuffer is not null)
        {
            context.CopyResource(_perKindCountersReadback, _perKindCountersBuffer);

            var mappedCounters = context.Map(_perKindCountersReadback, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                unsafe
                {
                    uint* src = (uint*)mappedCounters.DataPointer;
                    _lastAliveCountByKind[ParticleKind.Shell] = (int)src[1];
                    _lastAliveCountByKind[ParticleKind.Spark] = (int)src[2];
                    _lastAliveCountByKind[ParticleKind.Smoke] = (int)src[3];
                    _lastAliveCountByKind[ParticleKind.Crackle] = (int)src[4];
                    _lastAliveCountByKind[ParticleKind.PopFlash] = (int)src[5];
                }
            }
            finally
            {
                context.Unmap(_perKindCountersReadback, 0);
            }
        }

        if (_detonationCountBuffer is not null && _detonationUAV is not null)
        {
            context.CopyStructureCount(_detonationCountBuffer, 0, _detonationUAV);
        }

        foreach (var kind in kinds)
        {
            if (!_aliveUAVByKind.TryGetValue(kind, out var uav) || uav is null)
                continue;
            if (!_aliveDrawArgsByKind.TryGetValue(kind, out var argsBuf) || argsBuf is null)
                continue;

            context.CopyStructureCount(argsBuf, sizeof(uint), uav);
        }
    }

    public void Draw(ID3D11DeviceContext context, Matrix4x4 view, Matrix4x4 proj, Vector3 schemeTint, ID3D11DepthStencilState? depthStencilState, bool additive)
    {
        if (_vs is null || _ps is null || _particleSRV is null || _frameCB is null)
            return;

        var kinds = additive ? s_kindsAdditive : s_kindsAlpha;

        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.IASetInputLayout(null);
        context.IASetVertexBuffers(0, 0, Array.Empty<ID3D11Buffer>(), Array.Empty<uint>(), Array.Empty<uint>());

        if (additive)
        {
            context.OMSetDepthStencilState(_depthReadNoWrite, 0);
            context.OMSetBlendState(_blendAdditive, new Color4(0, 0, 0, 0), uint.MaxValue);
        }
        else
        {
            context.OMSetDepthStencilState(null, 0);
            context.OMSetBlendState(_blendAlpha, new Color4(0, 0, 0, 0), uint.MaxValue);
        }

        context.VSSetShader(_vs);
        context.PSSetShader(_ps);
        context.VSSetShaderResource(0, _particleSRV);

        context.VSSetConstantBuffer(0, _frameCB);

        var passCb = additive ? _passCBAdditive : _passCBAlpha;
        if (passCb != null)
        {
            context.VSSetConstantBuffer(1, passCb);
        }
        else if (_passCB != null)
        {
            var mappedPass = context.Map(_passCB, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                var pass = new PassCBData
                {
                    ParticlePass = additive ? 0u : 1u
                };
                Marshal.StructureToPtr(pass, mappedPass.DataPointer, false);
            }
            finally
            {
                context.Unmap(_passCB, 0);
            }

            context.VSSetConstantBuffer(1, _passCB);
        }

        foreach (var kind in kinds)
        {
            if (!_aliveSRVByKind.TryGetValue(kind, out var srv) || srv is null)
                continue;

            if (!_aliveDrawArgsByKind.TryGetValue(kind, out var argsBuf) || argsBuf is null)
                continue;

            context.VSSetShaderResource(1, srv);

            context.DrawInstancedIndirect(argsBuf, 0);
        }

        context.VSSetShaderResource(0, null);
        context.VSSetShaderResource(1, null);
        context.OMSetBlendState(null, new Color4(0, 0, 0, 0), uint.MaxValue);
        context.OMSetDepthStencilState(depthStencilState, 0);
    }
}
