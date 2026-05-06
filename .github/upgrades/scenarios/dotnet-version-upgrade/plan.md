# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade the .NET projects in `OnlyM.sln` from `net9.0-windows` to `net10.0-windows`, including required package and code compatibility fixes.
**Scope**: Medium-sized WPF solution (11 projects total; 10 .NET projects in scope, 1 native C++ project out of scope for TFM change).

## Tasks

### Selected Strategy
**Bottom-Up (Dependency-First)** — Upgrade from leaf nodes to root applications, tier by tier.
**Rationale**: 10 .NET projects in scope with a multi-tier dependency graph and substantial binary/source compatibility findings that benefit from incremental validation.

Dependency graph visualization:

Tier 5: [IntegrationTests] [OnlyM.Tests]
         ↓                ↓
Tier 4: [OnlyM] [OnlyM.Core.Tests]
         ↓       ↓
Tier 3: [OnlyM.Core] [OnlyMSlideManager]
         ↓         ↓
Tier 2: [OnlyM.Slides] [OnlyM.CoreSys.Tests]
         ↓
Tier 1: [OnlyM.CoreSys] [OnlyM.CustomControls]

### 01-upgrade-foundation-projects
Upgrade Tier 1 foundation projects (`OnlyM.CoreSys`, `OnlyM.CustomControls`) to `net10.0-windows`, apply package compatibility updates for these projects, and resolve compile-time API compatibility issues introduced by the framework bump.

**Done when**: Both Tier 1 projects target `net10.0-windows`, restore successfully, and build without errors.

---

### 02-upgrade-shared-and-dependent-libraries
Upgrade Tier 2 and Tier 3 library-layer projects (`OnlyM.Slides`, `OnlyM.Core`, `OnlyMSlideManager`) plus `OnlyM.CoreSys.Tests`, then address package/API compatibility findings for this layer while keeping dependency alignment with completed lower tiers.

**Done when**: All projects in this layer target `net10.0-windows`, compile successfully, and dependent references resolve cleanly.

---

### 03-upgrade-main-app-and-core-tests
Upgrade the main application and direct app-adjacent tests (`OnlyM`, `OnlyM.Core.Tests`) to `net10.0-windows`, update recommended package versions (including applicable upgrades flagged in assessment), and resolve high-impact API compatibility errors in app code paths.

**Done when**: `OnlyM` and `OnlyM.Core.Tests` target `net10.0-windows`, solution restore succeeds, and these projects build successfully.

---

### 04-upgrade-remaining-test-projects-and-validate
Upgrade remaining higher-tier test projects (`IntegrationTests`, `OnlyM.Tests`) and perform full solution validation: restore, build, and automated test execution for the .NET test projects.

**Done when**: All in-scope .NET projects target `net10.0-windows`, full solution build succeeds, and test runs complete without failures.

---