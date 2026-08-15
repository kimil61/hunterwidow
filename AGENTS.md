# Repository Guidelines

## Project Structure & Module Organization

Unity 6.3 (`6000.3.12f1`) uses 2D URP. `Assets/Scenes/` holds scenes, `Assets/Settings/` URP assets, and `Docs/PRD.md` is authoritative. Keep configuration in `Packages/` and `ProjectSettings/`; do not commit generated `Library/`, `Temp/`, `Logs/`, or `UserSettings/`.

Keep the split: pure C# rules in `Assets/Scripts/Domain/` (`Game.Domain.asmdef`) and Unity-facing code in `Assets/Scripts/Unity/` (`Game.Unity.asmdef`). Domain code must compile without `UnityEngine`. Put JSON/CSV content under `Assets/StreamingAssets/content/`, utilities under `Tools/`, and tests under `Assets/Tests/EditMode/`.

## Build, Test, and Development Commands

Set a PowerShell variable to the required editor executable, then open the project:

```powershell
$env:HUNTERWIDOW_UNITY = 'C:\path\to\6000.3.12f1\Editor\Unity.exe'
& $env:HUNTERWIDOW_UNITY -projectPath $PWD
```

Run EditMode tests headlessly and inspect the XML, not only the exit code:

```powershell
& $env:HUNTERWIDOW_UNITY -batchmode -projectPath $PWD -runTests -testPlatform EditMode -testResults "$PWD\Logs\EditMode.xml" -logFile "$PWD\Logs\EditMode.log"
```

Build the Windows MVP through the scripted entry point:

```powershell
$env:HUNTERWIDOW_BUILD_PATH = "$PWD\Builds\HunterWidowMvp.exe"
& $env:HUNTERWIDOW_UNITY -batchmode -quit -projectPath $PWD -executeMethod HunterWidow.Editor.HunterWidowBuild.BuildWindowsMvp -logFile "$PWD\Logs\Build.WindowsMvp.log"
```

Run `./Tools/VerifyMvp.ps1 -UnityPath $env:HUNTERWIDOW_UNITY -BuildPlayer` for both packs, 22 cycles, architecture checks, test XML, and build. CI repeats them on push/PR; configure `UNITY_LICENSE` before Unity jobs.

## Coding Style & Naming Conventions

Use four-space indentation and standard C# naming: `PascalCase` for types and public members, `camelCase` for parameters, locals, and private fields. Keep Domain logic deterministic by injecting time, input, and seeded RNG. Do not embed display text or balance values in C#; store them in the content pack. Content IDs are globally unique lowercase snake case with PRD prefixes such as `wpn_`, `enm_`, and `rcp_`. Commit every Unity asset with its `.meta` file.

## Testing Guidelines

Use Unity Test Framework 1.6 and name fixtures `*Tests.cs`. The project intentionally favors EditMode tests for Domain logic; do not add PlayMode tests without changing the PRD decision. There is no numeric coverage target. Cover boundary values, state transitions, deterministic simulations, and full `DiveSession`/`CycleSession` flows. Treat content validation separately from logic tests. Passing automation does not approve combat feel—record manual Play-mode observations for feel-sensitive changes.

## Commit & Pull Request Guidelines

History currently contains only `초기 체크인`, so no formal message convention is established. Use a short imperative subject and one concern per commit, for example `Add ChargeLogic boundary tests`. PRs should state the slice and rationale, list test commands/results, link the issue, and include screenshots or clips for scene/UI changes. Call out schema, save-format, or tuning changes, and keep unrelated Unity settings out of the diff.
