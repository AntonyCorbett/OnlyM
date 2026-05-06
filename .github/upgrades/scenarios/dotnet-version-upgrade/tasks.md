# .NET Version Upgrade Progress

## Overview

Upgrade `OnlyM.sln` from `.NET 9` to `.NET 10` using a dependency-first execution order. The work proceeds from foundational libraries to applications and then test projects to reduce blast radius while resolving API and package compatibility issues.

**Progress**: 0/9 tasks complete (0%) ![0%](https://progress-bar.xyz/0)

## Tasks

- 🔲 01-upgrade-foundation-projects
- 🔄 02-upgrade-shared-and-dependent-libraries
  - 🔲 02.01-upgrade-onlym-slides
  - 🔲 02.02-upgrade-onlym-coresys-tests
  - 🔲 02.03-upgrade-onlym-core
  - 🔲 02.04-upgrade-onlym-slidemanager
  - 🔲 02.05-validate-library-layer
- 🔲 03-upgrade-main-app-and-core-tests
- 🔲 04-upgrade-remaining-test-projects-and-validate