# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
