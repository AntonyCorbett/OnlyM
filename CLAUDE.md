# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

OnlyM is a Windows WPF media player application (.NET 10, x86) used in presentation contexts (church, events). It plays video, audio, images, PDFs, and web content on a secondary monitor/display. The repo also contains OnlyMSlideManager, a companion app for creating slide presentations.

## Build and Run

```powershell
# Build the solution
dotnet build OnlyM.sln

# Build in Release mode
dotnet build OnlyM.sln -c Release

# Run the main application
dotnet run --project OnlyM/OnlyM.csproj
```

All projects target `net10.0-windows` and use **x86** platform target.

## Testing

```powershell
# Run all unit tests
dotnet test OnlyM.sln

# Run tests in a specific project
dotnet test OnlyM.Core.Tests/OnlyM.Core.Tests.csproj
dotnet test OnlyM.Tests/OnlyM.Tests.csproj
dotnet test OnlyM.CoreSys.Tests/OnlyM.CoreSys.Tests.csproj

# Run integration tests (MSTest)
dotnet test IntegrationTests/IntegrationTests.csproj

# Run a single test by name
dotnet test OnlyM.Core.Tests/OnlyM.Core.Tests.csproj --filter "FullyQualifiedName~TestName"
```

Unit tests use **xUnit v3** with Moq. `OnlyM.CoreSys.Tests` uses `Xunit.StaFact` for WPF STA thread requirements. Integration tests use MSTest.

## Architecture

### Project Layout

| Project | Role |
|---|---|
| `OnlyM` | Main WPF application (entry point) |
| `OnlyM.Core` | Business logic, services, options, database (SQLite) |
| `OnlyM.CoreSys` | Image/thumbnail processing (ImageSharp, PhotoSauce, TagLib) |
| `OnlyM.Slides` | Slide/presentation file model |
| `OnlyM.CustomControls` | Reusable WPF controls |
| `OnlyMSlideManager` | Companion app to build slide packages |
| `OnlyMMirror` | Native C++ component for display duplication |

### MVVM Pattern

The app uses **CommunityToolkit.Mvvm** with constructor-injected services via `Microsoft.Extensions.DependencyInjection`. ViewModels live in `OnlyM/ViewModel/`; views in `OnlyM/Windows/`. The `PageService` manages navigation between the Operator and Settings pages.

Key ViewModels:
- `MainViewModel` — top-level window state
- `OperatorViewModel` — media list, playback control
- `MediaViewModel` — the output (media window) state
- `SettingsViewModel` — user preferences

### Two-Window Design

The app runs two WPF windows simultaneously:
- **Operator window** (`OperatorPage`) — control surface shown to the operator
- **Media window** (`MediaWindow`) — full-screen output on the presentation monitor

The `MonitorsService` detects available displays; the media window is placed on the configured secondary monitor.

### Service Registration

All services are registered in `App.xaml.cs`. Key services:
- `OptionsService` — persists user settings to a JSON file
- `DatabaseService` — SQLite database via `System.Data.SQLite`
- `MediaProviderService` / `MediaMetaDataService` — file discovery and metadata
- `ThumbnailService` — background thumbnail generation
- `DragAndDropService` — drag-and-drop into the media list
- `WebBrowserService` — CefSharp-backed web content display

### Pub/Sub Messaging

Inter-component communication uses a pub/sub pattern. Message types are in `OnlyM/PubSubMessages/`.

### Localization

Resource `.resx` files live in `Properties/` directories. Translations are managed via **Crowdin** (`crowdin.yml`). A PreBuild target copies locale-specific resource files (e.g., `Resources.no-NO.resx` → `Resources.no.resx`) before compilation.

## Code Style

StyleCop.Analyzers is enforced across all projects. The `.editorconfig` at the root configures the rules. Key constraints:
- No `this.` qualification on fields or properties
- Accessibility modifiers required on all members
- Braces required on all control flow blocks
- `var` only for non-built-in types
- Expression-bodied members for single-line properties
- CRLF line endings, UTF-8 with BOM
- System `using` directives sorted before others

StyleCop violations produce build warnings/errors — keep the build warning-free.

## Key Dependencies

- **CefSharp.Wpf.NETCore** — renders PDF files and web content (initialized in `App.xaml.cs`)
- **FFME.Windows** — FFmpeg-backed video/audio playback
- **NAudio** — additional audio support
- **MaterialDesignThemes** — UI theming (DeepPurple/Lime palette)
- **Serilog** — file logging (daily rolling, 28-day retention, in `%AppData%`)
- **Sentry** — error/crash reporting
- **Newtonsoft.Json** — settings serialization
- **System.Data.SQLite** — local database

## Platform Notes

- Windows 10+ only; WPF app, not cross-platform.
- All projects compile to **x86** (32-bit) regardless of host architecture — required for SQLite and some native dependencies.
- `OnlyMMirror` is a C++ vcxproj; it must be built through Visual Studio or MSBuild with the C++ workload installed.
- `unsafe` blocks are enabled in `OnlyM.Core` and `OnlyM.CoreSys`.
