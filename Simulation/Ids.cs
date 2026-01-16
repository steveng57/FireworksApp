using System;

namespace FireworksApp.Simulation;

public readonly record struct ShellId
{
    public string Value { get; }

    public ShellId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string ToString() => Value;

    public static implicit operator string(ShellId id) => id.Value;
    public static implicit operator ShellId(string value) => new(value);
}

public readonly record struct SubShellId
{
    public string Value { get; }

    public SubShellId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string ToString() => Value;

    public static implicit operator string(SubShellId id) => id.Value;
    public static implicit operator SubShellId(string value) => new(value);
}

public readonly record struct ColorSchemeId
{
    public string Value { get; }

    public ColorSchemeId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string ToString() => Value;

    public static implicit operator string(ColorSchemeId id) => id.Value;
    public static implicit operator ColorSchemeId(string value) => new(value);
}
