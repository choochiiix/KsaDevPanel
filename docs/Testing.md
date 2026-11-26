# Testing

Start with the local build and dependency-free checks. Run the build twice against the same inputs and inspect the staged file list. Dry-run the deployment, deliberately pass the wrong build and unsafe path using synthetic fixtures, and test deploy plus rollback against a fixture with existing content.

Use the disposable instance for the runtime scenarios.

1. Launch twice. Confirm exactly one startup marker per launch and distinct run IDs. Check the first GUI event and explain any loader restart separately.
2. Open and close the workbench with F12 in the menu and active-world views. Test focus with a text input and use normal game controls while the panel is closed and open.
3. Open or create a disposable world with at least two vessels. Switch vessels and check identity, the parent body and parts. Return to the menu or change worlds and confirm the inspector clears promptly.
4. Change the keybind and move and resize the window. Restart with the same profile and confirm both settings survive.
5. Copy Runtime Console diagnostics. Check the run ID, bounded events, and absence of private paths or save contents.
6. Disable the mod, relaunch, and confirm the baseline starts and the workbench is unavailable.

See [Verification](Verification.md) for outcomes and [Development](Development.md) for the local workflow.
