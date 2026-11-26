# Changelog

## [Unreleased]

## [0.2.0-alpha.1] - 2026-11-26

### Added

- Added altitude, surface and orbital speed, acceleration, total and propellant mass, and manual throttle to the Vessel Inspector. Grouped the values into readable sections and tucked vector components into expandable details.
- Added the current vessel's ECL velocity vector and speed magnitude to the inspection snapshot.
- Added live refresh of isolated panel shortcut and window settings, with an in-game reload button and validation status.
- Added the read-only vessel snapshot contract, pure display formatting, and isolated-profile build, deployment and log tooling.
- Added the StarMap startup handshake for KSA 2026.11.25.5949 with a per-run identifier.
- Added a developer workbench with Vessel, Diagnostics, and Settings & Refresh tabs.
- Added one workspace development copy and profile that can be reused across mod projects.

### Changed

- Reorganized the workbench into readable sections and a larger default layout.
- Changed the motion readout to show speed and labeled ECL components separately.
- Changed velocity formatting to one decimal place and refresh it five times per second to reduce visual jitter.
- Changed deployment and rollback to preserve independent mod manifest entries and per-mod backup folders.

### Fixed

- Fixed the shared launcher to initialize StarMap's required game path before launch.
- Fixed the shared launcher profile handshake and default workbench visibility for fresh profiles.
- Fixed rollback to restore the prior mod files and manifest entry without replacing other mods.
