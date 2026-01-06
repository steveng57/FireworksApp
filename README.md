# FireworksApp

A WPF (.NET 10) desktop application that renders and simulates fireworks in real time using a custom Direct3D 11 renderer and GPU shaders.

This project combines:
- A **simulation layer** (profiles, show scripts, shell/ground effect logic)
- A **rendering layer** (D3D11 interop + GPU particle system)
- **HLSL shaders** for the pad/ground/canisters/shells and GPU particles

> Notes
> - This README describes the repository as it exists in your workspace. File names referenced below (for example `Simulation/FireworksEngine.cs`, `Rendering/D3D11Renderer.cs`, `Shaders/*.hlsl`) are present in the current solution.

---

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Repository Layout](#repository-layout)
- [Build & Run](#build--run)
- [Controls](#controls)
- [How the Simulation Works (High-Level)](#how-the-simulation-works-high-level)
- [Profiles: Canisters, Shells, Color Schemes](#profiles-canisters-shells-color-schemes)
  - [Canisters](#canisters)
  - [Shells](#shells)
  - [Color Schemes](#color-schemes)
- [Show Scripts](#show-scripts)
  - [Default Show Generator](#default-show-generator)
  - [Adding a New Show Event](#adding-a-new-show-event)
- [Rendering Pipeline (Direct3D 11)](#rendering-pipeline-direct3d-11)
- [Shaders](#shaders)
- [GPU Particle System](#gpu-particle-system)
- [Extending the Project](#extending-the-project)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## Features

- Real-time fireworks simulation with configurable shell profiles
- Multiple **burst shapes** (for example peony, chrysanthemum, willow, ring)
- Color schemes with variation and brightness boosting
- Scripted show playback (timeline of launch events)
- GPU-accelerated rendering and effects via HLSL (including a compute-shader particle update)
- WPF desktop UI hosting a D3D11 surface
- Orbit/pan/zoom camera controls
- Ground emission effects (show events can trigger ground effects, not just shell launches)

---

## Tech Stack

- **Language:** C# (project set for C# 14)
- **Application framework:** WPF
- **Target runtime:** .NET 10
- **Graphics:** Direct3D 11
- **Shaders:** HLSL (`Shaders/*.hlsl`)

---

## Repository Layout

Typical top-level structure (may vary slightly):

- `Simulation/`
  - `Defaults.cs` – default profile sets + generated demo show
  - `DefaultCanisterTypes.cs` – canister type definitions (burst height, muzzle velocity range, etc.)
  - `FireworksEngine.cs` – core simulation engine (show playback, shells, ground effects)
  - `GroundEmissionStyles.cs` – ground-effect emission patterns
  - `Profiles.cs` – profile types (`CanisterProfile`, `FireworkShellProfile`, `ColorScheme`, `FireworksProfileSet`, ground-effect profiles)
  - `ShowScript.cs` – show timeline types (`ShowScript`, `ShowEvent`)
- `Rendering/`
  - `D3D11Renderer.cs` – D3D11 setup, camera, draw loop, and GPU particle system
- `Shaders/`
  - `Pad.hlsl` – launch pad geometry shader
  - `Ground.hlsl` – ground plane shader
  - `Canister.hlsl` – canister shader
  - `Shell.hlsl` – bright unlit shell shader (debug/visibility)
  - `Particles.hlsl` – compute + draw shaders for the GPU particle system
- `D3DHost.cs` – WPF/Direct3D host integration
- `MainWindow.xaml.cs` – input wiring / app UI host
- `FireworksApp.csproj` – project definition

---

## Build & Run

### Prerequisites

- Visual Studio 2022 (or newer) with:
  - **.NET desktop development** workload
  - Windows SDK components
- Windows machine with a GPU/driver capable of Direct3D 11

### Build

1. Open the solution folder in Visual Studio.
2. Restore NuGet packages (Visual Studio usually does this automatically).
3. Build the `FireworksApp` project.

### Run

- Set `FireworksApp` as Startup Project (if needed)
- Press **F5** to run

---

## Controls

Camera controls are handled in `Rendering/D3D11Renderer.cs` and surfaced via WPF input:

- Mouse drag: orbit (yaw/pitch)
- Mouse wheel: zoom in/out
- Pan: move the camera target (implementation in `D3D11Renderer.PanCamera`)

---

## How the Simulation Works (High-Level)

At a high level, the app runs two tightly coupled loops:

1. **Simulation update** (CPU)
   - Advances show time (with a global `TimeScale`)
   - Processes show events (shell launches and/or ground effects)
   - Updates active shells (including drag and fuse timing)
   - Tells the renderer when to spawn bursts/trails/ground particles
2. **Render update** (GPU)
   - Updates GPU particles via compute shader
   - Renders ground/pad/canisters/shell markers
   - Renders particles in multiple passes (additive + alpha)

The simulation is data-driven via *profiles* and a *show script*:

- `FireworkShellProfile` defines how a shell behaves when launched and when it bursts.
- `CanisterProfile` defines where a shell launches from and the initial direction.
- `ColorScheme` defines base colors and how much random variation is applied.
- `ShowScript` is a timeline of `ShowEvent` objects describing launches at a given time.

`ShowEvent` can also schedule **ground effects** (for example fountains/sparkle sources) via a ground-effect profile ID.

---

## Profiles: Canisters, Shells, Color Schemes

Profiles are created by `DefaultProfiles.Create()` in `Simulation/Defaults.cs`.

### Canisters

A **canister** defines a launch point and initial aim direction, and references a default shell profile.

You’ll see a grid of canisters created like:

- A string ID (`"c01"`, `"c02"`, ...)
- A label (for example `"M2"`, `"M3"`)
- A 2D position (`Vector2`) laid out in a grid
- A 3D unit direction vector (`Vector3.Normalize(...)`) pointing upward at different angles
- A default shell ID (for example `"basic"`, `"donut"`, `"chrys"`, `"willow"`)

The constant `canisterSpacingScale` affects the spacing of positions.

### Shells

A **shell profile** defines the burst behavior and particle properties.

Common fields include:

- `Id`: profile ID referenced by show events/canisters
- `BurstShape`: enum value such as `Peony`, `Chrysanthemum`, `Willow`, `Palm`, `Ring`, etc.
- `ColorSchemeId`: resolves to a `ColorScheme`
- `FuseTimeSeconds`: time from launch to burst
- `ExplosionRadius`: overall size of the burst
- `ParticleCount`: number of particles emitted at burst
- `ParticleLifetimeSeconds`: how long particles remain visible

Some shapes have extra parameters, for example ring shells:

- `RingAxis`: base axis for the ring
- `RingAxisRandomTiltDegrees`: random tilt to vary orientation per shell

### Color Schemes

A **color scheme** is defined by:

- `Id`
- `Colors[]`: base palette
- `Variation`: per-particle random variation
- `Boost`: brightness/intensity multiplier

Example schemes:

- `warm`, `cool`, `mixed` for typical fireworks palettes
- `neon`, `pastel` for stylized looks
- `debug` for visually distinct diagnostic colors

> Important
> Color scheme IDs are case-sensitive. Ensure `ColorSchemeId` in a `FireworkShellProfile` matches an existing scheme key.

---

## Show Scripts

A show script is a list of timestamped launch events.

### Default Show Generator

`DefaultShow.Create()` in `Simulation/Defaults.cs` generates a simple looping timeline (placeholder for JSON/YAML loading):

- Creates a `List<ShowEvent>`
- Uses `DefaultProfiles.Create()`
- Iterates in a grid-like pattern to launch different canister/shell/color combinations
- Increments the timeline (`t`) to spread launches over time

Each `ShowEvent` contains:

- `TimeSeconds`: when to launch
- `CanisterId`: which canister launches
- `ShellProfileId`: which shell profile is launched
- `ColorSchemeId`: optional override for colors
- `MuzzleVelocity`: optional override for velocity

Additionally, a show event may specify:

- `GroundEffectProfileId`: if set, the event starts a ground effect instead of launching a shell

### Adding a New Show Event

To add a new event (either in the default generator or in a different show loader), create a `ShowEvent`:

- Choose a `TimeSeconds` (float)
- Choose a valid `CanisterId` from `profiles.Canisters`
- Choose a valid `ShellProfileId` from `profiles.Shells`
- Choose a valid `ColorSchemeId` from `profiles.ColorSchemes` (or set null/empty if supported by your model)

Then append it to the events list.

---

## Rendering Pipeline (Direct3D 11)

The rendering layer lives under `Rendering/` and is hosted in WPF via `D3DHost.cs`.

Typical responsibilities include:

- Creating D3D11 device/swap chain (or shared surface) compatible with WPF hosting
- Managing render targets and depth buffers
- Uploading simulation outputs (shell positions, canister positions)
- Maintaining a GPU particle buffer updated via compute shader
- Invoking shader passes and draw calls

If you’re modifying the renderer:

- Keep buffer updates efficient (batch uploads, reuse buffers)
- Be careful about WPF/D3D interop constraints (threading and device lifetime)

---

## Shaders

HLSL shaders are in `Shaders/`:

- `Shaders/Pad.hlsl` – launch pad shading
- `Shaders/Ground.hlsl` – ground shading
- `Shaders/Canister.hlsl` – canister shading
- `Shaders/Shell.hlsl` – shell marker shading
- `Shaders/Particles.hlsl` – GPU particle compute + render

When changing shaders:

- Ensure the build action / content pipeline is configured to copy shaders to output (or embed them), depending on how `D3D11Renderer` loads them.
- Keep shader input layouts and constant buffer structures synchronized with your C# side structs.

---

## GPU Particle System

The renderer uses a GPU particle system backed by a structured buffer:

- Update: `Particles.hlsl` compute shader (`CSUpdate`)
- Render: billboards expanded in the vertex shader (`VSParticle`) and shaded in the pixel shader

Particle “kinds” include shells, burst sparks, smoke, and crackle-style sparks. Rendering is split into passes so additive particles and alpha-blended particles (like smoke) can be handled separately.

---

## Extending the Project

### Add a New Shell Type

1. Add a new `FireworkShellProfile` in `DefaultProfiles.Create()`.
2. Ensure:
   - `Id` is unique
   - `BurstShape` is implemented by the simulation/rendering logic
   - `ColorSchemeId` exists
3. Reference the new shell from either:
   - a canister default shell ID, or
   - a show event’s `ShellProfileId`

### Add a New Color Scheme

1. Add a new entry to the `schemes` dictionary in `DefaultProfiles.Create()`.
2. Use it by setting `ColorSchemeId` in a shell profile or show event.

### Load Shows from File (JSON/YAML)

The default show generator is explicitly a placeholder; a next step is:

- Define a serializable `ShowEvent` representation
- Parse a file into a `ShowScript`
- Validate IDs against `FireworksProfileSet`

Keep validation strict so invalid IDs fail fast with clear errors.

---

## Troubleshooting

### Black/blank render area

- Confirm your GPU supports Direct3D 11.
- Verify shaders are being found/loaded from the output directory.
- Check Visual Studio Output window for D3D debug layer messages (if enabled).

### Missing colors / unexpected palettes

- Confirm `ColorSchemeId` matches a key in the `ColorSchemes` dictionary.
- Confirm variation/boost values aren’t producing over/under-saturated results.

### Crashes on startup

- Startup device creation failures can happen due to driver issues.
- Try running on a different GPU (laptop integrated vs discrete) or updating drivers.

---

## License

This repository’s license is defined by the [MIT LICENSE](/LICENSE.md) file.
