# 02.02-upgrade-onlym-coresys-tests: Retarget OnlyM.CoreSys.Tests and update deprecated test package usage

# 02.02-upgrade-onlym-coresys-tests: Upgrade OnlyM.CoreSys.Tests

## Objective
Retarget `OnlyM.CoreSys.Tests` to `net10.0-windows` and resolve test dependency issues (including deprecated package guidance).

## Scope
- `OnlyM.CoreSys.Tests/OnlyM.CoreSys.Tests.csproj`
- Test files under `OnlyM.CoreSys.Tests/` if required

## Done when
- Project targets `net10.0-windows`
- Restore/build succeeds
- Test package references are compatible and non-deprecated where upgrade path exists
