# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-06-30

Reworked the package to be **Unity-native** (the 0.1.0 engine-agnostic / runtime-YAML approach was
the wrong target). Minimum Unity version is now **6000.3 (Unity 6.3)**.

### Changed

- **Definitions are now ScriptableObjects** (`AttributeSetDefinition`, `GameplayEffectDefinition`,
  `GameplayAbilityDefinition`, `GameplayTagRegistry`, `GameplayControllerConfig`) — Unity serializes
  them to `.asset` with inspectors. The runtime data model and the runtime YAML parser were removed.
- **Spec packs are imported, not parsed at runtime.** A new editor assembly `Jbltx.Ugas.Editor`
  provides a `ScriptedImporter` (`.ugasentity`) and an "Import Spec Pack…" menu that convert spec
  `entities/*.yaml` into the SO definitions. The validated YAML reader and the spec→SO mapping were
  re-homed into this editor assembly; **the runtime never parses YAML**.
- **Runtime uses engine idioms.** New `UgasController : MonoBehaviour` owns runtime attribute/effect/
  ability instances and ticks them from `Update`. Gameplay tags are interned to `int` handles;
  modifier aggregation is struct-based and allocation-free; active-effect records are pooled.
- The §5 aggregation math was factored into a shared, Burst-compatible **kernel** (`AttributeKernel`)
  re-homed from the validated 0.1.0 logic (still yields the worked `WeaponDamage == 18.0`).
- Tests moved to the **Unity Test Framework** (EditMode + PlayMode); CI now uses
  `game-ci/unity-test-runner` (Unity 6000.3.0f1) plus a license-free structure-validation job. The
  headless `dotnet` build/test path was removed.
- `package.json`: `unity` → `6000.3` (+ `unityRelease`); removed the Newtonsoft hard dependency.

### Added

- **DOTS/Burst-accelerated backend** as an optional **soft dependency** (`Jbltx.Ugas.Dots`): ECS
  components + a Burst `ISystem`/`IJobEntity` that call the same shared kernel. The assembly is gated
  by `versionDefines` (`com.unity.entities` → `UGAS_DOTS`, `com.unity.burst` → `UGAS_BURST`) and
  `defineConstraints: ["UGAS_DOTS"]`, so the package compiles and runs with Entities/Burst absent.
  `package.json` lists neither as a dependency; `UgasBackend.Active` reports the path in effect.

## [0.1.0] - 2026-06-30

Initial scaffold of the UGAS Unity reference implementation (UPM package
`com.jbltx.ugas`). Tracks the umbrella issue [jbltx/ugas#27](https://github.com/jbltx/ugas/issues/27).

### Added

- UPM package layout: `package.json`, `Runtime/` and `Tests/` assembly definitions, CI.
- **Schema-loading layer** (`Runtime/Schema/`) — C# data types mirroring the six
  engine-agnostic core schemas (`attribute`, `attribute_set`, `gameplay_effect`,
  `gameplay_ability`, `gameplay_tag`, `gameplay_controller`) plus a loader that
  reads the genre-pack `entities/*.yaml` files. Fully working.
- **Attributes pillar** — dual-value Base/Current model with the full ordered
  modifier-aggregation pipeline (Add -> per-channel Multiply -> AddPost -> Override)
  and clamping. Fully implemented.
- **Gameplay Tags pillar** — hierarchical dot-notation tag container with
  `HasTag` / `HasTagExact` / `HasAny` / `HasAll` query API. Fully implemented.
- **Gameplay Effects pillar** — Instant / HasDuration / Infinite model and the
  RunInParallel / RunInSequence / RunInMerge execution policies. Interfaces with
  minimal Instant application; durational scheduling stubbed.
- **Gameplay Abilities pillar** — lifecycle state machine interface with a
  minimal default implementation; ability tasks stubbed.
- **Gameplay Controller** — authoritative container wiring the pillars together,
  with state-persistence (save/restore) scaffolding per SPEC §14.
- **Conformance test scaffold** (`Tests/`) — NUnit fixtures that load the spec
  case-study / genre entities and assert basic behaviour. Stubbed pillars use
  `[Ignore]` placeholders so the harness compiles.

### Not yet implemented

- Networking & client prediction — explicit non-goal pending spec issues
  [jbltx/ugas#4](https://github.com/jbltx/ugas/issues/4),
  [#5](https://github.com/jbltx/ugas/issues/5),
  [#7](https://github.com/jbltx/ugas/issues/7). Marked with `// TODO` in code.
- Editor authoring tooling / custom inspectors.

[Unreleased]: https://github.com/jbltx/ugas-unity/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/jbltx/ugas-unity/releases/tag/v0.1.0
