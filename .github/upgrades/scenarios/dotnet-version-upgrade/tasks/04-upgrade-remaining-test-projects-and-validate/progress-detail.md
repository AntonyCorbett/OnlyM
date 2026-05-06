# Progress Details — 04-upgrade-remaining-test-projects-and-validate

## What Changed

- Updated target framework to `net10.0-windows` in:
  - `IntegrationTests/IntegrationTests.csproj`
  - `OnlyM.Tests/OnlyM.Tests.csproj`
- Updated task notes:
  - `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/04-upgrade-remaining-test-projects-and-validate/task.md`

## Validation Results

Build validation:
- `dotnet build IntegrationTests/IntegrationTests.csproj -m:1` ✅ Succeeded
- `dotnet build OnlyM.Tests/OnlyM.Tests.csproj -m:1` ⚠️ Initial build attempts had transient file-lock issues in dependent WPF outputs
- `dotnet build OnlyM.sln -m:1` ⚠️ Fails in CLI due native C++ project (`OnlyMMirror.vcxproj`) requiring VS C++ MSBuild targets; .NET projects build successfully
- `run_build` ✅ Build successful in Visual Studio workspace

Test validation:
- `dotnet test IntegrationTests/IntegrationTests.csproj -m:1 --no-build` ✅ Passed (4/4)
- `dotnet test OnlyM.Tests/OnlyM.Tests.csproj -m:1` ✅ Passed (20/20)
- `dotnet test OnlyM.Core.Tests/OnlyM.Core.Tests.csproj -m:1 --no-build` ✅ Passed (114/114)
- `dotnet test OnlyM.CoreSys.Tests/OnlyM.CoreSys.Tests.csproj -m:1 --no-build` ✅ Passed (40/40)

## Issues Encountered

- Intermittent file-lock errors during CLI solution/project build for WPF outputs (`obj`/`bin` files in use). Retrying with sequential builds and terminating stale host processes cleared these transient failures.
- CLI full solution build includes native project `OnlyMMirror.vcxproj`; this project is intentionally out of .NET TFM scope and can fail under dotnet CLI when C++ targets are unavailable.
