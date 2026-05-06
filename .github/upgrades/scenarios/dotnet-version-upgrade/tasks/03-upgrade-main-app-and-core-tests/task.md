# 03-upgrade-main-app-and-core-tests: Upgrade Main App and Core Tests

Upgrade the main application and direct app-adjacent tests (`OnlyM`, `OnlyM.Core.Tests`) to `net10.0-windows`, update recommended package versions (including applicable upgrades flagged in assessment), and resolve high-impact API compatibility errors in app code paths.

**Done when**: `OnlyM` and `OnlyM.Core.Tests` target `net10.0-windows`, solution restore succeeds, and these projects build successfully.

## Execution Notes

Applied project updates:
- `OnlyM/OnlyM.csproj`
  - `TargetFramework`: `net9.0-windows` → `net10.0-windows`
  - `CefSharp.Wpf.NETCore`: `143.0.90` → `137.0.100` (assessment compatibility guidance)
  - `FFME.Windows`: `4.4.350` → `4.2.330` (assessment compatibility guidance)
  - `Microsoft.Extensions.DependencyInjection`: `10.0.1` → `10.0.7` (recommended update)
  - `chromiumembeddedframework.runtime.win-*`: `137.0.10` → `137.0.100` for version alignment with CefSharp package line
- `OnlyM.Core.Tests/OnlyM.Core.Tests.csproj`
  - `TargetFramework`: `net9.0-windows` → `net10.0-windows`
