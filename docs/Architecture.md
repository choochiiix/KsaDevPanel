# Architecture

StarMap loads `DevPanelMod` from the isolated profile manifest. The BeforeMain callback verifies the host build, API and effective documents path, then creates the run marker. AfterGui samples live state and passes an immutable vessel snapshot to the workbench. The snapshot is replaced on every draw. The adapter owns all live game reads.

Session diagnostics hold bounded, structured events and format a compact clipboard report. The workbench owns the hotkey and layout. Settings are stored in the verified isolated profile. The mod has no gameplay commands and adds no Harmony patches of its own.
