using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;

namespace FireworksApp.Simulation;

public static class DefaultProfiles
{
    public static FireworksProfileSet Create()
    {
        var canisters = new Dictionary<string, CanisterProfile>
        {
            ["c1"] = new CanisterProfile(
                Id: "c1",
                Position: new Vector2(0, 0),
                MuzzleVelocity: 55.0f,
                ReloadTimeSeconds: 2.5f,
                DefaultShellProfileId: "basic"),

            ["c2"] = new CanisterProfile(
                Id: "c2",
                Position: new Vector2(-2.5f, 0),
                MuzzleVelocity: 58.0f,
                ReloadTimeSeconds: 3.0f,
                DefaultShellProfileId: "donut")
        };

        var schemes = new Dictionary<string, ColorScheme>
        {
            ["warm"] = new ColorScheme("warm", new[] { Colors.Gold, Colors.OrangeRed, Colors.Orange }, 0.08f, 1.2f),
            ["cool"] = new ColorScheme("cool", new[] { Colors.DeepSkyBlue, Colors.MediumPurple, Colors.LimeGreen }, 0.08f, 1.2f)
        };

        var shells = new Dictionary<string, FireworkShellProfile>
        {
            ["basic"] = new FireworkShellProfile(
                Id: "basic",
                Style: "sphere",
                ColorSchemeId: "warm",
                FuseTimeSeconds: 3.8f,
                ExplosionRadius: 12.0f,
                ParticleCount: 6000,
                ParticleLifetimeSeconds: 3.2f),

            ["donut"] = new FireworkShellProfile(
                Id: "donut",
                Style: "donut",
                ColorSchemeId: "cool",
                FuseTimeSeconds: 4.1f,
                ExplosionRadius: 14.0f,
                ParticleCount: 7000,
                ParticleLifetimeSeconds: 3.2f)
        };

        return new FireworksProfileSet(canisters, shells, schemes);
    }
}

public static class DefaultShow
{
    public static ShowScript Create()
    {
        // Short loop show (placeholder for JSON/YAML loading).
        var events = new List<ShowEvent>();
        var profiles = DefaultProfiles.Create();
        float t = 0;
        for (int i = 0; i < 60; i++)
        {
            string canisterId = i % 2 == 0 ? "c1" : "c2";
            string shellId = i % 2 == 0 ? "basic" : "donut";

            float muzzleVelocity = profiles.Canisters[canisterId].MuzzleVelocity;
            string colorSchemeId = i % 2 == 0 ? "warm" : "cool";

            var showEvent = new ShowEvent(
                TimeSeconds: t,
                CanisterId: canisterId,
                ShellProfileId: shellId,
                ColorSchemeId: colorSchemeId,
                MuzzleVelocity: muzzleVelocity);
            events.Add(showEvent);
            t += 1.0f;
        }

        var showScript = new ShowScript(events);
        return showScript;
    }
}
