# CI support

This folder holds tooling used by continuous integration; it is **not** part of the runtime
package surface.

## `UnityProject/`

A minimal throwaway Unity project that embeds this repository as a local UPM package so
`game-ci/unity-test-runner` can discover and run the package's EditMode/PlayMode tests.

- `Packages/manifest.json` references the package with `"com.jbltx.ugas": "file:../../.."`, which
  resolves to the repository root (the package root).
- `testables` is scoped to `com.jbltx.ugas` so only the package's test assemblies
  (`Jbltx.Ugas.EditorTests`, `Jbltx.Ugas.PlayTests`) are run.
- The editor version (`ProjectSettings/ProjectVersion.txt`) is pinned to **6000.3.0f1** (Unity 6.3),
  matching `package.json`'s `"unity": "6000.3"`.

This project intentionally does **not** depend on `com.unity.entities` / `com.unity.burst`, so CI
exercises the **managed** backend and proves the package compiles and runs with the DOTS soft
dependency absent (the `Jbltx.Ugas.Dots` assembly is excluded by its `UGAS_DOTS` define constraint).
A separate matrix entry could add Entities to also exercise the DOTS path.

## Running tests locally

Open `ci/UnityProject` in Unity 6.3 and use **Window ▸ General ▸ Test Runner**, or run headless:

```bash
Unity -runTests -batchmode -projectPath ci/UnityProject \
  -testPlatform EditMode -testResults results-editmode.xml
```
