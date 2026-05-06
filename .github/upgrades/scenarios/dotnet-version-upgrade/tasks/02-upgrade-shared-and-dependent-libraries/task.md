# 02-upgrade-shared-and-dependent-libraries: Upgrade Shared and Dependent Libraries

Upgrade Tier 2 and Tier 3 library-layer projects (`OnlyM.Slides`, `OnlyM.Core`, `OnlyMSlideManager`) plus `OnlyM.CoreSys.Tests`, then address package/API compatibility findings for this layer while keeping dependency alignment with completed lower tiers.

**Done when**: All projects in this layer target `net10.0-windows`, compile successfully, and dependent references resolve cleanly.

## Execution Notes

Completed upgrades in this layer:
- Retargeted to `net10.0-windows`:
  - `OnlyM.Slides/OnlyM.Slides.csproj`
  - `OnlyM.CoreSys.Tests/OnlyM.CoreSys.Tests.csproj`
  - `OnlyM.Core/OnlyM.Core.csproj`
  - `OnlyMSlideManager/OnlyMSlideManager.csproj`
- Applied package updates from assessment recommendations:
  - `OnlyM.Core`: `FFME.Windows` `4.4.350` → `4.2.330`
  - `OnlyM.CoreSys.Tests`: `Microsoft.NET.Test.Sdk` `17.12.0` → `18.0.1`
  - `OnlyMSlideManager`: `Microsoft.Extensions.DependencyInjection` `10.0.1` → `10.0.7`

Validation completed with successful project-level builds for all four upgraded projects.
