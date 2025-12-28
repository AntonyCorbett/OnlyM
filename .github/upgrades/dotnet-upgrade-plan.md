# .NET 9.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that an .NET 9.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 9.0 upgrade.
3. Upgrade OnlyM.CoreSys\OnlyM.CoreSys.csproj
4. Upgrade OnlyM.Slides\OnlyM.Slides.csproj
5. Upgrade OnlyM.CustomControls\OnlyM.CustomControls.csproj
6. Upgrade OnlyM.Core\OnlyM.Core.csproj
7. Upgrade OnlyM\OnlyM.csproj
8. Upgrade OnlyM.Tests\OnlyM.Tests.csproj
9. Upgrade OnlyM.Core.Tests\OnlyM.Core.Tests.csproj
10. Upgrade OnlyMSlideManager\OnlyMSlideManager.csproj
11. Upgrade IntegrationTests\IntegrationTests.csproj

## Settings

This section contains settings and data used by execution steps.

### Excluded projects

Table below contains projects that do belong to the dependency graph for selected projects and should not be included in the upgrade.

| Project name                                   | Description                 |
|:-----------------------------------------------|:---------------------------:|
| OnlyMMirror\OnlyMMirror.vcxproj                | Explicitly excluded (C++/VC project, non-.NET)

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                        | Current Version | New Version | Description                                   |
|:------------------------------------|:---------------:|:-----------:|:----------------------------------------------|
| CefSharp.Wpf.NETCore                |   143.0.90      |  137.0.100  | Incompatible with .NET 9.0; downgrade recommended for compatibility |
| FFME.Windows                        |   4.4.350       |  4.2.330    | Incompatible with .NET 9.0; downgrade recommended for compatibility |
| Microsoft.Extensions.DependencyInjection | 9.0.6       |  9.0.11     | Update recommended for .NET 9.0 |

### Project upgrade details
This section contains details about each project upgrade and modifications that need to be done in the project.

#### OnlyM.CoreSys\\OnlyM.CoreSys.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0-windows` to `net9.0-windows`

Other changes:
  - None

#### OnlyM.Slides\\OnlyM.Slides.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0-windows` to `net9.0-windows`

Other changes:
  - None

#### OnlyM.CustomControls\\OnlyM.CustomControls.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0-windows` to `net9.0-windows`

Other changes:
  - None

#### OnlyM.Core\\OnlyM.Core.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0-windows` to `net9.0-windows`

NuGet packages changes:
  - FFME.Windows should be updated from `4.4.350` to `4.2.330` (recommended for .NET 9.0)

Other changes:
  - None

#### OnlyM\\OnlyM.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0-windows` to `net9.0-windows`

NuGet packages changes:
  - CefSharp.Wpf.NETCore should be updated from `143.0.90` to `137.0.100` (recommended for .NET 9.0)
  - FFME.Windows should be updated from `4.4.350` to `4.2.330` (recommended for .NET 9.0)
  - Microsoft.Extensions.DependencyInjection should be updated from `9.0.6` to `9.0.11` (recommended for .NET 9.0)

Other changes:
  - None

#### OnlyM.Tests\\OnlyM.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0-windows` to `net9.0-windows`

Other changes:
  - None

#### OnlyM.Core.Tests\\OnlyM.Core.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0-windows` to `net9.0-windows`

Other changes:
  - None

#### OnlyMSlideManager\\OnlyMSlideManager.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0-windows` to `net9.0-windows`

NuGet packages changes:
  - Microsoft.Extensions.DependencyInjection should be updated from `9.0.6` to `9.0.11` (recommended for .NET 9.0)

Other changes:
  - None

#### IntegrationTests\\IntegrationTests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0-windows` to `net9.0-windows`

Other changes:
  - None
