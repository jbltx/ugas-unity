# UGAS for Unity

**Unity reference implementation of [UGAS](https://github.com/jbltx/ugas)** — the Universal
Gameplay Ability System specification — as a Unity Package Manager (UPM) package
(`com.jbltx.ugas`).

This package is **Unity-native**: gameplay data is authored as **ScriptableObjects**, the runtime is
a **MonoBehaviour** controller ticked off the player loop, and the UGAS spec packs
(`schemas/*` + genre `entities/*.yaml`) are an **import source** converted into asset definitions by
an editor `ScriptedImporter`. An optional **DOTS/Burst** backend accelerates batched evaluation when
`com.unity.entities` is installed. It tracks the umbrella issue
[jbltx/ugas#27](https://github.com/jbltx/ugas/issues/27).

> **Status: scaffold (0.2.0).** The shared aggregation kernel, the Attributes and Gameplay Tags
> pillars, the SO definitions, the editor importer, and the MonoBehaviour controller are implemented
> and tested. The Effects and Abilities pillars implement their core paths; advanced behaviour is
> stubbed with clear `TODO`s. Networking is an explicit non-goal for now.

## Requirements

- **Unity 6000.3 (Unity 6.3)** or newer.
- No hard package dependencies. `com.unity.entities` and `com.unity.burst` are **optional** — install
  them to enable the DOTS-accelerated backend (see [Two backends](#two-backends-managed--dots)).

## Installation

Add via the Unity Package Manager using the Git URL:

```
https://github.com/jbltx/ugas-unity.git
```

Or in `Packages/manifest.json`:

```json
{ "dependencies": { "com.jbltx.ugas": "https://github.com/jbltx/ugas-unity.git" } }
```

## Architecture

```
Runtime/                         Jbltx.Ugas.Runtime (always compiled)
├── Kernel/                      Shared, Burst-compatible §5 aggregation math (ModifierSample, AttributeKernel)
├── Definitions/                 ScriptableObject definitions (the data model)
│   ├── AttributeSetDefinition       §6
│   ├── GameplayEffectDefinition     §9
│   ├── GameplayAbilityDefinition    §8
│   ├── GameplayTagRegistry          §7
│   └── GameplayControllerConfig     §4
├── Tags/                        Interned GameplayTag handles + hierarchical container (§7)
├── Engine/                      Live instances + UgasController : MonoBehaviour (§4)
│   ├── RuntimeAttribute / RuntimeAttributeSet
│   ├── ActiveGameplayEffect (pooled) / GameplayEffectsSystem (§9)
│   ├── GameplayAbility (lifecycle, §8)
│   └── UgasController
├── Abilities/                   AbilityState machine + IAbilityTask (§8/§10)
└── Networking/                  Non-goal stub (§13)

Dots/                            Jbltx.Ugas.Dots (compiled ONLY when com.unity.entities present)
├── UgasDotsComponents.cs        IComponentData / IBufferElementData mirrors
└── AttributeAggregationSystem   Burst ISystem + IJobEntity calling the SAME kernel

Editor/                          Jbltx.Ugas.Editor (editor-only)
└── Import/                      ScriptedImporter (.ugasentity) + spec→SO mapper + "Import Spec Pack…" menu
```

### Why this shape

- **Definitions are ScriptableObjects.** Unity serializes them to `.asset` YAML and gives you
  inspectors for free, so there is no runtime YAML/JSON parser.
- **Spec packs are imported, not parsed at runtime.** The editor `UgasSpecImporter` (a
  `ScriptedImporter` on the `.ugasentity` extension) converts a spec entity file into the matching SO.
  The YAML reader and the spec→SO mapping live entirely in the editor assembly.
- **The runtime uses engine idioms.** `UgasController : MonoBehaviour` owns the live attribute/effect/
  ability instances and ticks them from `Update`. Tags are interned to `int` handles (no string
  compares); modifier aggregation is struct-based and allocation-free; active-effect records are
  pooled.

## Two backends (managed + DOTS)

Both backends call the **same shared kernel** (`AttributeKernel`), so results are identical — only
performance differs.

| Backend | When | Packages |
| --- | --- | --- |
| **Managed** (default) | always available | none |
| **DOTS/Burst** | auto-selected when compiled in | `com.unity.entities`, `com.unity.burst` |

The DOTS path is a **soft dependency**. The mechanism:

- `Jbltx.Ugas.Dots.asmdef` references `Unity.Entities`/`Unity.Burst`/`Unity.Collections`, declares
  `versionDefines` mapping `com.unity.entities` → `UGAS_DOTS` (and `com.unity.burst` → `UGAS_BURST`),
  and sets `"defineConstraints": ["UGAS_DOTS"]`. Because version-defines are evaluated **before**
  define-constraints, the assembly compiles when Entities is present and is **excluded entirely** when
  it is absent — so its `Unity.Entities` references never cause errors in a project without DOTS.
- The main runtime guards any Burst-specific usage behind `#if UGAS_BURST` and exposes
  `UgasBackend.Active` to report which path is in effect.
- `package.json` lists **neither** Entities nor Burst under `dependencies`.

## Quick start

```csharp
using Jbltx.Ugas.Runtime;
using UnityEngine;

// Assign a GameplayControllerConfig asset (authored, or imported from a spec pack) in the inspector;
// the controller bootstraps from it in Awake. Or wire it up in code:
var go = new GameObject("Hero");
var gc = go.AddComponent<UgasController>();
gc.RegisterAttributeSet(new RuntimeAttributeSet(myAttributeSetDefinition));
gc.FindAttribute("Strength").BaseValue = 50f;
gc.ApplyEffect(mainStatEffect);   // +1% WeaponDamage per Strength, "MainStat" channel
gc.ApplyEffect(fireSwordEffect);  // +20% WeaponDamage,            "DamageBonuses" channel

// base 10 x MainStat(1.50) x DamageBonuses(1.20) = 18.0
Debug.Log(gc.GetCurrentValue("WeaponDamage")); // 18
```

## Importing UGAS spec packs

1. **Assets ▸ UGAS ▸ Import Spec Pack…** and pick a genre pack folder (e.g. `genres/rpg`). Its
   `entities/*.yaml` are copied into your project as `.ugasentity` files and imported into SO assets.
2. Or copy/rename a single entity to `*.ugasentity`; the `ScriptedImporter` converts it on import.

The importer detects each entity's kind (attribute set / effect / ability / tag registry / controller)
from its root keys, so one importer handles every UGAS entity type.

## The modifier-aggregation pipeline (§5)

`AttributeKernel.Aggregate` implements the spec pipeline exactly:

```
CurrentValue = clamp( ( Base + Σ Add ) × Π_channels( 1 + Σ Multiply_in_channel ) + Σ AddPost, Min, Max )
```

with `Override` (highest `Priority`; last-applied on tie) replacing the result before clamping.
**Same-channel `Multiply` bonuses sum** (two +20% → ×1.40); **different channels multiply**
(×1.40 and ×1.30 → ×1.82). Channels are interned to ints; the kernel is span-based and allocation-free
so the identical code runs in the managed path and inside Burst jobs.

## Implementation status

| Pillar / area | Status |
| --- | --- |
| Shared aggregation kernel (§5 math) | **Implemented** |
| ScriptableObject definitions (all entity kinds) | **Implemented** |
| Editor ScriptedImporter + spec→SO mapper | **Implemented** |
| Attributes pillar (dual-value, pipeline, clamping incl. attribute-ref) | **Implemented** |
| Gameplay Tags pillar (interned handles, hierarchy, refcount) | **Implemented** |
| `UgasController` MonoBehaviour wiring + Update tick | **Implemented** |
| Gameplay Effects (Instant, HasDuration/Infinite, periodic, pooled records) | **Core implemented**; RunInSequence/RunInMerge stubbed |
| Gameplay Abilities (§8 lifecycle + activation validation) | **Core implemented**; cost/cooldown + tasks stubbed |
| DOTS/Burst backend (optional) | **Scaffold** — components + Burst job calling the shared kernel |
| Ability Tasks (§10) | **Interface stub** |
| State persistence (§14) | Planned (see issues) |
| Networking & prediction (§13) | **Non-goal (v1)** — spec gaps jbltx/ugas#4/#5/#7 resolved upstream; deferred |

## Tests & CI

Tests use the **Unity Test Framework**:

- `Tests/Editor` (EditMode) — kernel math, tag semantics, the spec→SO importer against real genre
  entities, and `UgasController` integration (incl. the worked `WeaponDamage == 18.0`).
- `Tests/Runtime` (PlayMode) — a `UgasController` ticked off the real player loop advancing a periodic
  effect across frames.

### Running tests locally

Open **`ci/UnityProject`** in Unity 6000.3 — it embeds this package (a `file:` reference) with the
Test Framework wired up, so the EditMode/PlayMode suites appear directly in **Window ▸ General ▸ Test
Runner**. To exercise the DOTS/Burst path, add `com.unity.entities` to that project (which defines
`UGAS_DOTS` and compiles the `Dots/` assembly).

### CI

CI ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)):

- **validate-structure** — no Unity license; checks `package.json` and asmdef soft-dependency gating.
  Runs on every push/PR and is the gating check.
- **unity-tests** — EditMode + PlayMode via `game-ci/unity-test-runner` on Unity **6000.3.0f1**.
  **Manual-only** (Actions ▸ CI ▸ Run workflow): it does not run on push/PR, so it never gates a PR or
  consumes a Unity license activation automatically. It needs the license secrets (see the workflow
  header) — `UNITY_EMAIL` + `UNITY_PASSWORD` + either `UNITY_LICENSE` (personal) or `UNITY_SERIAL`
  (pro) — plus an available activation seat. Until then, verify locally (above).

## License

[MIT](LICENSE) — matching the parent [jbltx/ugas](https://github.com/jbltx/ugas) specification repo.
