using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Media;
using static FireworksApp.Simulation.DefaultIds;

namespace FireworksApp.Simulation;

internal static class DefaultIds
{
    public const float canisterSpacingScale = 1.5f;

    public const string canTypeM2 = "M2";
    public const string canTypeM3 = "M3";
    public const string canTypeM4 = "M4";
    public const string canTypeM5 = "M5";
    public const string canTypeM6 = "M6";
    public const string canTypeM8 = "M8";
    public const string canTypeM10 = "M10";

    public const string canC01 = "c01";
    public const string canC02 = "c02";
    public const string canC03 = "c03";
    public const string canC04 = "c04";
    public const string canC05 = "c05";
    public const string canC06 = "c06";
    public const string canC07 = "c07";
    public const string canC08 = "c08";
    public const string canC09 = "c09";
    public const string canC10 = "c10";
    public const string canC11 = "c11";
    public const string canC12 = "c12";
    public const string canC13 = "c13";
    public const string canC14 = "c14";
    public const string canC15 = "c15";
    public const string canC16 = "c16";
    public const string canC17 = "c17";
    public const string canC18 = "c18";
    public const string canC19 = "c19";
    public const string canC20 = "c20";
    public const string canC21 = "c21";
    public const string canC22 = "c22";
    public const string canC23 = "c23";
    public const string canC24 = "c24";
    public const string canC25 = "c25";

    public const string canG01 = "g01";
    public const string canG02 = "g02";
    public const string canG03 = "g03";
    public const string canG04 = "g04";
    public const string canG05 = "g05";
    public const string canG06 = "g06";
    public const string canG07 = "g07";
    public const string canG08 = "g08";

    public const string schemeWarm = "warm";
    public const string schemeCool = "cool";
    public const string schemeMixed = "mixed";
    public const string schemeNeon = "neon";
    public const string schemePastel = "pastel";
    public const string schemeWhite = "white";
    public const string schemeDebug = "debug";
    public const string schemeGold = "gold";
    public const string schemeBrocadeGold = "brocadegold";

    public const string shellBasicId = "basic";
    public const string shellChrysId = "chrys";
    public const string shellWillowId = "willow";
    public const string shellPalmId = "palm";
    public const string shellDonutId = "donut";
    public const string shellHorsetailGoldId = "horsetail_gold";
    public const string shellCometGoldStreakId = "comet_gold_streak";
    public const string shellDoubleRingId = "double_ring";
    public const string shellSpiralId = "spiral";
    public const string shellFishId = "fish";
    public const string shellSpokeWheelPopId = "spoke_wheel_pop";
    public const string shellWillowTrailOnlyId = "willow_trail_only";
    public const string shellPeonyToWillowId = "peony_to_willow";
    public const string shellFinaleSaluteId = "finale_salute";
    public const string shellCometNeonId = "comet_neon";
    public const string shellCracklePeonyId = "crackle_peony";
    public const string shellCometCrackleId = "comet_crackle";
    public const string shellStrobeId = "strobe";
    public const string shellStrobeFlashId = "strobe_flash";

    public const string subshellBasicPopId = "subshell_basic_pop";
    public const string subshellWillowTrailOnlyId = "subshell_willow_trail_only";
    public const string subshellRingSparkleId = "subshell_ring_sparkle";
    public const string subshellCracklePeonyId = "subshell_crackle_peony";
    public const string subshellHorsetailCometId = "subshell_horsetail_comet";
    public const string subshellStrobeId = "subshell_strobe";

    public const string groundFountainWarmId = "fountain_warm";
    public const string groundSpinnerNeonId = "spinner_neon";
    public const string groundSpinnerNeonVId = "spinner_neon_v";
    public const string groundMineMixedId = "mine_mixed";
    public const string groundBengalWarmId = "bengal_warm";
    public const string groundLanceHeartId = "lance_heart";
    public const string groundWaterfallGoldId = "waterfall_gold";
    public const string groundChaserZipperId = "chaser_zipper";
    public const string groundBloomBrocadeId = "bloom_brocade";
    public const string groundGlitterPulseId = "glitter_pulse";
}

public static class DefaultProfiles
{

