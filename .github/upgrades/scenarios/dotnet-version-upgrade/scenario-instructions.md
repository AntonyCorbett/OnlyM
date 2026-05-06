# .NET Version Upgrade

## Strategy
Bottom-Up (Dependency-First) — upgrade dependency leaves first and move upward to application and integration test projects.

## Preferences
- **Flow Mode**: Automatic
- **Commit Strategy**: After Each Task
- **Pace**: Standard
- **Target Framework**: net10.0-windows
- **Source Branch**: net10-upgrade
- **Working Branch**: upgrade-to-NET10
- **Pending Changes Handling**: Committed before branch switch

## Decisions
- Include package compatibility fixes and recommended upgrades discovered during assessment as part of the upgrade.
- Exclude `OnlyMMirror.vcxproj` from .NET TFM upgrade tasks (native C++ project, no .NET target framework).
- Selected Bottom-Up strategy due to 11-project dependency graph with substantial API compatibility findings and multi-tier project relationships.

## Custom Instructions
<!-- Task-specific overrides: "For {taskId}: {instruction}" -->