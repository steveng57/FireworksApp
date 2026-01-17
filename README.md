# FireworksApp

WPF desktop app (targeting .NET 10 / C# 14) that simulates and renders fireworks in real time using a Direct3D 11 renderer, GPU particle buffers, and HLSL shaders.

---

## Feature highlights

- D3D11 renderer hosted in WPF (HwndHost) with orbit/pan/zoom camera controls
- GPU particle system updated by compute shader for shells, sparks, smoke, and trails
- Rich burst shapes (peony, chrysanthemum, willow, palm, ring, spiral, double ring, horsetail, fish, spoke wheel, peony-to-willow, finale salute, comet)
- Data-driven profiles for canisters, shells, color schemes, subshells, trails, and ground effects
- Scripted shows (timeline of launches and ground effects) generated in code and ready for future file-based loading

---

## Requirements

- Windows with a Direct3D 11-capable GPU/driver
- Visual Studio 2022 or newer with the **.NET desktop development** workload

---

## Build and run

1. Open the solution in Visual Studio.
2. Restore NuGet packages if prompted.
3. Build the `FireworksApp` project.
4. Set `FireworksApp` as the startup project (if not already) and press **F5**.

---

## Controls

Mouse-driven camera in `Rendering/D3D11Renderer.cs`:

- Drag with the mouse to orbit (yaw/pitch)
- Mouse wheel to zoom
- Pan moves the camera target (see `PanCamera` implementation)

---

## Simulation data

Profiles and the default show are defined in `Simulation/Defaults.cs` via `DefaultProfiles.Create()` and `DefaultShow.Create()`.

### Canisters

- IDs: `c01`-`c25` (shell canisters) and `g01`-`g08` (ground effects)
- Each canister stores an ID, canister type label (for muzzle velocity/height), pad position (`Vector2`), launch direction (`Vector3`), and a default shell ID.
- `canisterSpacingScale` controls the grid spacing of the main pad layout.

### Shell profiles (examples)

- `basic` (peony), `chrys` (sparkling chrysanthemum), `willow`, `palm`, `donut` (ring), `fish`, `horsetail_gold`, `double_ring`, `spiral`, `spoke_wheel_pop`, `peony_to_willow`, `finale_salute`, `comet_neon`
- Each profile sets burst shape, fuse time, explosion radius, particle counts/lifetimes, sparkle rates, and any shape-specific parameters (for example ring axis tilt or subshell settings).

### Color schemes (examples)

- `warm`, `cool`, `mixed`, `neon`, `pastel`, `white`, `gold`, `brocadegold`, `debug`
- Each scheme defines a palette, variation, and brightness boost. IDs are case-sensitive.

### Ground effects (examples)

- `fountain_warm`, `spinner_neon`, `spinner_neon_v`, `mine_mixed`, `bengal_warm`, `lance_heart`, `waterfall_gold`, `chaser_zipper`, `bloom_brocade`, `glitter_pulse`

### Subshells and trails

- Subshell presets (for example `subshell_basic_pop`, `subshell_willow_trail_only`, `subshell_ring_sparkle`) and trail presets (shell and subshell) are registered alongside shell profiles.

---

## Show scripts

`DefaultShow.Create()` builds a looping demo timeline:

- Main show: iterates through canisters in a grid, cycling shell IDs and color schemes over time (`t`), spreading launches with small offsets.
- Finale part 1: three passes of basic shells from all canisters.
- Finale part 2: alternating comet neon and peony-to-willow launches.
- Finale part 3: rapid-fire finale salute shells from all canisters.

Show events can launch shells (`ShellProfileId`) or trigger ground effects (`GroundEffectProfileId`) at `TimeSeconds`. IDs are validated against the profile set.

---

## Rendering and shaders

- Renderer: `Rendering/D3D11Renderer*.cs` sets up the device, swap chain, camera, constant buffers, and particle buffers.
- Shaders: `Shaders/Pad.hlsl`, `Shaders/Ground.hlsl`, `Shaders/Canister.hlsl`, `Shaders/Shell.hlsl`, `Shaders/Particles.hlsl` (compute + draw pipeline for particles).
- Keep struct layouts in sync between C# and HLSL when adjusting shader inputs or buffer formats.

---

## Extending the project

### Add a shell

1. Add a new `FireworkShellProfile` entry in `DefaultProfiles.Create()` with a unique `Id` and supported `BurstShape`.
2. Reference it from a canister default shell ID or a `ShowEvent.ShellProfileId`.
3. Ensure the color scheme and any subshell/trail profiles referenced exist.

### Add a color scheme

1. Add an entry to the color scheme dictionary in `DefaultProfiles.Create()`.
2. Use the new `ColorSchemeId` in shells or show events.

### Add a ground effect

1. Register a `GroundEffectProfile` (see `groundEffectProfiles` in `DefaultProfiles.Create()`).
2. Trigger it via `ShowEvent` by setting `GroundEffectProfileId` and using a ground canister ID (`g01`-`g08`).

### Load shows from file

- Define a serializable `ShowEvent` format, parse into a `ShowScript`, and validate IDs against `FireworksProfileSet` for early error reporting.

---

## Troubleshooting

- **Blank render:** verify D3D11 support, shader files are copied to output, and check the Visual Studio Output window for D3D debug messages.
- **Unexpected colors:** ensure `ColorSchemeId` matches a registered scheme and variation/boost values are reasonable.
- **Startup/device failures:** update GPU drivers or switch GPUs (integrated vs discrete) if the device creation fails.
- **Performance:** lower particle counts, reduce trail rates, or simplify burst shapes in profiles when tuning performance.

---

## License

See [LICENSE](LICENSE.md) for the project license (MIT).