    public static FireworksProfileSet Create()
    {
        // Shell-launch canisters (keep as-is, centered around the pad).
        var canisterProfiles = new Dictionary<string, CanisterProfile>
        {
            [canC01] = new CanisterProfile(canC01, canTypeM2, new Vector2(-4.0f, -4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.29883623f, 0.9063078f, -0.29883623f)), shellBasicId),
            [canC02] = new CanisterProfile(canC02, canTypeM3, new Vector2(-2.0f, -4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.13395266f, 0.9238795f, -0.26790532f)), shellBasicId),
            [canC03] = new CanisterProfile(canC03, canTypeM4, new Vector2(0.0f, -4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.0f, 0.9659258f, -0.25881904f)), shellBasicId),
            [canC04] = new CanisterProfile(canC04, canTypeM5, new Vector2(2.0f, -4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.13395266f, 0.9238795f, -0.26790532f)), shellBasicId),
            [canC05] = new CanisterProfile(canC05, canTypeM6, new Vector2(4.0f, -4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.29883623f, 0.9063078f, -0.29883623f)), shellBasicId),

            [canC06] = new CanisterProfile(canC06, canTypeM8, new Vector2(-4.0f, -2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.26790532f, 0.9238795f, -0.13395266f)), shellBasicId),
            [canC07] = new CanisterProfile(canC07, canTypeM10, new Vector2(-2.0f, -2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.1648116f, 0.9698463f, -0.1648116f)), shellBasicId),
            [canC08] = new CanisterProfile(canC08, canTypeM2, new Vector2(0.0f, -2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.0f, 0.9914449f, -0.13052619f)), shellDonutId),
            [canC09] = new CanisterProfile(canC09, canTypeM3, new Vector2(2.0f, -2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.1648116f, 0.9698463f, -0.1648116f)), shellDonutId),
            [canC10] = new CanisterProfile(canC10, canTypeM4, new Vector2(4.0f, -2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.26790532f, 0.9238795f, -0.13395266f)), shellDonutId),

            [canC11] = new CanisterProfile(canC11, canTypeM5, new Vector2(-4.0f, 0.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.25881904f, 0.9659258f, 0.0f)), shellDonutId),
            [canC12] = new CanisterProfile(canC12, canTypeM6, new Vector2(-2.0f, 0.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.13052619f, 0.9914449f, 0.0f)), shellDonutId),
            [canC13] = new CanisterProfile(canC13, canTypeM8, new Vector2(0.0f, 0.0f) * canisterSpacingScale, Vector3.UnitY, shellDonutId),
            [canC14] = new CanisterProfile(canC14, canTypeM10, new Vector2(2.0f, 0.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.13052619f, 0.9914449f, 0.0f)), shellDonutId),
            [canC15] = new CanisterProfile(canC15, canTypeM2, new Vector2(4.0f, 0.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.25881904f, 0.9659258f, 0.0f)), shellChrysId),

            [canC16] = new CanisterProfile(canC16, canTypeM3, new Vector2(-4.0f, 2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.26790532f, 0.9238795f, 0.13395266f)), shellChrysId),
            [canC17] = new CanisterProfile(canC17, canTypeM4, new Vector2(-2.0f, 2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.1648116f, 0.9698463f, 0.1648116f)), shellChrysId),
            [canC18] = new CanisterProfile(canC18, canTypeM5, new Vector2(0.0f, 2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.0f, 0.9914449f, 0.13052619f)), shellChrysId),
            [canC19] = new CanisterProfile(canC19, canTypeM6, new Vector2(2.0f, 2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.1648116f, 0.9698463f, 0.1648116f)), shellChrysId),
            [canC20] = new CanisterProfile(canC20, canTypeM8, new Vector2(4.0f, 2.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.26790532f, 0.9238795f, 0.13395266f)), shellChrysId),

            [canC21] = new CanisterProfile(canC21, canTypeM10, new Vector2(-4.0f, 4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.29883623f, 0.9063078f, 0.29883623f)), shellChrysId),
            [canC22] = new CanisterProfile(canC22, canTypeM4, new Vector2(-2.0f, 4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(-0.13395266f, 0.9238795f, 0.26790532f)), shellWillowId),
            [canC23] = new CanisterProfile(canC23, canTypeM5, new Vector2(0.0f, 4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.0f, 0.9659258f, 0.25881904f)), shellWillowId),
            [canC24] = new CanisterProfile(canC24, canTypeM6, new Vector2(2.0f, 4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.13395266f, 0.9238795f, 0.26790532f)), shellWillowId),
            [canC25] = new CanisterProfile(canC25, canTypeM8, new Vector2(4.0f, 4.0f) * canisterSpacingScale, Vector3.Normalize(new Vector3(0.29883623f, 0.9063078f, 0.29883623f)), shellWillowId)
        };

        // Ground-effect canisters: separate set, placed around the pad border.
        // The pad border spans 8m..10m; use ~9m for the centerline.
        const float groundHalf = 9.0f;
        var groundCanisters = new Dictionary<string, CanisterProfile>
        {
            [canG01] = new CanisterProfile(canG01, canTypeM2, new Vector2(-groundHalf, -groundHalf), Vector3.UnitY, shellBasicId),
            [canG02] = new CanisterProfile(canG02, canTypeM3, new Vector2(0.0f, -groundHalf), Vector3.UnitY, shellBasicId),
            [canG03] = new CanisterProfile(canG03, canTypeM4, new Vector2(groundHalf, -groundHalf), Vector3.UnitY, shellBasicId),
            [canG04] = new CanisterProfile(canG04, canTypeM5, new Vector2(groundHalf, 0.0f), Vector3.UnitY, shellBasicId),
            [canG05] = new CanisterProfile(canG05, canTypeM6, new Vector2(groundHalf, groundHalf), Vector3.UnitY, shellBasicId),
            [canG06] = new CanisterProfile(canG06, canTypeM8, new Vector2(0.0f, groundHalf), Vector3.UnitY, shellBasicId),
            [canG07] = new CanisterProfile(canG07, canTypeM10, new Vector2(-groundHalf, groundHalf), Vector3.UnitY, shellBasicId),
            [canG08] = new CanisterProfile(canG08, canTypeM6, new Vector2(-groundHalf, 0.0f), Vector3.UnitY, shellBasicId),
        };

        foreach (var kvp in groundCanisters)
            canisterProfiles.Add(kvp.Key, kvp.Value);

        var colorSchemeProfiles = new Dictionary<string, ColorScheme>
        {
            [schemeWarm] = new ColorScheme(schemeWarm, new[] { Colors.Gold, Colors.OrangeRed, Colors.Orange }, 0.08f, 1.2f),
            [schemeCool] = new ColorScheme(schemeCool, new[] { Colors.DeepSkyBlue, Colors.MediumPurple, Colors.LimeGreen }, 0.08f, 1.2f),
            [schemeMixed] = new ColorScheme(schemeMixed, new[] { Colors.Gold, Colors.OrangeRed, Colors.Orange, Colors.DeepSkyBlue, Colors.MediumPurple, Colors.LimeGreen }, 0.1f, 1.5f),
            [schemeNeon] = new ColorScheme(schemeNeon, new[] { Colors.Lime, Colors.Magenta, Colors.Cyan, Colors.HotPink }, 0.12f, 0.8f),
            [schemePastel] = new ColorScheme(schemePastel, new[] { Colors.LightPink, Colors.LightBlue, Colors.LightGreen, Colors.Lavender }, 0.05f, 2.0f),
            [schemeWhite] = new ColorScheme(schemeWhite, new[] { Colors.White }, 0.02f, 3.0f),
            [schemeDebug] = new ColorScheme(schemeDebug, new[] { Colors.White, Colors.Red, Colors.Lime, Colors.Blue, Colors.Yellow, Colors.Cyan, Colors.Magenta }, 0.2f, 0.5f),
            // id, base colors[], variation, boost
            [schemeGold] = new ColorScheme(
                schemeGold,
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

            [schemeBrocadeGold] = new ColorScheme(
                schemeBrocadeGold,
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

        var trailProfiles = new Dictionary<string, TrailProfile>
        {
            [ShellTrailPresets.Default.Id] = ShellTrailPresets.Default,
            [ShellTrailPresets.ShortBright.Id] = ShellTrailPresets.ShortBright,
            [ShellTrailPresets.WillowLingering.Id] = ShellTrailPresets.WillowLingering,
            [ShellTrailPresets.CometNeon.Id] = ShellTrailPresets.CometNeon,
            ["trail_none"] = new TrailProfile(
                Id: "trail_none",
                ParticleCount: 0,
                ParticleLifetimeSeconds: 0.0f,
                Speed: 0.0f,
                SmokeChance: 0.0f,
                Color: Vector4.Zero)
        };

        var subshellTrailProfiles = new Dictionary<string, TrailProfile>
        {
            [SubShellTrailPresets.FinaleDefault.Id] = SubShellTrailPresets.FinaleDefault,
            [SubShellTrailPresets.SpokeWheel.Id] = SubShellTrailPresets.SpokeWheel
        };

        var shellProfiles = new Dictionary<string, FireworkShellProfile>
        {
            [shellBasicId] = ShellPresets.Create(
                id: shellBasicId,
                burstShape: FireworkBurstShape.Peony,
                colorSchemeId: schemeWarm,
                fuseTimeSeconds: 3.8f,
                explosionRadius: 12.0f,
                particleCount: 5000,
                particleLifetimeSeconds: 2.5f,
                burstSparkleRateHz: 12.0f,
                burstSparkleIntensity: 0.35f,
                burstSpeed: 9.0f),

            [shellChrysId] = ShellPresets.Create(
                id: shellChrysId,
                burstShape: FireworkBurstShape.SparklingChrysanthemum,
                colorSchemeId: schemeGold,
                fuseTimeSeconds: 3.9f,
                explosionRadius: 13.0f,
                particleCount: 0,
                particleLifetimeSeconds: 0.0f,
                burstSparkleRateHz: 0.0f,
                burstSparkleIntensity: 0.0f,
                emission: BurstEmissionSettings.Defaults with
                {
                    ChrysanthemumSpokeCount = 30,
                    ChrysanthemumSpokeJitter = 0.10f,
                },
                sparklingChrysanthemum: new SparklingChrysanthemumParams(
                    SubShellCount: 110,
                    SubShellSpeedMin: 25.0f,
                    SubShellSpeedMax: 35.0f,
                    SubShellLifetimeMinSeconds: 1.2f,
                    SubShellLifetimeMaxSeconds: 2.5f,
                    SubShellGravityScale: 0.42f,
                    SubShellDrag: 0.05f,
                    Trail: new SparklerLineTrailParams(
                        SparkRate: 120.0f,
                        SparkLifetimeSeconds: 0.85f,
                        SparkSpeed: 1.5f,
                        SparkDirectionJitter: 0.32f,
                        BrightnessScalar: 1.05f,
                        MinSpawnPerTick: 32))),

            [shellWillowId] = ShellPresets.Create(
                id: shellWillowId,
                burstShape: FireworkBurstShape.Willow,
                colorSchemeId: schemePastel,
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

            [shellPalmId] = ShellPresets.Create(
                id: shellPalmId,
                burstShape: FireworkBurstShape.Palm,
                colorSchemeId: schemeWarm,
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

            [shellDonutId] = ShellPresets.Create(
                id: shellDonutId,
                burstShape: FireworkBurstShape.Ring,
                colorSchemeId: schemeCool,
                fuseTimeSeconds: 4.1f,
                explosionRadius: 14.0f,
                particleCount: 5000,
                particleLifetimeSeconds: 3.2f,
                burstSparkleRateHz: 5.0f,
                burstSparkleIntensity: 0.65f,
                ringAxis: Vector3.UnitY,
                ringAxisRandomTiltDegrees: 90.0f),

            [shellFishId] = ShellPresets.Create(
                id: shellFishId,
                burstShape: FireworkBurstShape.Fish,
                colorSchemeId: schemeNeon,
                fuseTimeSeconds: 3.9f,
                explosionRadius: 13.0f,
                particleCount: 0,
                particleLifetimeSeconds: 0.0f,
                burstSparkleRateHz: 0.0f,
                burstSparkleIntensity: 0.0f,
                emission: BurstEmissionSettings.Defaults with
                {
                    ChrysanthemumSpokeCount = 36,
                    ChrysanthemumSpokeJitter = 0.12f,
                },
                fish: new FishParams(
                    SubShellCount: 72,
                    SubShellSpeedMin: 18.0f,
                    SubShellSpeedMax: 30.0f,
                    SubShellLifetimeMinSeconds: 1.6f,
                    SubShellLifetimeMaxSeconds: 3.0f,
                    SubShellGravityScale: 0.58f,
                    SubShellDrag: 0.06f,

                    JerkCountMin: 4,
                    JerkCountMax: 9,
                    JerkIntervalMinSeconds: 0.10f,
                    JerkIntervalMaxSeconds: 0.32f,
                    JerkMaxAngleDegrees: 75.0f,
                    SpeedJitter: 0.12f,
                    UpBiasPerJerk: 0.08f,

                    Trail: new SparklerLineTrailParams(
                        SparkRate: 150.0f,
                        SparkLifetimeSeconds: 0.60f,
                        SparkSpeed: 2.4f,
                        SparkDirectionJitter: 0.38f,
                        BrightnessScalar: 1.05f,
                        MinSpawnPerTick: 30))),

            [shellHorsetailGoldId] = ShellPresets.Create(
                id: shellHorsetailGoldId,
                burstShape: FireworkBurstShape.PeonyToWillow,
                colorSchemeId: schemeGold,
                fuseTimeSeconds: 3.2f,             // whatever works with your canister
                explosionRadius: 20.0f,
                particleCount: 220,
                particleLifetimeSeconds: 1.2f,
                burstSparkleRateHz: 3.0f,
                burstSparkleIntensity: 0.18f,
                emission: BurstEmissionSettings.Defaults with
                {
                    WillowDownwardBlend = 0.72f,
                },
                peonyToWillow: PeonyToWillowParams.Defaults with
                {
                    // Simplify: one-to-one handoff driven by subshell profile count for predictable streak counts
                    PeonySparkCount = 1,
                    HandoffDelaySeconds = 0.08f,
                    HandoffFraction = 1.0f,
                    HandoffRandomness = 0.0f,
                    WillowSubshellProfileId = subshellHorsetailCometId,
                    WillowVelocityScale = 0.30f,
                    WillowGravityMultiplier = 2.6f,
                    WillowDragMultiplier = 2.4f,
                    // Keep streaks one-stage: trigger subshell burst immediately instead of a long pre-trail.
                    WillowLifetimeMultiplier = 0.0f,
                    WillowBrightnessBoost = 1.10f,
                    WillowTrailSpawnRate = 0.0f,
                    WillowTrailSpeed = 1.9f
                }),

            [shellDoubleRingId] = ShellPresets.Create(
                id: shellDoubleRingId,
                burstShape: FireworkBurstShape.DoubleRing,
                colorSchemeId: schemeGold,   // nice classy gold rings
                fuseTimeSeconds: 4.1f,
                explosionRadius: 16.0f,
                particleCount: 4500,
                particleLifetimeSeconds: 4.5f,
                burstSparkleRateHz: 2.0f,
                burstSparkleIntensity: 0.65f,
                ringAxis: Vector3.UnitY,
                ringAxisRandomTiltDegrees: 18.0f),

            [shellSpiralId] = ShellPresets.Create(
                id: shellSpiralId,
                burstShape: FireworkBurstShape.Spiral,
                colorSchemeId: schemeNeon,   // loud & colorful
                fuseTimeSeconds: 4.0f,
                explosionRadius: 14.0f,
                particleCount: 5500,
                particleLifetimeSeconds: 4.5f,
                burstSparkleRateHz: 16.0f,
                burstSparkleIntensity: 0.55f,
                ringAxis: Vector3.UnitY,
                ringAxisRandomTiltDegrees: 25.0f),

            [shellSpokeWheelPopId] = ShellPresets.Create(
                id: shellSpokeWheelPopId,
                burstShape: FireworkBurstShape.SubShellSpokeWheelPop,
                colorSchemeId: schemeWarm,
                fuseTimeSeconds: 3.6f,
                explosionRadius: 0.0f,
                particleCount: 0,
                particleLifetimeSeconds: 0.0f,
                burstSparkleRateHz: 0.0f,
                burstSparkleIntensity: 0.0f,
                ringAxis: Vector3.UnitY,
                ringAxisRandomTiltDegrees: 180.0f,
                trailProfile: ShellTrailPresets.ShortBright,
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
                    PopFlashColorSchemeId = schemeWhite,
                    SubShellGravityScale = 0.95f,
                    SubShellDrag = 0.07f,
                    AngleJitterDegrees = 3.5f,
                    TangentialSpeed = 3.0f,
                    RingAxis = Vector3.UnitY,
                    RingAxisRandomTiltDegrees = 180.0f,
                    TrailProfile = SubShellTrailPresets.SpokeWheel
                }),

            [shellWillowTrailOnlyId] = ShellPresets.Create(
                id: shellWillowTrailOnlyId,
                burstShape: FireworkBurstShape.Willow,
                colorSchemeId: schemePastel,
                fuseTimeSeconds: 2.0f,
                explosionRadius: 0.0f,
                particleCount: 0,
                particleLifetimeSeconds: 0.0f,
                suppressBurst: true,
                terminalFadeOutSeconds: 1.5f,
                trailProfile: ShellTrailPresets.WillowLingering),

            // Sub-comet shell for horsetail streaks: slow, gold, lingering trails.
            [shellCometGoldStreakId] = ShellPresets.Create(
                id: shellCometGoldStreakId,
                burstShape: FireworkBurstShape.Comet,
                colorSchemeId: schemeGold,
                fuseTimeSeconds: 1.0f,
                explosionRadius: 0.0f,
                particleCount: 0,
                particleLifetimeSeconds: 0.0f,
                burstSparkleRateHz: 0.0f,
                burstSparkleIntensity: 0.0f,
                trailProfile: ShellTrailPresets.WillowLingering,
                comet: CometParams.Defaults with
                {
                    CometCount = 24,
                    CometSpeedMin = 8.0f,
                    CometSpeedMax = 12.0f,
                    CometUpBias = 0.05f,
                    CometGravityScale = 1.10f,
                    CometDrag = 0.08f,
                    CometLifetimeSeconds = 3.6f,
                    CometLifetimeJitterSeconds = 1.0f,
                    TrailParticleCount = 14,
                    TrailParticleLifetime = 1.1f,
                    TrailSpeed = 3.2f,
                    TrailSmokeChance = 0.22f,
                    TrailColor = null,
                    SubShellProfileId = null,
                    SubShellDelaySeconds = null,
                    SubShellDelayJitterSeconds = 0.0f
                }
            ),

            [shellPeonyToWillowId] = ShellPresets.Create(
                id: shellPeonyToWillowId,
                burstShape: FireworkBurstShape.PeonyToWillow,
                colorSchemeId: schemeGold,
                fuseTimeSeconds: 4.0f,
                explosionRadius: 14.0f,
                particleCount: 100,
                particleLifetimeSeconds: 3.0f,
                burstSparkleRateHz: 10.0f,
                burstSparkleIntensity: 0.40f,
                peonyToWillow: PeonyToWillowParams.Defaults with
                {
                    WillowSubshellProfileId = subshellWillowTrailOnlyId,
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
            [shellFinaleSaluteId] = ShellPresets.Create(
                    id: shellFinaleSaluteId,
                    burstShape: FireworkBurstShape.FinaleSalute,
                    colorSchemeId: schemeDebug, // ignored by PopFlash, but useful for debugging
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
                        TrailProfile = SubShellTrailPresets.FinaleDefault,
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

                [shellCometNeonId] = ShellPresets.Create(
                    id: shellCometNeonId,
                    burstShape: FireworkBurstShape.Comet,
                    colorSchemeId: schemeNeon,
                    fuseTimeSeconds: 3.9f,
                    explosionRadius: 0.0f,
                    particleCount: 0,
                    particleLifetimeSeconds: 0.0f,
                    burstSparkleRateHz: 0.0f,
                    burstSparkleIntensity: 0.0f,
                    trailProfile: ShellTrailPresets.CometNeon,
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
            SubShellDelayJitterSeconds = 0.6f,
                            }
                ),

            [shellCometCrackleId] = ShellPresets.Create(
                id: shellCometCrackleId,
                burstShape: FireworkBurstShape.Comet,
                colorSchemeId: schemeMixed,
                fuseTimeSeconds: 4.0f,
                explosionRadius: 0.0f,
                particleCount: 0,
                particleLifetimeSeconds: 0.0f,
                burstSparkleRateHz: 0.0f,
                burstSparkleIntensity: 0.0f,
                trailProfile: ShellTrailPresets.CometNeon,
                comet: CometParams.Defaults with
                {
                    CometCount = 30,
                    CometSpeedMin = 14f,
                    CometSpeedMax = 26f,
                    CometUpBias = 0.30f,
                    CometGravityScale = 0.85f,
                    CometDrag = 0.05f,
                    CometLifetimeSeconds = 4.2f,
                    TrailParticleCount = 11,
                    TrailParticleLifetime = 0.55f,
                    TrailSpeed = 4.5f,
                    TrailSmokeChance = 0.20f,
                    TrailColor = null,
                    SubShellProfileId = subshellCracklePeonyId,
                    SubShellDelaySeconds = 2.4f,
                    SubShellDelayJitterSeconds = 1.2f
                }
            ),

            [shellCracklePeonyId] = ShellPresets.Create(
                id: shellCracklePeonyId,
                burstShape: FireworkBurstShape.CrackleStar,
                colorSchemeId: schemeMixed,
                fuseTimeSeconds: 3.8f,
                explosionRadius: 9.0f,
                particleCount: 2500,
                particleLifetimeSeconds: 2.5f,
                burstSparkleRateHz: 4.0f,
                burstSparkleIntensity: 0.10f,
                crackleStar: new CrackleStarProfile(
                    CrackleStarProbability: 0.55f,
                    ClusterCountMin: 12,
                    ClusterCountMax: 24,
                    ClusterConeAngleDegrees: 14.0f,
                    MicroSpeedMulMin: 0.60f,
                    MicroSpeedMulMax: 1.10f,
                    MicroLifetimeMinSeconds: 0.05f,
                    MicroLifetimeMaxSeconds: 0.14f,
                    ClusterStaggerMaxSeconds: 0.32f,
                    NormalSparkMixProbability: 0.05f)),

            [shellStrobeFlashId] = ShellPresets.Create(
                id: shellStrobeFlashId,
                burstShape: FireworkBurstShape.Peony,
                colorSchemeId: schemeWhite,
                fuseTimeSeconds: 0.50f,
                explosionRadius: 0.025f,
                particleCount: 100,
                particleLifetimeSeconds: 0.25f,
                burstSparkleRateHz: 0.0f,
                burstSparkleIntensity: 0.0f,
                trailProfile: trailProfiles["trail_none"],
                emission: BurstEmissionSettings.Defaults),

            [shellStrobeId] = ShellPresets.Create(
                id: shellStrobeId,
                burstShape: FireworkBurstShape.Peony,
                colorSchemeId: schemeWhite,
                fuseTimeSeconds: 3.7f,
                explosionRadius: 12.0f,
                particleCount: 5200,
                particleLifetimeSeconds: 1.4f,
                burstSparkleRateHz: 10.0f,
                burstSparkleIntensity: 0.45f,
                emission: BurstEmissionSettings.Defaults,
                strobe: new StrobeParams(
                    SubShellProfileId: subshellStrobeId,
                    StrobeCount: 80,
                    StrobeColor: Colors.White,
                    StrobeRadiusMeters: 0.025f,
                    StrobeLifetimeSeconds: 0.25f,
                    SpreadRadiusFraction: 0.75f,
                    SpawnMode: StrobeSpawnMode.Jittered,
                    SpawnJitterSeconds: 0.75f))
        };

        // SubShell profiles: reusable child shell behaviors
        var subshellProfiles = new Dictionary<string, SubShellProfile>
        {
            [subshellBasicPopId] = SubShellPresets.Sphere(
                id: subshellBasicPopId,
                shellProfileId: shellBasicId,
                count: 12,
                minAltitudeToSpawn: 5.0f),

            [subshellWillowTrailOnlyId] = SubShellPresets.Sphere(
                id: subshellWillowTrailOnlyId,
                shellProfileId: shellWillowTrailOnlyId,
                count: 12,
                minAltitudeToSpawn: 5.0f),

            [subshellRingSparkleId] = SubShellPresets.Ring(
                id: subshellRingSparkleId,
                shellProfileId: shellDonutId,
                count: 24,
                minAltitudeToSpawn: 8.0f,
                colorSchemeId: schemeNeon),

            [subshellCracklePeonyId] = SubShellPresets.Sphere(
                id: subshellCracklePeonyId,
                shellProfileId: shellCracklePeonyId,
                count: 10,
                minAltitudeToSpawn: 6.0f,
                delaySeconds: 0.25f,
                inheritParentVelocity: 0.15f,
                addedSpeed: 10.0f,
                directionJitter: 0.10f,
                speedJitter: 0.22f,
                positionJitter: 0.5f,
                childTimeScale: 0.9f,
                maxSubshellDepth: 1),

            [subshellHorsetailCometId] = SubShellPresets.Sphere(
                id: subshellHorsetailCometId,
                shellProfileId: shellCometGoldStreakId,
                count: 2,
                minAltitudeToSpawn: 5.0f,
                delaySeconds: 0.15f,
                inheritParentVelocity: 0.12f,
                addedSpeed: 8.0f,
                directionJitter: 0.10f,
                speedJitter: 0.18f,
                positionJitter: 0.45f,
                childTimeScale: 1.0f,
                maxSubshellDepth: 1),

            [subshellStrobeId] = SubShellPresets.Sphere(
                id: subshellStrobeId,
                shellProfileId: shellStrobeFlashId,
                count: 100,
                minAltitudeToSpawn: 5.0f,
                delaySeconds: 0.0f,
                inheritParentVelocity: 0.05f,
                addedSpeed: 6.0f,
                directionJitter: 0.15f,
                speedJitter: 0.20f,
                positionJitter: 0.35f,
                childTimeScale: 1.0f,
                colorSchemeId: schemeWhite,
                burstShapeOverride: FireworkBurstShape.Peony,
                maxSubshellDepth: 1)
        };

        var groundEffectProfiles = new Dictionary<string, GroundEffectProfile>
        {
            [groundFountainWarmId] = GroundEffectPresets.Fountain(
                id: groundFountainWarmId,
                colorSchemeId: schemeWarm,
                durationSeconds: 5.0f,
                emissionRate: 1800.0f,
                particleVelocityRange: new Vector2(6.0f, 12.0f),
                particleLifetimeSeconds: 1.6f,
                gravityFactor: 0.55f,
                brightnessScalar: 1.2f,
                coneAngleDegrees: 40.0f,
                flickerIntensity: 0.10f,
                smokeAmount: 0.2f),

            [groundSpinnerNeonId] = GroundEffectPresets.Spinner(
                id: groundSpinnerNeonId,
                colorSchemeId: schemeNeon,
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


            [groundSpinnerNeonVId] = GroundEffectPresets.Spinner(
                id: groundSpinnerNeonVId,
                colorSchemeId: schemeNeon,
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

            [groundMineMixedId] = new GroundEffectProfile(
                Id: groundMineMixedId,
                Type: GroundEffectType.Mine,
                ColorSchemeId: schemeMixed,
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

            [groundBengalWarmId] = new GroundEffectProfile(
                Id: groundBengalWarmId,
                Type: GroundEffectType.BengalFlare,
                ColorSchemeId: schemeWarm,
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

            [groundLanceHeartId] = new GroundEffectProfile(
                Id: groundLanceHeartId,
                Type: GroundEffectType.LanceworkPanel,
                ColorSchemeId: schemeNeon,
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

            [groundWaterfallGoldId] = new GroundEffectProfile(
                Id: groundWaterfallGoldId,
                Type: GroundEffectType.WaterfallCurtain,
                ColorSchemeId: schemeGold,
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

            [groundChaserZipperId] = new GroundEffectProfile(
                Id: groundChaserZipperId,
                Type: GroundEffectType.ChaserLine,
                ColorSchemeId: schemeNeon,
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

            [groundBloomBrocadeId] = new GroundEffectProfile(
                Id: groundBloomBrocadeId,
                Type: GroundEffectType.GroundBloom,
                ColorSchemeId: schemeBrocadeGold,
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

            [groundGlitterPulseId] = new GroundEffectProfile(
                Id: groundGlitterPulseId,
                Type: GroundEffectType.PulsingGlitterFountain,
                ColorSchemeId: schemeCool,
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

        var profileSet = new FireworksProfileSet(canisterProfiles, shellProfiles, groundEffectProfiles, colorSchemeProfiles, subshellProfiles, trailProfiles, subshellTrailProfiles);
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
        var random = new Random();



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

        // Ordered list of shell IDs for the main show. Adjust this list to change launch order.
        var mainShowShellIds = new[]
        {
           // shellCracklePeonyId,
            //shellBasicId,
            shellStrobeId,
            shellChrysId,
            shellWillowId,
            shellFishId,
            shellDonutId,
            shellSpokeWheelPopId,
            shellHorsetailGoldId,
            shellCometCrackleId,
            shellDoubleRingId,
            shellPeonyToWillowId,
            shellSpiralId,
            shellCometNeonId
        };
        int mainCanisters = 25;
        for (int i = 0; i < 50; i += gridSize)
        {
            for (int j = 0; j < gridSize; j++)
            {
                string shellId = mainShowShellIds[(i + j) % mainShowShellIds.Length];

                string canisterId = profiles.Canisters.Keys.ElementAt((i + j) % mainCanisters);
                string colorSchemeId;
                if (shellId != DefaultIds.shellChrysId)
                {
                    colorSchemeId = profiles.ColorSchemes.Keys.ElementAt((i + j) % profiles.ColorSchemes.Count);
                }
                else
                {
                    // Chrysanthemum shells look best in gold colors.
                    colorSchemeId = DefaultIds.schemeGold;
                }

                // debug variations
                //shellId = shellFishId;
                shellId = (j % 2 == 0)? shellStrobeId: shellWillowId;
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

        // Finale - Part 1: all canisters fire basic shells in sequence, three times.

        for (int j = 0; j < 3; j++)
        {
            for (int i = 0; i < mainCanisters; i++)
            {
                string canisterId = profiles.Canisters.Keys.ElementAt(i % mainCanisters);
                float jitter = (float)(random.NextDouble() * 0.1);
                var finaleEvent = new ShowEvent(
                    TimeSeconds: t + jitter,
                    CanisterId: canisterId,
                    ShellProfileId: DefaultIds.shellBasicId);
                events.Add(finaleEvent);
                //t += 1.0f;
            }
            t += 8f;
        }

        // Finale - Part 2: alternating comet neon and peony-to-willow shells.

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

        // Finale - Part 3: rapid-fire finale salute shells from all canisters.

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
