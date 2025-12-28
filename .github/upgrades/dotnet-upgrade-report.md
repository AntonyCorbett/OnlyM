# .NET 9 Upgrade Report

## Project target framework modifications

| Project name                                   | Old Target Framework    | New Target Framework | Commits   |
|:-----------------------------------------------|:-----------------------:|:--------------------:|:----------|
| OnlyM.CoreSys\OnlyM.CoreSys.csproj             |   net8.0-windows        | net9.0-windows       | 1cd161b2  |
| OnlyM.Slides\OnlyM.Slides.csproj               |   net8.0-windows        | net9.0-windows       | 0b3fd579  |
| OnlyM.CustomControls\OnlyM.CustomControls.csproj|  net8.0-windows        | net9.0-windows       | 6e089576  |
| OnlyM.Core\OnlyM.Core.csproj                   |   net8.0-windows        | net9.0-windows       | a84c74fb  |
| OnlyM\OnlyM.csproj                              |   net8.0-windows        | net9.0-windows       | 208de1d0  |
| OnlyM.Tests\OnlyM.Tests.csproj                 |   net8.0-windows        | net9.0-windows       | 237ea0b0  |
| OnlyM.Core.Tests\OnlyM.Core.Tests.csproj       |   net8.0-windows        | net9.0-windows       | 22318bea  |
| OnlyMSlideManager\OnlyMSlideManager.csproj     |   net8.0-windows        | net9.0-windows       | f6a0c70d  |
| IntegrationTests\IntegrationTests.csproj       |   net8.0-windows        | net9.0-windows       | 02e86e42  |

## NuGet Packages

| Package Name                        | Old Version | New Version | Commit Id |
|:------------------------------------|:-----------:|:-----------:|:----------|
| CefSharp.Wpf.NETCore                |   143.0.90  |  137.0.100  | ad8bd51a  |
| FFME.Windows                        |   4.4.350   |  4.2.330    | ca5bbe6a  |
| Microsoft.Extensions.DependencyInjection | 9.0.6  |  9.0.11     | c1814b84  |

## All commits

| Commit ID  | Description                                |
|:-----------|:-------------------------------------------|
| c7f17f5d   | Commit upgrade plan                         |
| 1cd161b2   | Update OnlyM.CoreSys.csproj to target .NET 9.0 |
| 0b3fd579   | Update OnlyM.Slides.csproj to target .NET 9.0 |
| 6e089576   | Update OnlyM.CustomControls.csproj to target .NET 9.0 |
| a84c74fb   | Update OnlyM.Core.csproj to target .NET 9.0 |
| ca5bbe6a   | Downgrade FFME.Windows in OnlyM.Core.csproj |
| ad8bd51a   | Update package versions in OnlyM.csproj     |
| 208de1d0   | Update OnlyM.csproj to target .NET 9.0 for Windows |
| 237ea0b0   | Update OnlyM.Tests.csproj to target net9.0-windows |
| 22318bea   | Update OnlyM.Core.Tests.csproj to target .NET 9.0 |
| f6a0c70d   | Update OnlyMSlideManager.csproj to target .NET 9.0 |
| c1814b84   | Update DependencyInjection to v9.0.11 in OnlyMSlideManager.csproj |
| 02e86e42   | Update IntegrationTests.csproj to target net9.0-windows |

## Project feature upgrades

No feature upgrade sections were present in the plan.

## Next steps

- Run the full test suite and validate.
- Verify application behavior with CefSharp and FFME.Windows versions.
- Merge the branch and create a release.

---
Tokens and cost summary:
- Model input tokens: N/A
- Model output tokens: N/A
- Estimated cost: N/A
