# Progress Details — 01-upgrade-foundation-projects

## What Changed

- Updated `OnlyM.CoreSys/OnlyM.CoreSys.csproj`:
  - `TargetFramework` changed from `net9.0-windows` to `net10.0-windows`.
- Updated `OnlyM.CustomControls/OnlyM.CustomControls.csproj`:
  - `TargetFramework` changed from `net9.0-windows` to `net10.0-windows`.
- Updated task working notes in:
  - `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/01-upgrade-foundation-projects/task.md`.

## Validation Results

- `dotnet build OnlyM.CoreSys/OnlyM.CoreSys.csproj` ✅ Succeeded
- `dotnet build OnlyM.CustomControls/OnlyM.CustomControls.csproj` ✅ Succeeded

Both Tier 1 foundation projects now restore and build on `net10.0-windows`.

## Issues Encountered

- No blocking issues encountered during this task.
