using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;

namespace FireworksApp.Simulation;

public static class DefaultProfiles
{
    public static FireworksProfileSet Create()
    {
        const float canisterSpacingScale = 2f;
        var canisters = new Dictionary<string, CanisterProfile>
        {
            ["c01"] = new CanisterProfile("c01", "M2", new Vector2(-4.0f, -4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.29883623f, 0.9063078f, -0.29883623f)), "basic"),
            ["c02"] = new CanisterProfile("c02", "M3", new Vector2(-2.0f, -4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.13395266f, 0.9238795f, -0.26790532f)), "basic"),
            ["c03"] = new CanisterProfile("c03", "M4", new Vector2(0.0f, -4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.0f, 0.9659258f, -0.25881904f)), "basic"),
            ["c04"] = new CanisterProfile("c04", "M5", new Vector2(2.0f, -4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.13395266f, 0.9238795f, -0.26790532f)), "basic"),
            ["c05"] = new CanisterProfile("c05", "M6", new Vector2(4.0f, -4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.29883623f, 0.9063078f, -0.29883623f)), "basic"),

            ["c06"] = new CanisterProfile("c06", "M8", new Vector2(-4.0f, -2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.26790532f, 0.9238795f, -0.13395266f)), "basic"),
            ["c07"] = new CanisterProfile("c07", "M10", new Vector2(-2.0f, -2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.1648116f, 0.9698463f, -0.1648116f)), "basic"),
            ["c08"] = new CanisterProfile("c08", "M2", new Vector2(0.0f, -2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.0f, 0.9914449f, -0.13052619f)), "donut"),
            ["c09"] = new CanisterProfile("c09", "M3", new Vector2(2.0f, -2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.1648116f, 0.9698463f, -0.1648116f)), "donut"),
            ["c10"] = new CanisterProfile("c10", "M4", new Vector2(4.0f, -2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.26790532f, 0.9238795f, -0.13395266f)), "donut"),

            ["c11"] = new CanisterProfile("c11", "M5", new Vector2(-4.0f, 0.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.25881904f, 0.9659258f, 0.0f)), "donut"),
            ["c12"] = new CanisterProfile("c12", "M6", new Vector2(-2.0f, 0.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.13052619f, 0.9914449f, 0.0f)), "donut"),
            ["c13"] = new CanisterProfile("c13", "M8", new Vector2(0.0f, 0.0f) * canisterSpacingScale, Vector3.UnitY, "donut"),
            ["c14"] = new CanisterProfile("c14", "M10", new Vector2(2.0f, 0.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.13052619f, 0.9914449f, 0.0f)), "donut"),
            ["c15"] = new CanisterProfile("c15", "M2", new Vector2(4.0f, 0.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.25881904f, 0.9659258f, 0.0f)), "chrys"),

            ["c16"] = new CanisterProfile("c16", "M3", new Vector2(-4.0f, 2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.26790532f, 0.9238795f, 0.13395266f)), "chrys"),
            ["c17"] = new CanisterProfile("c17", "M4", new Vector2(-2.0f, 2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.1648116f, 0.9698463f, 0.1648116f)), "chrys"),
            ["c18"] = new CanisterProfile("c18", "M5", new Vector2(0.0f, 2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.0f, 0.9914449f, 0.13052619f)), "chrys"),
            ["c19"] = new CanisterProfile("c19", "M6", new Vector2(2.0f, 2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.1648116f, 0.9698463f, 0.1648116f)), "chrys"),
            ["c20"] = new CanisterProfile("c20", "M8", new Vector2(4.0f, 2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.26790532f, 0.9238795f, 0.13395266f)), "chrys"),

            ["c21"] = new CanisterProfile("c21", "M10", new Vector2(-4.0f, 4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.29883623f, 0.9063078f, 0.29883623f)), "chrys"),
            ["c22"] = new CanisterProfile("c22", "M4", new Vector2(-2.0f, 4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.13395266f, 0.9238795f, 0.26790532f)), "willow"),
            ["c23"] = new CanisterProfile("c23", "M5", new Vector2(0.0f, 4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.0f, 0.9659258f, 0.25881904f)), "willow"),
            ["c24"] = new CanisterProfile("c24", "M6", new Vector2(2.0f, 4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.13395266f, 0.9238795f, 0.26790532f)), "willow"),
            ["c25"] = new CanisterProfile("c25", "M8", new Vector2(4.0f, 4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.29883623f, 0.9063078f, 0.29883623f)), "willow")
        };

        var schemes = new Dictionary<string, ColorScheme>
        {
            ["warm"] = new ColorScheme("warm", new[] { Colors.Gold, Colors.OrangeRed, Colors.Orange }, 0.08f, 1.2f),
            ["cool"] = new ColorScheme("cool", new[] { Colors.DeepSkyBlue, Colors.MediumPurple, Colors.LimeGreen }, 0.08f, 1.2f),
            ["mixed"] = new ColorScheme("mixed", new[] { Colors.Gold, Colors.OrangeRed, Colors.Orange, Colors.DeepSkyBlue, Colors.MediumPurple, Colors.LimeGreen }, 0.1f, 1.5f),
            ["neon"] = new ColorScheme("neon", new[] { Colors.Lime, Colors.Magenta, Colors.Cyan, Colors.HotPink }, 0.12f, 0.8f),
            ["pastel"] = new ColorScheme("pastel", new[] { Colors.LightPink, Colors.LightBlue, Colors.LightGreen, Colors.Lavender }, 0.05f, 2.0f),

            ["debug"] = new ColorScheme("debug", new[] { Colors.White, Colors.Red, Colors.Lime, Colors.Blue, Colors.Yellow, Colors.Cyan, Colors.Magenta }, 0.2f, 0.5f),
            // id, base colors[], variation, boost
            ["gold"] = new ColorScheme(
                "gold",
                new[]
                {
                    Color.FromArgb(255, 255, 220, 120),
                    Color.FromArgb(255, 255, 205, 105),
                    Color.FromArgb(255, 245, 190,  95),
                    Color.FromArgb(255, 230, 175,  80)
                },
                0.04f,
                2.2f
            ),

                        ["brocadegold"] = new ColorScheme(
                "brocadegold",
                new[]
                {
                    Color.FromArgb(255, 235, 180,  80),
                    Color.FromArgb(255, 220, 165,  70),
                    Color.FromArgb(255, 205, 150,  60)
                },
                0.03f,
                2.2f
            ),

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
                RingAxisRandomTiltDegrees: 90.0f),
            ["horsetail_gold"] = new FireworkShellProfile(
                Id: "horsetail_gold",
                BurstShape: FireworkBurstShape.Horsetail,
                ColorSchemeId: "Gold",
                FuseTimeSeconds: 3.2f,             // whatever works with your canister
                ExplosionRadius: 45.0f,
                ParticleCount: 3000,
                ParticleLifetimeSeconds: 3.5f,
                RingAxis: null,
                RingAxisRandomTiltDegrees: 0.0f
            ),

        };

        var groundEffects = new Dictionary<string, GroundEffectProfile>
        {
            ["fountain_warm"] = new GroundEffectProfile(
                Id: "fountain_warm",
                Type: GroundEffectType.Fountain,
                ColorSchemeId: "warm",
                DurationSeconds: 5.0f,
                EmissionRate: 1800.0f,
                ParticleVelocityRange: new Vector2(6.0f, 12.0f),
                ParticleLifetimeSeconds: 1.6f,
                GravityFactor: 0.55f,
                BrightnessScalar: 1.2f,
                ConeAngleDegrees: 40.0f,
                FlickerIntensity: 0.10f,
                SmokeAmount: 0.2f),

            ["spinner_neon"] = new GroundEffectProfile(
                Id: "spinner_neon",
                Type: GroundEffectType.Spinner,
                ColorSchemeId: "neon",
                DurationSeconds: 6.5f,
                EmissionRate: 1400.0f,
                ParticleVelocityRange: new Vector2(5.0f, 9.0f),
                ParticleLifetimeSeconds: 1.4f,
                GravityFactor: 0.35f,
                BrightnessScalar: 1.1f,
                AngularVelocityRadiansPerSec: 9.0f,
                EmissionRadius: 0.22f,
                SmokeAmount: 0.1f),

            ["strobe_cool"] = new GroundEffectProfile(
                Id: "strobe_cool",
                Type: GroundEffectType.Strobe,
                ColorSchemeId: "cool",
                DurationSeconds: 4.5f,
                EmissionRate: 160.0f,
                ParticleVelocityRange: new Vector2(0.3f, 1.2f),
                ParticleLifetimeSeconds: 0.62f,
                GravityFactor: 0.05f,
                BrightnessScalar: 2.0f,
                FlashIntervalSeconds: 0.16f,
                FlashDutyCycle: 0.18f,
                FlashBrightness: 3.4f,
                ResidualSparkDensity: 0.14f),

            ["mine_mixed"] = new GroundEffectProfile(
                Id: "mine_mixed",
                Type: GroundEffectType.Mine,
                ColorSchemeId: "mixed",
                DurationSeconds: 3.0f,
                EmissionRate: 0.0f,
                ParticleVelocityRange: new Vector2(10.0f, 18.0f),
                ParticleLifetimeSeconds: 1.55f,
                GravityFactor: 0.85f,
                BrightnessScalar: 1.3f,
                ConeAngleDegrees: 42.0f,
                BurstRate: 1.9f,
                ParticlesPerBurst: 5200,
                SmokeAmount: 0.55f),
        };

        return new FireworksProfileSet(canisters, shells, groundEffects, schemes);
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
        int gridSize = 5;

        // Kick off a few overlapping ground effects near the start.
        // Use the outer ring of canisters so effects appear around the launch pad perimeter.
        events.Add(new ShowEvent(TimeSeconds: 10.0f, CanisterId: "c01", GroundEffectProfileId: "fountain_warm"));
        events.Add(new ShowEvent(TimeSeconds: 15.0f, CanisterId: "c05", GroundEffectProfileId: "spinner_neon"));
        events.Add(new ShowEvent(TimeSeconds: 20f, CanisterId: "c21", GroundEffectProfileId: "strobe_cool"));
        events.Add(new ShowEvent(TimeSeconds: 25f, CanisterId: "c25", GroundEffectProfileId: "mine_mixed"));

        for (int i = 0; i < 200; i+= gridSize)
        {
            for (int j = 0; j < gridSize; j++)
            {
                string canisterId = profiles.Canisters.Keys.ElementAt((i + j) % profiles.Canisters.Count);
                string shellId = profiles.Shells.Keys.ElementAt((i + j) % profiles.Shells.Count);
                string colorSchemeId = profiles.ColorSchemes.Keys.ElementAt((i + j) % profiles.ColorSchemes.Count);
                float? muzzleVelocity = null;

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
             
                t += 0.2f;
            }
            t += 1f;
        }

        var showScript = new ShowScript(events);
        return showScript;
    }
}
