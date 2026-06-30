# UGAS for Unity

**Unity reference implementation of [UGAS](https://github.com/jbltx/ugas)** — the Universal
Gameplay Ability System specification — packaged as a Unity Package Manager (UPM) package
(`com.jbltx.ugas`).

This package implements the four UGAS pillars plus a Gameplay Controller, and consumes the
**engine-agnostic UGAS schemas directly** — it does not redefine the data model. It tracks the
umbrella issue [jbltx/ugas#27](https://github.com/jbltx/ugas/issues/27).

> **Status: early scaffold (0.1.0).** The schema-loading layer, the Attributes pillar, the Gameplay
> Tags pillar, and state persistence are fully implemented and covered by conformance tests. The
> Effects and Abilities pillars implement their core paths; some advanced behaviour is stubbed with
> clear `TODO`s (see [Implementation status](#implementation-status)). Networking is an explicit
> non-goal for now.

## Installation

Add the package to your project via the Unity Package Manager using the Git URL:

```
https://github.com/jbltx/ugas-unity.git
```

Or add it to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.jbltx.ugas": "https://github.com/jbltx/ugas-unity.git",
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }
}
```

**Requirements:** Unity 2021.3 or newer. The package depends on
`com.unity.nuget.newtonsoft-json` (used for the JSON schema variants).

## Architecture

The package is organised around the spec's four pillars and a central controller:

```
Runtime/
├── Schema/            Engine-agnostic data model + loaders for the 6 core schemas
│   ├── Model/         C# types mirroring attribute, attribute_set, gameplay_effect,
│   │                  gameplay_ability, gameplay_tag, gameplay_controller
│   ├── Yaml/          Dependency-free reader for the genre-pack entities/*.yaml subset
│   └── SchemaLoader   Public entry point (From*Yaml / Load*)
├── Attributes/        Pillar 1 — dual-value Base/Current + §5 aggregation pipeline
├── Tags/              Pillar 2 — hierarchical reference-counted tag container (§7)
├── Effects/           Pillar 3 — Instant/HasDuration/Infinite + execution policies (§9)
├── Abilities/         Pillar 4 — lifecycle state machine + ability tasks (§8, §10)
├── Networking/        Non-goal stub (§13) — blocked on jbltx/ugas#4, #5, #7
├── Persistence/       Save/restore per §14
└── GameplayController The authoritative container wiring the pillars together (§4)
```

The `Runtime` assembly has **no `UnityEngine` dependency** — the core logic is pure C#, which keeps
it engine-agnostic and unit-testable headlessly. Unity-specific glue (e.g. `MonoBehaviour`
adapters) can be layered on top later without touching the core.

## Quick start

```csharp
using Jbltx.Ugas;
using Jbltx.Ugas.Attributes;
using Jbltx.Ugas.Schema;

// 1. Load an attribute set and some effects from UGAS YAML (genre-pack entities).
var setDef    = SchemaLoader.LoadAttributeSet("path/to/rpg/entities/attribute_set.yaml");
var mainStat  = SchemaLoader.LoadEffect("path/to/rpg/entities/effect_mainstat_strength.yaml");
var fireSword = SchemaLoader.LoadEffect("path/to/rpg/entities/effect_weapon_firesword.yaml");

// 2. Build a controller and register the set.
var gc = new GameplayController { OwnerActor = new ActorReference { ActorID = "Hero" } };
gc.RegisterAttributeSet(new AttributeSet(setDef));
gc.FindAttribute("Strength").BaseValue = 50f;

// 3. Apply two Infinite effects that feed different damage-bucket channels.
gc.ApplyEffect(mainStat);   // +1% WeaponDamage per Strength, "MainStat" channel
gc.ApplyEffect(fireSword);  // +20% WeaponDamage,            "DamageBonuses" channel
gc.RecalculateAttributes();

// base 10  x  MainStat(1 + 0.01*50 = 1.50)  x  DamageBonuses(1 + 0.20 = 1.20)  =  18.0
Debug.Log(gc.FindAttribute("WeaponDamage").CurrentValue); // 18
```

## The modifier-aggregation pipeline (§5)

The Attributes pillar implements the spec's ordered pipeline exactly:

```
CurrentValue = clamp( ( Base + Σ Add )
                      × Π_channels( 1 + Σ Multiply_in_channel )
                      + Σ AddPost,
                      Min, Max )
```

with an `Override` step (highest `Priority` wins; last-applied wins on tie) replacing the result
before clamping. The key channel rule: **`Multiply` bonuses in the same channel sum** (two +20% →
×1.40), while **different channels multiply** (×1.40 and ×1.30 → ×1.82). This is what powers ARPG
damage-bucket designs.

## Implementation status

| Pillar / area | Status | Notes |
| --- | --- | --- |
| Schema loading (6 core schemas + entities) | **Implemented** | YAML subset reader + typed model; loads real genre packs |
| Attributes (dual-value + pipeline + clamping) | **Implemented** | Full §5 pipeline incl. channels, Override, attribute-ref clamps |
| Gameplay Tags (hierarchy + queries + refcount) | **Implemented** | `HasTag`/`HasTagExact`/`HasAny`/`HasAll`/`HasNone`, 0↔1 events |
| Gameplay Effects | **Core implemented** | Instant application, HasDuration/Infinite tracking, periodic ticks, tag grants. `RunInSequence`/`RunInMerge` scheduling and custom `Executions` are stubbed |
| Gameplay Abilities | **Core implemented** | §8 lifecycle state machine + activation validation. Cost/cooldown application and ability-task execution are stubbed |
| Gameplay Controller (§4) | **Implemented** | Wires pillars; builds the active-modifier list; brokers activation/application |
| State persistence (§14) | **Implemented** | Capture + hydrate following the exact §14 restoration order |
| Ability Tasks (§10) | **Interface stub** | `IAbilityTask` defined; no concrete tasks/scheduler yet |
| Networking & prediction (§13) | **Non-goal (stub)** | Blocked on jbltx/ugas#4, #5, #7 |

Stubbed areas are marked in source with `// TODO` (and `// TODO(jbltx/ugas#…)` for networking).

## Conformance tests & CI

Conformance fixtures under `Tests/` load real UGAS case-study / genre entities (copied into
`Tests/ConformanceData/`) and assert spec behaviour — including the worked Barbarian example
(`WeaponDamage == 18.0`), tag hierarchy semantics, effect lifecycles, and a §14 save/restore
round-trip. Tests for stubbed behaviour use `[Ignore("pending …")]` placeholders so the harness
compiles and documents intent.

CI ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) builds the Runtime as netstandard2.1
(warnings-as-errors) and **runs** the NUnit suite via `dotnet test`, using the mirror build
projects under `ci/`. This needs no Unity license; an in-editor pass via
`game-ci/unity-test-runner` can be added later.

To run the suite locally without Unity:

```bash
dotnet test ci/Tests/Jbltx.Ugas.Tests.Build.csproj
```

## License

[MIT](LICENSE) — matching the parent [jbltx/ugas](https://github.com/jbltx/ugas) specification repo.
