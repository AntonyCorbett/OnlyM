# 04-upgrade-remaining-test-projects-and-validate: Upgrade Remaining Test Projects and Validate

Upgrade remaining higher-tier test projects (`IntegrationTests`, `OnlyM.Tests`) and perform full solution validation: restore, build, and automated test execution for the .NET test projects.

**Done when**: All in-scope .NET projects target `net10.0-windows`, full solution build succeeds, and test runs complete without failures.

## Execution Notes

- Retargeted remaining test projects to `net10.0-windows`:
  - `IntegrationTests/IntegrationTests.csproj`
  - `OnlyM.Tests/OnlyM.Tests.csproj`
- Next validation steps:
  1. Build both updated test projects.
  2. Run full solution build.
  3. Execute automated tests for upgraded test projects.
