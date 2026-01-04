using System;
using System.Collections.Generic;

namespace FireworksApp.Simulation;

public sealed record class ShowEvent(
    float TimeSeconds,
    string CanisterId,
    string ShellProfileId,
    string? ColorSchemeId = null,
    float? MuzzleVelocity = null);

public sealed record class ShowScript(IReadOnlyList<ShowEvent> Events)
{
    public static readonly ShowScript Empty = new(Array.Empty<ShowEvent>());
}
