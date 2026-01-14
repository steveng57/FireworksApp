using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Media;

namespace FireworksApp.Simulation;

public static class DefaultProfiles
{
    public static FireworksProfileSet Create()
    {
        const float canisterSpacingScale = 1.5f;

        // Shell-launch canisters (keep as-is, centered around the pad).
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

        // Ground-effect canisters: separate set, placed around the pad border.
        // The pad border spans 8m..10m; use ~9m for the centerline.
        const float groundHalf = 9.0f;
        var groundCanisters = new Dictionary<string, CanisterProfile>
        {
            ["g01"] = new CanisterProfile("g01", "M2", new Vector2(-groundHalf, -groundHalf), Vector3.UnitY, "basic"),
            ["g02"] = new CanisterProfile("g02", "M3", new Vector2(0.0f, -groundHalf), Vector3.UnitY, "basic"),
            ["g03"] = new CanisterProfile("g03", "M4", new Vector2(groundHalf, -groundHalf), Vector3.UnitY, "basic"),
            ["g04"] = new CanisterProfile("g04", "M5", new Vector2(groundHalf, 0.0f), Vector3.UnitY, "basic"),
            ["g05"] = new CanisterProfile("g05", "M6", new Vector2(groundHalf, groundHalf), Vector3.UnitY, "basic"),
            ["g06"] = new CanisterProfile("g06", "M8", new Vector2(0.0f, groundHalf), Vector3.UnitY, "basic"),
            ["g07"] = new CanisterProfile("g07", "M10", new Vector2(-groundHalf, groundHalf), Vector3.UnitY, "basic"),
            ["g08"] = new CanisterProfile("g08", "M6", new Vector2(-groundHalf, 0.0f), Vector3.UnitY, "basic"),
        };

        foreach (var kvp in groundCanisters)
            canisters.Add(kvp.Key, kvp.Value);

        var schemes = new Dictionary<string, ColorScheme>
        {
            ["warm"] = new ColorScheme("warm", new[] { Colors.Gold, Colors.OrangeRed, Colors.Orange }, 0.08f, 1.2f),
            ["cool"] = new ColorScheme("cool", new[] { Colors.DeepSkyBlue, Colors.MediumPurple, Colors.LimeGreen }, 0.08f, 1.2f),
            ["mixed"] = new ColorScheme("mixed", new[] { Colors.Gold, Colors.OrangeRed, Colors.Orange, Colors.DeepSkyBlue, Colors.MediumPurple, Colors.LimeGreen }, 0.1f, 1.5f),
            ["neon"] = new ColorScheme("neon", new[] { Colors.Lime, Colors.Magenta, Colors.Cyan, Colors.HotPink }, 0.12f, 0.8f),
            ["pastel"] = new ColorScheme("pastel", new[] { Colors.LightPink, Colors.LightBlue, Colors.LightGreen, Colors.Lavender }, 0.05f, 2.0f),
            ["white"] = new ColorScheme("white", new[] { Colors.White }, 0.02f, 3.0f),
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
            ["basic"] = ShellPresets.Create(
                id: "basic",
                burstShape: FireworkBurstShape.Peony,
                colorSchemeId: "warm",
                fuseTimeSeconds: 3.8f,
                explosionRadius: 12.0f,
                particleCount: 5000,
                particleLifetimeSeconds: 2.5f,
                burstSparkleRateHz: 12.0f,
                burstSparkleIntensity: 0.35f,
                burstSpeed: 9.0f),

            ["chrys"] = ShellPresets.Create(
                id: "chrys",
                burstShape: FireworkBurstShape.Chrysanthemum,
                colorSchemeId: "mixed",
                fuseTimeSeconds: 3.9f,
                explosionRadius: 13.0f,
                particleCount: 5500,
                particleLifetimeSeconds: 5.0f,
                burstSparkleRateHz: 14.0f,
                burstSparkleIntensity: 0.45f,
                emission: BurstEmissionSettings.Defaults with
                {
                    ChrysanthemumSpokeCount = 24,
                    ChrysanthemumSpokeJitter = 0.12f,
                }),

            ["willow"] = ShellPresets.Create(
                id: "willow",
                burstShape: FireworkBurstShape.Willow,
                colorSchemeId: "pastel",
                fuseTimeSeconds: 4.2f,
                explosionRadius: 15.0f,
                particleCount: 5000,
                particleLifetimeSeconds: 6.0f,
                burstSparkleRateHz: 8.0f,
                burstSparkleIntensity: 0.25f,
                emission: BurstEmissionSettings.Defaults with
                {
                    WillowDownwardBlend = 0.35f,
                }),

            ["palm"] = ShellPresets.Create(
                id: "palm",
                burstShape: FireworkBurstShape.Palm,
                colorSchemeId: "warm",
                fuseTimeSeconds: 4.0f,
                explosionRadius: 16.0f,
                particleCount: 5000,
                particleLifetimeSeconds: 4.5f,
                burstSparkleRateHz: 18.0f,
                burstSparkleIntensity: 0.65f,
                emission: BurstEmissionSettings.Defaults with
                {
                    PalmFrondCount = 7,
                    PalmFrondConeAngleRadians = 0.65f,
                    PalmFrondJitterAngleRadians = 0.08f,
                }),

            ["donut"] = ShellPresets.Create(
                id: "donut",
                burstShape: FireworkBurstShape.Ring,
                colorSchemeId: "cool",
                fuseTimeSeconds: 4.1f,
                explosionRadius: 14.0f,
                particleCount: 5000,
                particleLifetimeSeconds: 3.2f,
                burstSparkleRateHz: 5.0f,
                burstSparkleIntensity: 0.65f,
                ringAxis: Vector3.UnitY,
                ringAxisRandomTiltDegrees: 90.0f),

            ["horsetail_gold"] = ShellPresets.Create(
                id: "horsetail_gold",
                burstShape: FireworkBurstShape.Horsetail,
                colorSchemeId: "gold",
                fuseTimeSeconds: 3.2f,             // whatever works with your canister
                explosionRadius: 45.0f,
                particleCount: 4000,
                particleLifetimeSeconds: 1.5f,
                burstSparkleRateHz: 6.0f,
                burstSparkleIntensity: 0.20f,
                ringAxis: Vector3.UnitY,
                ringAxisRandomTiltDegrees: 25.0f),

            ["double_ring"] = ShellPresets.Create(
                id: "double_ring",
                burstShape: FireworkBurstShape.DoubleRing,
                colorSchemeId: "gold",   // nice classy gold rings
                fuseTimeSeconds: 4.1f,
                explosionRadius: 16.0f,
                particleCount: 4500,
                particleLifetimeSeconds: 4.5f,
                burstSparkleRateHz: 2.0f,
                burstSparkleIntensity: 0.65f,
                ringAxis: Vector3.UnitY,
                ringAxisRandomTiltDegrees: 18.0f),

            ["spiral"] = ShellPresets.Create(
                id: "spiral",
                burstShape: FireworkBurstShape.Spiral,
                colorSchemeId: "neon",   // loud & colorful
                fuseTimeSeconds: 4.0f,
                explosionRadius: 14.0f,
                particleCount: 5500,
                particleLifetimeSeconds: 4.5f,
                burstSparkleRateHz: 16.0f,
                burstSparkleIntensity: 0.55f,
                ringAxis: Vector3.UnitY,
                ringAxisRandomTiltDegrees: 25.0f),

            ["spoke_wheel_pop"] = ShellPresets.Create(
                id: "spoke_wheel_pop",
                burstShape: FireworkBurstShape.SubShellSpokeWheelPop,
                colorSchemeId: "warm",
                fuseTimeSeconds: 3.6f,
                explosionRadius: 0.0f,
                particleCount: 0,
                particleLifetimeSeconds: 0.0f,
                burstSparkleRateHz: 0.0f,
                burstSparkleIntensity: 0.0f,
                ringAxis: Vector3.UnitY,
                ringAxisRandomTiltDegrees: 180.0f,
                trailParticleCount: ShellTrailPresets.ShortBright.Count,
                trailParticleLifetimeSeconds: ShellTrailPresets.ShortBright.LifetimeSeconds,
                trailSpeed: ShellTrailPresets.ShortBright.Speed,
                trailSmokeChance: ShellTrailPresets.ShortBright.SmokeChance,
                subShellSpokeWheelPop: SubShellSpokeWheelPopParams.Defaults with
                {
                    SubShellCount = 12,
                    RingStartAngleDegrees = 0.0f,
                    RingEndAngleDegrees = 360.0f,
                    RingRadius = 25.0f,
                    SubShellSpeed = 10.0f,
                    SubShellFuseMinSeconds = 0.50f,
                    SubShellFuseMaxSeconds = 1.5f,
                    PopFlashParticleCount = 2000,
                    PopFlashLifetime = 0.12f,
                    PopFlashRadius = 1.2f,
                    PopFlashIntensity = 8.0f,
                    PopFlashFadeGamma = 2.2f,
                    PopFlashColorSchemeId = "white",
                    SubShellGravityScale = 0.95f,
                    SubShellDrag = 0.07f,
                    AngleJitterDegrees = 3.5f,
                    TangentialSpeed = 3.0f,
                    RingAxis = Vector3.UnitY,
                    RingAxisRandomTiltDegrees = 180.0f
                }),

            ["willow_trail_only"] = ShellPresets.Create(
                id: "willow_trail_only",
                burstShape: FireworkBurstShape.Willow,
                colorSchemeId: "pastel",
                fuseTimeSeconds: 2.0f,
                explosionRadius: 0.0f,
                particleCount: 0,
                particleLifetimeSeconds: 0.0f,
                suppressBurst: true,
                terminalFadeOutSeconds: 1.5f,
                trailParticleCount: ShellTrailPresets.WillowLingering.Count,
                trailParticleLifetimeSeconds: ShellTrailPresets.WillowLingering.LifetimeSeconds,
                trailSpeed: ShellTrailPresets.WillowLingering.Speed,
                trailSmokeChance: ShellTrailPresets.WillowLingering.SmokeChance),

            ["peony_to_willow"] = ShellPresets.Create(
                id: "peony_to_willow",
                burstShape: FireworkBurstShape.PeonyToWillow,
                colorSchemeId: "gold",
                fuseTimeSeconds: 4.0f,
                explosionRadius: 14.0f,
                particleCount: 100,
                particleLifetimeSeconds: 3.0f,
                burstSparkleRateHz: 10.0f,
                burstSparkleIntensity: 0.40f,
                peonyToWillow: PeonyToWillowParams.Defaults with
                {
                    WillowSubshellProfileId = "subshell_willow_trail_only",
                    //WillowSubshellProfileId = "subshell_ring_sparkle",
                    //WillowSubshellProfileId = "subshell_basic_pop",
                    WillowVelocityScale = 0.50f,
                    WillowGravityMultiplier = 2.2f,
                    WillowDragMultiplier = 2.4f,
                    WillowLifetimeMultiplier = 2.2f,
                    WillowTrailSpawnRate = 8f,
                    WillowTrailSpeed = 2.2f
                }),

            // Finale: scatter mini report shells that pop as a single bright white flash.
            // Note: SubShells now support smoke trails. Tune TrailParticleCount/TrailParticleLifetime/TrailSmokeChance to balance visual quality vs performance.
            ["finale_salute"] = ShellPresets.Create(
                    id: "finale_salute",
                    burstShape: FireworkBurstShape.FinaleSalute,
                    colorSchemeId: "debug", // ignored by PopFlash, but useful for debugging
                    fuseTimeSeconds: 3.8f,
                    explosionRadius: 0.0f,
                    particleCount: 0,
                    particleLifetimeSeconds: 0.10f,
                    burstSparkleRateHz: 0.0f,
                    burstSparkleIntensity: 0.0f,
                    finaleSalute: FinaleSaluteParams.Defaults with 
                    { 
                        SubShellCount = 75,
                        // Trail tuning: reduce counts if performance is an issue
                        EnableSubShellTrails = true,
                        TrailParticleCount = 6,
                        TrailParticleLifetime = 0.4f,
                        TrailSpeed = 3.0f,
                        TrailSmokeChance = 0.15f,
                        SparkParticleCount = 3000,
                    }
                ),

                // Comet: beautiful streaming trails that fade naturally without explosions.
                // Fully configurable trail colors, particle counts, and physics.
                //["comet_gold"] = new FireworkShellProfile(
                //    Id: "comet_gold",
                //    BurstShape: FireworkBurstShape.Comet,
                //    ColorSchemeId: "gold",
                //    FuseTimeSeconds: 3.8f,
                //    ExplosionRadius: 0.0f,
                //    ParticleCount: 0,
                //    ParticleLifetimeSeconds: 0.0f,
                //    BurstSparkleRateHz: 0.0f,
                //    BurstSparkleIntensity: 0.0f,
                //    Comet: CometParams.Defaults with
                //    {
                //        CometCount = 40,
                //        CometSpeedMin = 12f,
                //        CometSpeedMax = 22f,
                //        CometUpBias = 0.35f,
                //        CometGravityScale = 0.75f,
                //        CometDrag = 0.05f,
                //        CometLifetimeSeconds = 5.0f,
                //        TrailParticleCount = 10,
                //        TrailParticleLifetime = 0.6f,
                //        TrailSpeed = 4.0f,
                //        TrailSmokeChance = 0.25f,
                //        TrailColor = null  // null = use shell's color scheme
                //    }
                //),

                ["comet_neon"] = ShellPresets.Create(
                    id: "comet_neon",
                    burstShape: FireworkBurstShape.Comet,
                    colorSchemeId: "neon",
                    fuseTimeSeconds: 3.9f,
                    explosionRadius: 0.0f,
                    particleCount: 0,
                    particleLifetimeSeconds: 0.0f,
                    burstSparkleRateHz: 0.0f,
                    burstSparkleIntensity: 0.0f,
                            comet: CometParams.Defaults with
                            {
                                CometCount = 50,
                                CometSpeedMin = 15f,
                                CometSpeedMax = 28f,
                                CometUpBias = 0.25f,
                                CometGravityScale = 0.85f,
                                CometDrag = 0.04f,
                                CometLifetimeSeconds = 4.5f,
                                TrailParticleCount = 12,
                                TrailParticleLifetime = 0.5f,
                                TrailSpeed = 5.0f,
                                TrailSmokeChance = 0.18f,
                    // Custom vivid cyan trail color
                    TrailColor = new Vector4(0.3f, 1.5f, 2.0f, 1.0f),
                    SubShellProfileId = null,
                    SubShellDelaySeconds = 2.0f,
                            }
                ),
        };

        // SubShell profiles: reusable child shell behaviors
        var subshellProfiles = new Dictionary<string, SubShellProfile>
        {
            ["subshell_basic_pop"] = SubShellPresets.Sphere(
                id: "subshell_basic_pop",
                shellProfileId: "basic",
                count: 12,
                minAltitudeToSpawn: 5.0f),

            ["subshell_willow_trail_only"] = SubShellPresets.Sphere(
                id: "subshell_willow_trail_only",
                shellProfileId: "willow_trail_only",
                count: 12,
                minAltitudeToSpawn: 5.0f),

            ["subshell_ring_sparkle"] = SubShellPresets.Ring(
                id: "subshell_ring_sparkle",
                shellProfileId: "donut",
                count: 24,
                minAltitudeToSpawn: 8.0f,
                colorSchemeId: "neon")
        };

        var groundEffects = new Dictionary<string, GroundEffectProfile>
        {
            ["fountain_warm"] = GroundEffectPresets.Fountain(
                id: "fountain_warm",
                colorSchemeId: "warm",
                durationSeconds: 5.0f,
                emissionRate: 1800.0f,
                particleVelocityRange: new Vector2(6.0f, 12.0f),
                particleLifetimeSeconds: 1.6f,
                gravityFactor: 0.55f,
                brightnessScalar: 1.2f,
                coneAngleDegrees: 40.0f,
                flickerIntensity: 0.10f,
                smokeAmount: 0.2f),

            ["spinner_neon"] = GroundEffectPresets.Spinner(
                id: "spinner_neon",
                colorSchemeId: "neon",
                durationSeconds: 6.5f,
                emissionRate: 1400.0f,
                particleVelocityRange: new Vector2(5.0f, 9.0f),
                particleLifetimeSeconds: 1.4f,
                gravityFactor: 0.35f,
                brightnessScalar: 1.1f,
                heightOffsetMeters: 2.0f,
                angularVelocityRadiansPerSec: 9.0f,
                emissionRadius: 0.22f,
                spinnerAxis: Vector3.UnitY,
                smokeAmount: 0.1f),

            //["strobe_cool"] = new GroundEffectProfile(
            //    Id: "strobe_cool",
            //    Type: GroundEffectType.Strobe,
            //    ColorSchemeId: "cool",
            //    DurationSeconds: 4.5f,
            //    EmissionRate: 160.0f,
            //    ParticleVelocityRange: new Vector2(0.3f, 1.2f),
            //    ParticleLifetimeSeconds: 0.62f,
            //    GravityFactor: 0.05f,
            //    BrightnessScalar: 2.0f,
            //    FlashIntervalSeconds: 0.16f,
            //    FlashDutyCycle: 0.18f,
            //    FlashBrightness: 3.4f,
            //    ResidualSparkDensity: 0.14f),


            ["spinner_neon_v"] = GroundEffectPresets.Spinner(
                id: "spinner_neon_v",
                colorSchemeId: "neon",
                durationSeconds: 6.5f,
                emissionRate: 1400.0f,
                particleVelocityRange: new Vector2(5.0f, 9.0f),
                particleLifetimeSeconds: 1.4f,
                gravityFactor: 0.35f,
                brightnessScalar: 1.1f,
                heightOffsetMeters: 3.0f,
                angularVelocityRadiansPerSec: 9.0f,
                emissionRadius: 0.22f,
                spinnerAxis: Vector3.UnitX,
                smokeAmount: 0.1f),

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

            ["bengal_warm"] = new GroundEffectProfile(
                Id: "bengal_warm",
                Type: GroundEffectType.BengalFlare,
                ColorSchemeId: "warm",
                DurationSeconds: 8.0f,
                EmissionRate: 900.0f,
                ParticleVelocityRange: new Vector2(1.2f, 2.6f),
                ParticleLifetimeSeconds: 0.55f,
                GravityFactor: 0.20f,
                BrightnessScalar: 2.0f,
                FlameHeightMeters: 1.6f,
                FlameNoiseAmplitude: 0.22f,
                LocalLightRadiusMeters: 7.0f,
                LocalLightIntensity: 1.3f,
                OccasionalSparkRate: 60.0f,
                SmokeAmount: 0.15f),

            ["lance_heart"] = new GroundEffectProfile(
                Id: "lance_heart",
                Type: GroundEffectType.LanceworkPanel,
                ColorSchemeId: "neon",
                DurationSeconds: 7.0f,
                EmissionRate: 0.0f,
                ParticleVelocityRange: new Vector2(0.5f, 1.2f),
                ParticleLifetimeSeconds: 0.55f,
                GravityFactor: 0.12f,
                BrightnessScalar: 1.5f,
                GridWidth: 8,
                GridHeight: 8,
                PatternFrameDurationSeconds: 0.30f,
                CellFlameHeightMeters: 0.30f,
                CellFlickerAmount: 0.18f,
                PatternFrames: new ulong[]
                {
                    // simple heart-ish 8x8, two-frame "beat" by toggling center pixels
                    0b_00000000_01100110_11111111_11111111_01111110_00111100_00011000_00000000UL,
                    0b_00000000_01100110_11111111_11111111_01111110_00011000_00000000_00000000UL,
                },
                SmokeAmount: 0.05f),

            ["waterfall_gold"] = new GroundEffectProfile(
                Id: "waterfall_gold",
                Type: GroundEffectType.WaterfallCurtain,
                ColorSchemeId: "gold",
                DurationSeconds: 10.0f,
                EmissionRate: 420.0f,
                ParticleVelocityRange: new Vector2(2.5f, 4.0f),
                ParticleLifetimeSeconds: 2.8f,
                GravityFactor: 1.20f,
                BrightnessScalar: 1.4f,
                EmitterCount: 28,
                EmitterHeightMeters: 6.0f,
                SparkFallSpeed: 3.6f,
                CurtainWidthMeters: 16.0f,
                DensityOverTime: 1.0f,
                SmokeAmount: 0.20f),

            ["chaser_zipper"] = new GroundEffectProfile(
                Id: "chaser_zipper",
                Type: GroundEffectType.ChaserLine,
                ColorSchemeId: "neon",
                DurationSeconds: 6.0f,
                EmissionRate: 0.0f,
                ParticleVelocityRange: new Vector2(6.0f, 10.0f),
                ParticleLifetimeSeconds: 0.65f,
                GravityFactor: 0.70f,
                BrightnessScalar: 1.3f,
                PointCount: 22,
                PointSpacingMeters: 0.65f,
                ChaseSpeed: 8.5f,
                BurstParticlesPerPoint: 950,
                BurstVelocity: 9.5f,
                ReverseOrBounce: true,
                SmokeAmount: 0.12f),

            ["bloom_brocade"] = new GroundEffectProfile(
                Id: "bloom_brocade",
                Type: GroundEffectType.GroundBloom,
                ColorSchemeId: "brocadegold",
                DurationSeconds: 7.0f,
                EmissionRate: 1600.0f,
                ParticleVelocityRange: new Vector2(5.0f, 9.0f),
                ParticleLifetimeSeconds: 1.25f,
                GravityFactor: 0.35f,
                BrightnessScalar: 1.2f,
                AngularVelocityRadiansPerSec: 14.0f,
                SpinRateOverTime: -0.8f,
                GroundDriftVelocity: new Vector3(0.15f, 0.0f, -0.10f),
                SmokeAmount: 0.10f),

            ["glitter_pulse"] = new GroundEffectProfile(
                Id: "glitter_pulse",
                Type: GroundEffectType.PulsingGlitterFountain,
                ColorSchemeId: "cool",
                DurationSeconds: 7.5f,
                EmissionRate: 1600.0f,
                ParticleVelocityRange: new Vector2(5.5f, 10.5f),
                ParticleLifetimeSeconds: 1.35f,
                GravityFactor: 0.52f,
                BrightnessScalar: 1.35f,
                ConeAngleDegrees: 36.0f,
                FlickerIntensity: 0.08f,
                PulseFrequencyHz: 6.5f,
                PulseDepth: 0.75f,
                GlitterParticleRatio: 0.40f,
                GlowDecayTimeSeconds: 0.16f,
                SmokeAmount: 0.18f),
        };

        var profileSet = new FireworksProfileSet(canisters, shells, groundEffects, schemes, subshellProfiles);
        ProfileValidator.Validate(profileSet);
        return profileSet;
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



        //// Kick off a few overlapping ground effects near the start.
        //// Use dedicated ground-effect canisters placed on the pad border.
        //events.Add(new ShowEvent(TimeSeconds: 10.0f, CanisterId: "g01", GroundEffectProfileId: "fountain_warm"));
        //events.Add(new ShowEvent(TimeSeconds: 15.0f, CanisterId: "g03", GroundEffectProfileId: "spinner_neon"));
        //events.Add(new ShowEvent(TimeSeconds: 20f, CanisterId: "g05", GroundEffectProfileId: "spinner_neon_v"));
        //events.Add(new ShowEvent(TimeSeconds: 25f, CanisterId: "g07", GroundEffectProfileId: "mine_mixed"));

        //// New ground effects showcase
        //events.Add(new ShowEvent(TimeSeconds: 30f, CanisterId: "g02", GroundEffectProfileId: "bengal_warm"));
        //events.Add(new ShowEvent(TimeSeconds: 33f, CanisterId: "g04", GroundEffectProfileId: "glitter_pulse"));
        //events.Add(new ShowEvent(TimeSeconds: 36f, CanisterId: "g06", GroundEffectProfileId: "bloom_brocade"));
        //events.Add(new ShowEvent(TimeSeconds: 39f, CanisterId: "g08", GroundEffectProfileId: "chaser_zipper"));
        //events.Add(new ShowEvent(TimeSeconds: 42f, CanisterId: "g05", GroundEffectProfileId: "lance_heart"));
        //events.Add(new ShowEvent(TimeSeconds: 46f, CanisterId: "g06", GroundEffectProfileId: "waterfall_gold"));

        //// Comet showcase - beautiful streaming trails
        //events.Add(new ShowEvent(TimeSeconds: 52f, CanisterId: "c13", ShellProfileId: "comet_gold"));
        //events.Add(new ShowEvent(TimeSeconds: 54f, CanisterId: "c07", ShellProfileId: "comet_neon"));
        //events.Add(new ShowEvent(TimeSeconds: 56f, CanisterId: "c19", ShellProfileId: "comet_gold"));
        //events.Add(new ShowEvent(TimeSeconds: 58f, CanisterId: "c03", ShellProfileId: "comet_neon"));

        var mainShowShells = profiles.Shells.Where(kvp => !(kvp.Key == "finale_salute" || kvp.Key == "willow_trail_only")).ToList();
        //var mainShowShells = profiles.Shells.Where(kvp => !(kvp.Key == "finale_salute" || kvp.Key == "comet_neon" || kvp.Key == "peony_to_willow")).ToList();
        int mainCanisters = 25;
        for (int i = 0; i < 50; i += gridSize)
        {
            for (int j = 0; j < gridSize; j++)
            {
                string shellId = mainShowShells[(i + j) % mainShowShells.Count].Key;

                string canisterId = profiles.Canisters.Keys.ElementAt((i + j) % mainCanisters);
                string colorSchemeId = profiles.ColorSchemes.Keys.ElementAt((i + j) % profiles.ColorSchemes.Count);

                // debug variations
                //shellId = "spoke_wheel_pop";
                // shellId = "peony_to_willow";
                // shellId = "spiral";
                //canisterId = "c2";
                //colorSchemeId = "debug";

                var showEvent = new ShowEvent(
                    TimeSeconds: t,
                    CanisterId: canisterId,
                    ShellProfileId: shellId,
                    ColorSchemeId: colorSchemeId
                    );
                events.Add(showEvent);

                t += 0.20f;
            }
            t += 5f;
        }

        t += 4.0f;

        for (int n = 0; n < 20; n += 2)
        {
            string canisterId = profiles.Canisters.Keys.ElementAt(n % mainCanisters);
            var finaleEvent = new ShowEvent(
                TimeSeconds: t,
                CanisterId: canisterId,
                ShellProfileId: "comet_neon");
            events.Add(finaleEvent);
            t += 1.0f;

            canisterId = profiles.Canisters.Keys.ElementAt((n + 1) % mainCanisters);
            finaleEvent = new ShowEvent(
                TimeSeconds: t,
                CanisterId: canisterId,
                ShellProfileId: "peony_to_willow");
            events.Add(finaleEvent);
            t += 1.0f;
        }

        t += 8.0f;

        for (int n = 0; n < 10; n++)
        {
            string canisterId = profiles.Canisters.Keys.ElementAt(n % 25);
            var finaleEvent = new ShowEvent(
                TimeSeconds: t,
                CanisterId: canisterId,
                ShellProfileId: "finale_salute");
            events.Add(finaleEvent);
            t += 1.5f;
        }

        var showScript = new ShowScript(events);
        return showScript;
    }
}
