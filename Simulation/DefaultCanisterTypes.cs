using System.Collections.Generic;

namespace FireworksApp.Simulation;

public static class DefaultCanisterTypes
{
    public static readonly IReadOnlyList<CanisterType> All =
    [
        new CanisterType("M2", 2.0f, 51f, 305f, 33f, 40f, 70f, 2f),
        new CanisterType("M3", 3.0f, 76f, 457f, 40f, 50f, 100f, 3f),
        new CanisterType("M4", 4.0f, 102f, 610f, 46f, 56f, 135f, 4f),
        new CanisterType("M5", 5.0f, 127f, 762f, 52f, 63f, 170f, 5f),
        new CanisterType("M6", 6.0f, 152f, 914f, 57f, 69f, 200f, 6f),
        new CanisterType("M8", 8.0f, 203f, 1118f, 65f, 80f, 270f, 8f),
        new CanisterType("M10", 10.0f, 254f, 1524f, 73f, 89f, 335f, 10f),
    ];
}
