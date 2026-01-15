using System;
using System.Numerics;

namespace FireworksApp.Camera;

public sealed class CameraController
{
    private CameraProfile _profile = CameraProfiles.Standard;

    private float _yaw;
    private float _pitch;
    private float _distance;

    private float _yawTarget;
    private float _pitchTarget;
    private float _distanceTarget;

    private Vector3 _target;
    private Vector3 _targetSmoothed;
    private float _distanceSmoothed;
    private float _orbitAngle;
    private float _orbitUserOffset;
    private float _orbitUserOffsetTarget;

    public Matrix4x4 View { get; private set; }
    public Matrix4x4 Projection { get; private set; }
    public Vector3 Position { get; private set; }

    public CameraProfile Profile => _profile;
    public bool IsDirty { get; private set; }

    public void SetProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("profileId is required", nameof(profileId));

        if (!CameraProfiles.All.TryGetValue(profileId, out var profile))
            throw new ArgumentException($"Unknown camera profile '{profileId}'", nameof(profileId));

        SetProfile(profile);
    }

    public void SetProfile(CameraProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _profile = profile;
        if (profile.Kind == CameraProfileKind.FixedCinematic)
        {
            _target = profile.FixedTarget;
            _targetSmoothed = _target;

            var (yaw, pitch, distance) = DeriveOrientation(profile.FixedPosition, profile.FixedTarget);
            _yaw = yaw;
            _yawTarget = yaw;
            _pitch = pitch;
            _pitchTarget = pitch;
            _distance = distance;
            _distanceTarget = distance;
            _distanceSmoothed = distance;
            _orbitAngle = 0.0f;
            _orbitUserOffset = 0.0f;
            _orbitUserOffsetTarget = 0.0f;
        }
        else
        {
            _target = new Vector3(0.0f, profile.TargetHeightMeters, 0.0f);
            _targetSmoothed = _target;
            _distance = profile.DefaultDistanceMeters;
            _distanceTarget = profile.DefaultDistanceMeters;
            _distanceSmoothed = profile.DefaultDistanceMeters;
            _pitch = profile.DefaultPitchRadians;
            _pitchTarget = profile.DefaultPitchRadians;
            _yaw = 0.0f;
            _yawTarget = 0.0f;
            _orbitAngle = 0.0f;
            _orbitUserOffset = 0.0f;
            _orbitUserOffsetTarget = 0.0f;
        }
        IsDirty = true;
    }

    public void OnMouseDrag(float deltaX, float deltaY)
    {
        if (_profile.Kind == CameraProfileKind.FixedCinematic)
            return;

        const float sensitivity = 0.01f;
        if (_profile.Kind == CameraProfileKind.AerialOrbit || _profile.Kind == CameraProfileKind.GroundOrbit)
        {
            _orbitUserOffsetTarget += deltaX * sensitivity;
        }
        else
        {
            _yawTarget += deltaX * sensitivity;
        }

        _pitchTarget += deltaY * sensitivity;
        _pitchTarget = System.Math.Clamp(_pitchTarget, -1.2f, 1.2f);
        IsDirty = true;
    }

    public void OnMouseWheel(float delta)
    {
        if (_profile.Kind == CameraProfileKind.FixedCinematic)
            return;

        _distanceTarget *= (float)System.Math.Pow(0.9, delta / 120.0);
        _distanceTarget = System.Math.Clamp(_distanceTarget, 5.0f, 450.0f);
        IsDirty = true;
    }

    public void Pan(float deltaX, float deltaY)
    {
        if (!_profile.AllowPan)
            return;

        float k = 0.0025f * _distance;
        var right = new Vector3(View.M11, View.M21, View.M31);
        var up = new Vector3(View.M12, View.M22, View.M32);

        _target += (-right * deltaX + up * deltaY) * k;
        IsDirty = true;
    }

    public void MarkDirty() => IsDirty = true;

    public void Update(float dt, int width, int height)
    {
        width = System.Math.Max(1, width);
        height = System.Math.Max(1, height);

        bool isFixed = _profile.Kind == CameraProfileKind.FixedCinematic;

        if (isFixed)
        {
            _target = _profile.FixedTarget;
        }
        else if (!_profile.AllowPan)
        {
            _target = new Vector3(0.0f, _profile.TargetHeightMeters, 0.0f);
        }

        bool snap = dt <= 0.0f || isFixed;

        if (snap)
        {
            _yaw = _yawTarget;
            _pitch = _pitchTarget;
            _distance = _distanceTarget;
            _orbitUserOffset = _orbitUserOffsetTarget;
        }
        else
        {
            const float angleSmoothSpeed = 7.5f; // No-op test
            const float distanceSmoothSpeed = 6.0f;
            float angleT = 1.0f - (float)System.Math.Exp(-angleSmoothSpeed * dt);
            float distT = 1.0f - (float)System.Math.Exp(-distanceSmoothSpeed * dt);

            _yaw += (_yawTarget - _yaw) * angleT;
            _pitch += (_pitchTarget - _pitch) * angleT;
            _distance += (_distanceTarget - _distance) * distT;
            _orbitUserOffset += (_orbitUserOffsetTarget - _orbitUserOffset) * angleT;
        }

        // Smooth follow of target and distance.
        if (snap)
        {
            _targetSmoothed = _target;
            _distanceSmoothed = _distance;
        }
        else
        {
            const float followSpeed = 5.0f;
            const float zoomSpeed = 5.0f;

            float followT = 1.0f - (float)System.Math.Exp(-followSpeed * dt);
            float zoomT = 1.0f - (float)System.Math.Exp(-zoomSpeed * dt);

            if (followT < 0.0f) followT = 0.0f;
            if (followT > 1.0f) followT = 1.0f;
            if (zoomT < 0.0f) zoomT = 0.0f;
            if (zoomT > 1.0f) zoomT = 1.0f;

            _targetSmoothed = Vector3.Lerp(_targetSmoothed, _target, followT);
            _distanceSmoothed = _distanceSmoothed + (_distance - _distanceSmoothed) * zoomT;
        }

        float yaw;
        if (_profile.Kind == CameraProfileKind.AerialOrbit || _profile.Kind == CameraProfileKind.GroundOrbit)
        {
            if (dt > 0.0f)
            {
                _orbitAngle += _profile.OrbitSpeedRadiansPerSecond * dt;
                const float twoPi = (float)(System.Math.PI * 2.0);
                if (_orbitAngle > twoPi || _orbitAngle < -twoPi)
                    _orbitAngle = System.MathF.IEEERemainder(_orbitAngle, twoPi);
            }

            yaw = _orbitAngle + _orbitUserOffset;
        }
        else
        {
            yaw = _yaw;
        }

        float cy = (float)System.Math.Cos(yaw);
        float sy = (float)System.Math.Sin(yaw);
        float cp = (float)System.Math.Cos(_pitch);
        float sp = (float)System.Math.Sin(_pitch);

        float aspect = height > 0 ? (float)width / height : 1.0f;
        var up = Vector3.UnitY;

        if (isFixed)
        {
            var fixedTarget = _profile.FixedTarget;
            var fixedEye = _profile.FixedPosition;

            Position = fixedEye;
            View = Matrix4x4.CreateLookAt(fixedEye, fixedTarget, up);
            Projection = Matrix4x4.CreatePerspectiveFieldOfView(_profile.FieldOfViewRadians, aspect, 1.0f, 2000.0f);
            IsDirty = false;
            return;
        }

        var eyeOffset = new Vector3(sy * cp, sp, cy * cp) * _distanceSmoothed;
        var target = _targetSmoothed;
        var eye = target + eyeOffset;

        Position = eye;
        View = Matrix4x4.CreateLookAt(eye, target, up);
        Projection = Matrix4x4.CreatePerspectiveFieldOfView(_profile.FieldOfViewRadians, aspect, 1.0f, 2000.0f);

        IsDirty = false;
    }

    private static (float yaw, float pitch, float distance) DeriveOrientation(Vector3 eye, Vector3 target)
    {
        var offset = eye - target;
        float distance = offset.Length();
        if (distance <= 1e-4f)
            return (0.0f, 0.0f, 0.1f);

        var dir = offset / distance;
        float pitch = (float)System.Math.Asin(dir.Y);
        float yaw = (float)System.Math.Atan2(dir.X, dir.Z);
        return (yaw, pitch, distance);
    }
}
