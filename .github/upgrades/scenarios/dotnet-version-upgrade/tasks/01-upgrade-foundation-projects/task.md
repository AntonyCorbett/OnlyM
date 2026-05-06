# 01-upgrade-foundation-projects: Upgrade Foundation Projects

Upgrade Tier 1 foundation projects (`OnlyM.CoreSys`, `OnlyM.CustomControls`) to `net10.0-windows`, apply package compatibility updates for these projects, and resolve compile-time API compatibility issues introduced by the framework bump.

**Done when**: Both Tier 1 projects target `net10.0-windows`, restore successfully, and build without errors.

## Research Notes

- `get_project_dependencies` confirms both projects are SDK-style and do not use central package management.
- `TargetFramework` is defined directly inside each project file (`OnlyM.CoreSys.csproj`, `OnlyM.CustomControls.csproj`), so retargeting should be done in-place in those files.
- Assessment indicates no package incompatibility for these two projects; package version changes are not required for this task.
- Scope for this task is therefore:
  1. Replace `net9.0-windows` with `net10.0-windows` in both project files.
  2. Restore/build the two projects to validate foundation tier upgrade.

## Execution Plan

1. Update `OnlyM.CoreSys/OnlyM.CoreSys.csproj` target framework.
2. Update `OnlyM.CustomControls/OnlyM.CustomControls.csproj` target framework.
3. Run project-level build validation for both projects.
4. If failures appear, fix only issues in Tier 1 scope and revalidate.
