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
                LaunchDirection: Vector3.UnitY,
                MuzzleVelocity: 55.0f,
                ReloadTimeSeconds: 2.5f,
                DefaultShellProfileId: "basic"),

            ["c2"] = new CanisterProfile(
                Id: "c2",
                Position: new Vector2(-2.5f, 0),
                LaunchDirection: Vector3.Normalize(new Vector3(0.0f, MathF.Cos(25.0f * MathF.PI / 180.0f), MathF.Sin(25.0f * MathF.PI / 180.0f))),
                MuzzleVelocity: 58.0f,
                ReloadTimeSeconds: 3.0f,
                DefaultShellProfileId: "donut"),

            ["c3"] = new CanisterProfile(
                Id: "c3",
                Position: new Vector2(2.5f, 0),
                LaunchDirection: Vector3.Normalize(new Vector3(0.0f, MathF.Cos(25.0f * MathF.PI / 180.0f), -MathF.Sin(25.0f * MathF.PI / 180.0f))),
                MuzzleVelocity: 58.0f,
                ReloadTimeSeconds: 3.0f,
                DefaultShellProfileId: "donut")
        };

        var schemes = new Dictionary<string, ColorScheme>
        {
            ["warm"] = new ColorScheme("warm", new[] { Colors.Gold, Colors.OrangeRed, Colors.Orange }, 0.08f, 1.2f),
            ["cool"] = new ColorScheme("cool", new[] { Colors.DeepSkyBlue, Colors.MediumPurple, Colors.LimeGreen }, 0.08f, 1.2f),
            ["mixed"] = new ColorScheme("mixed", new[] { Colors.Gold, Colors.OrangeRed, Colors.Orange, Colors.DeepSkyBlue, Colors.MediumPurple, Colors.LimeGreen }, 0.1f, 1.5f),
            ["neon"] = new ColorScheme("neon", new[] { Colors.Lime, Colors.Magenta, Colors.Cyan, Colors.HotPink }, 0.12f, 0.8f),
            ["pastel"] = new ColorScheme("pastel", new[] { Colors.LightPink, Colors.LightBlue, Colors.LightGreen, Colors.Lavender }, 0.05f, 2.0f)
        };

        var shells = new Dictionary<string, FireworkShellProfile>
        {
            ["basic"] = new FireworkShellProfile(
                Id: "basic",
                BurstShape: FireworkBurstShape.Peony,
                ColorSchemeId: "warm",
                FuseTimeSeconds: 3.8f,
                ExplosionRadius: 12.0f,
                ParticleCount: 6000,
                ParticleLifetimeSeconds: 5.2f),

            ["chrys"] = new FireworkShellProfile(
                Id: "chrys",
                BurstShape: FireworkBurstShape.Chrysanthemum,
                ColorSchemeId: "mixed",
                FuseTimeSeconds: 3.9f,
                ExplosionRadius: 13.0f,
                ParticleCount: 6500,
                ParticleLifetimeSeconds: 5.0f),

            ["willow"] = new FireworkShellProfile(
                Id: "willow",
                BurstShape: FireworkBurstShape.Willow,
                ColorSchemeId: "pastel",
                FuseTimeSeconds: 4.2f,
                ExplosionRadius: 15.0f,
                ParticleCount: 7000,
                ParticleLifetimeSeconds: 6.0f),

            ["palm"] = new FireworkShellProfile(
                Id: "palm",
                BurstShape: FireworkBurstShape.Palm,
                ColorSchemeId: "warm",
                FuseTimeSeconds: 4.0f,
                ExplosionRadius: 16.0f,
                ParticleCount: 5000,
                ParticleLifetimeSeconds: 5.5f),

            ["donut"] = new FireworkShellProfile(
                Id: "donut",
                BurstShape: FireworkBurstShape.Ring,
                ColorSchemeId: "cool",
                FuseTimeSeconds: 4.1f,
                ExplosionRadius: 14.0f,
                ParticleCount: 7000,
                ParticleLifetimeSeconds: 3.2f,
                RingAxis: Vector3.UnitY,
                RingAxisRandomTiltDegrees: 25.0f)
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
        for (int i = 0; i < 80; i++)
        {
            string canisterId = profiles.Canisters.Keys.ElementAt(i % profiles.Canisters.Count);
            string shellId = profiles.Shells.Keys.ElementAt(i % profiles.Shells.Count);
            string colorSchemeId = profiles.ColorSchemes.Keys.ElementAt(i % profiles.ColorSchemes.Count);
            float muzzleVelocity = profiles.Canisters[canisterId].MuzzleVelocity;

            // debug variations
            // shellId = "donut";
            //canisterId = "c2";
            //colorSchemeId = "debug";

            var showEvent = new ShowEvent(
                TimeSeconds: t,
                CanisterId: canisterId,
                ShellProfileId: shellId,
                ColorSchemeId: colorSchemeId,
                MuzzleVelocity: muzzleVelocity);
            events.Add(showEvent);
            // Respect canister reload so scheduled events actually fire.
            // Otherwise, most events are skipped by the engine's CanFire gating.
            t += 1f;
        }

        var showScript = new ShowScript(events);
        return showScript;
    }
}
