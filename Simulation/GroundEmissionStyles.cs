using System;
using System.Numerics;

namespace FireworksApp.Simulation;

public static class GroundEmissionStyles
{
    public static Vector3[] EmitCone(int count, Vector3 axis, float coneAngleRadians, Random rng)
    {
        if (count <= 0)
            return Array.Empty<Vector3>();

        if (axis.LengthSquared() < 1e-8f)
            axis = Vector3.UnitY;
        axis = Vector3.Normalize(axis);

        Vector3 t1 = Vector3.Cross(axis, Vector3.UnitY);
        if (t1.LengthSquared() < 1e-8f)
            t1 = Vector3.Cross(axis, Vector3.UnitX);
        t1 = Vector3.Normalize(t1);
        Vector3 t2 = Vector3.Normalize(Vector3.Cross(axis, t1));

        float cosMax = MathF.Cos(System.Math.Clamp(coneAngleRadians, 0.0f, MathF.PI));

        var dirs = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float u = (float)rng.NextDouble();
            float v = (float)rng.NextDouble();

            float cosTheta = 1.0f - u * (1.0f - cosMax);
            float sinTheta = MathF.Sqrt(MathF.Max(0.0f, 1.0f - cosTheta * cosTheta));
            float phi = MathF.Tau * v;

            Vector3 d = axis * cosTheta + (t1 * MathF.Cos(phi) + t2 * MathF.Sin(phi)) * sinTheta;
            dirs[i] = Vector3.Normalize(d);
        }

        return dirs;
    }

    public static Vector3[] EmitSpinnerTangents(int count, float phaseRadians, Vector3 axis, Random rng)
    {
        if (count <= 0)
            return Array.Empty<Vector3>();

        if (axis.LengthSquared() < 1e-8f)
            axis = Vector3.UnitY;
        axis = Vector3.Normalize(axis);

        Vector3 t1 = Vector3.Cross(axis, Vector3.UnitY);
        if (t1.LengthSquared() < 1e-8f)
            t1 = Vector3.Cross(axis, Vector3.UnitX);
        t1 = Vector3.Normalize(t1);
        Vector3 t2 = Vector3.Normalize(Vector3.Cross(axis, t1));

        var dirs = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float a = phaseRadians + ((float)rng.NextDouble() * 2.0f - 1.0f) * 0.25f;

            // Tangent direction around the chosen axis.
            Vector3 tangent = (-MathF.Sin(a) * t1) + (MathF.Cos(a) * t2);

            float lift = 0.15f + (float)rng.NextDouble() * 0.10f;

            // Add a bit of axis-aligned lift so particles don't stay glued to the plane.
            Vector3 d = tangent + axis * lift;
            dirs[i] = Vector3.Normalize(d);
        }

        return dirs;
    }

    public static Vector3[] EmitDownwardJitter(int count, float lateralJitterRadians, Random rng)
    {
        if (count <= 0)
            return Array.Empty<Vector3>();

        float cone = System.Math.Clamp(lateralJitterRadians, 0.0f, MathF.PI * 0.5f);
        float cosMax = MathF.Cos(cone);

        Vector3 axis = -Vector3.UnitY;
        Vector3 t1 = Vector3.UnitX;
        Vector3 t2 = Vector3.UnitZ;

        var dirs = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float u = (float)rng.NextDouble();
            float v = (float)rng.NextDouble();

            float cosTheta = 1.0f - u * (1.0f - cosMax);
            float sinTheta = MathF.Sqrt(MathF.Max(0.0f, 1.0f - cosTheta * cosTheta));
            float phi = MathF.Tau * v;

            Vector3 d = axis * cosTheta + (t1 * MathF.Cos(phi) + t2 * MathF.Sin(phi)) * sinTheta;
            dirs[i] = Vector3.Normalize(d);
        }

        return dirs;
    }

    public static Vector3[] EmitUpwardPuff(int count, float spreadRadians, Random rng)
    {
        return EmitCone(count, axis: Vector3.UnitY, coneAngleRadians: spreadRadians, rng: rng);
    }
}
