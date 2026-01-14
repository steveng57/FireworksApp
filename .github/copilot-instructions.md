# Copilot Instructions — FireworksApp

## General Guidelines
- Never use terminal/PowerShell for edits; always use Visual Studio tooling instead.  If Visual Studio tooling does not work, wait a bit and try again.  If that still does not work, ask for humand assistance.

- You are working in an existing C#/.NET desktop app that renders a real-time fireworks 
simulation using:
- WPF UI + a Direct3D 11 renderer (HwndHost / D3D11 pipeline)
- GPU particle buffers (StructuredBuffer / dynamic buffers), and HLSL shaders
- A simulation engine that drives shells, bursts, subshells, and show scripting

## 0) Golden rules (must follow)
1. **Do not refactor broadly unless explicitly asked.** Prefer minimal, localized edits.
2. **Do not invent new architecture.** Extend existing patterns and types.
3. **No silent behavior changes.** If you change timing, units, randomization, or lifetimes, call it out in comments.
4. **Performance is a feature.** Avoid per-frame allocations, LINQ, boxing, unnecessary copies.
5. **Never swallow exceptions.** If you touch try/catch, preserve stack traces and log with context.
6. **Always test your changes.** Ensure the app builds.

## 1) Change strategy
- Make changes in small steps; each step should compile.
- Prefer “additive” changes (new methods/types) over changing call signatures everywhere.
- If a change touches both CPU + GPU (C# + HLSL), keep names consistent and document data layout assumptions.
- When you’re unsure, add a TODO + a small diagnostic log rather than guessing.

## 2) Repository awareness
Before coding:
- Search for existing similar effects or profiles (e.g., ring burst, subshell, pop flash, willow, palm).
- Reuse existing profile + registry patterns (ProfileSet / dictionaries by string ID).
- Reuse existing random helpers / math helpers.

When adding a feature:
- Add it as a new `FireworksBurstShape` (or the existing enum/strategy mechanism).
- Add a corresponding profile entry in defaults (Defaults.cs / DefaultProfiles).
- Add a minimal UI hook only if requested (toolbar / debug toggles).

## 3) Simulation + timing rules
- All time values are in **seconds** unless the code clearly indicates otherwise.
- Simulation uses `DeltaTime` scaled by `TimeScale` (global). Do not introduce a second global time scale unless asked.
- If adding “visual-only” timing (sparkle/flicker), ensure it **does not** affect physics (velocity/gravity).
- Lifetimes: allow per-particle variance (randomized within configured min/max), avoid synchronized “wink out”.

## 4) Particle system rules (critical for perf + correctness)
- Avoid per-particle objects. Use structs / arrays / buffers.
- Do not allocate lists each frame. If you need lists, reuse/clear, or use pooled arrays.
- Prefer “alive list” patterns already used in the project:
  - Update only alive particles.
  - Maintain capacities per particle kind if the app already does this.
- GPU updates:
  - Minimize Map/Unmap calls and avoid CPU->GPU uploads larger than necessary.
  - When using `WriteDiscard`, batch writes and avoid multiple maps per frame if possible.
  - Keep constant buffers tightly packed; update once per frame.

## 5) Direct3D11 / HLSL rules
- Keep shader entry points, profiles, and compile order consistent with the project’s build scripts.
- Keep struct layouts consistent between C# and HLSL (packing, alignment).
- If you add new fields to a GPU struct:
  - Update both HLSL and C# definition.
  - Update any stride calculations.
  - Add a comment describing the layout and any alignment assumptions.

## 6) Logging + diagnostics (useful but controlled)
- Prefer lightweight counters or conditional debug logging.
- Any new verbose logging must be behind a debug flag or conditional compilation.
- If you add instrumentation, ensure it doesn’t allocate strings per frame.

## 7) Exception handling (explicit guidance)
- Do not add broad `try { ... } catch { }`.
- Catch only specific exceptions when you can handle them.
- When rethrowing, use `throw;` not `throw ex;`.
- If the codebase has a centralized logging/telemetry mechanism, use it; otherwise use `Debug.WriteLine` in DEBUG only.

## 8) Style + conventions
- Match existing naming and file structure.
- Prefer `readonly` where applicable.
- Avoid new dependencies unless asked.
- Avoid LINQ in hot paths; use for setup only.

## 9) Deliverable format
When producing code:
- Provide the **exact files and regions** to change.
- If you modify an existing file, include enough surrounding context to patch safely.
- If you add a new type, show its namespace and where it’s referenced.
- End with a short checklist:
  - Build succeeded
