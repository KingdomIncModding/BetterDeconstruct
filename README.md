# Better Deconstruct

[GitHub releases](https://github.com/urakkaamyx/KingdomInc_BetterDeconstruct/releases) · [Nexus Mods](https://www.nexusmods.com/kingdominc/mods/24) · Version 1.0.0

Upgrade Deconstruct mode in Kingdom Inc. with drag-box area selection, live refund previews, and a confirmation dialog that breaks down every selected building type with its in-game icon and count before anything is removed.

Single-clicking in Deconstruct mode continues to remove individual buildings, pipes, and decorations immediately as in vanilla, while dragging a box over an area opens a confirmation window so you can review and filter what will be demolished.

## Features

- **Box-Select Deconstruction:** Click and drag a selection rectangle in Deconstruct mode to select an entire area of your workshop at once, complete with automatic camera panning at screen edges and a live refund preview.
- **Confirmation Dialog with In-Game Icons & Counts:** Review a scrollable breakdown of every building type in your selection, displaying each machine's in-game icon, localized name, total unit count (including stacked buildings), and node count.
- **Category Filter Toggles:** Toggle **Buildings**, **Containers**, **Labels**, and **Stickers** directly inside the confirmation popup to include or exclude specific item types on the fly, dynamically updating the item list and refund total.
- **Accurate Batch Refunds:** Calculates and grants the exact total currency refund across all deconstructed buildings and decorations, accounting for diminishing building cost curves and displaying Gold, Silver, Copper, and specialty currencies with their in-game icons.
- **Preserves Single-Click Deconstruction:** Quick single clicks on machines, pipe connections, pipe routing nodes, and decorations still deconstruct immediately without interruption.
- **Foundation Settings Integration:** Configure default category toggles, whether locked decorations are ignored during box selection, and whether Deconstruct mode stays active after confirming under **Options > Mods > Better Deconstruct**.

## Usage and Controls

1. Enter **Deconstruct Mode** using the HUD button or hotkey (`X` by default).
2. **Single-click** any building, pipe, or decoration to deconstruct it immediately as normal.
3. **Click and drag** across the workshop floor to draw a deconstruct selection box:
   - Release the left mouse button to open the **Confirm Deconstruct** window.
   - Toggle **Buildings**, **Containers**, **Labels**, or **Stickers** at the top of the dialog to refine what will be removed.
   - Click **Deconstruct** (or press **Enter**) to demolish the selected items and receive the full refund.
   - Click **Cancel**, press **Escape**, or **Right-Click** to close the confirmation popup (or cancel an active drag box) without deleting anything.
4. Customize default behavior in **Options > Mods > Better Deconstruct** or in `BepInEx/config/urakka.kingdominc.betterdeconstruct.cfg`.

## Requirements

- Kingdom Inc (Windows x64 Mono)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6) (5.4.23.6 or newer)
- [Foundation](https://www.nexusmods.com/kingdominc/mods/18) (0.11.0 or newer)

## Installation

1. Install BepInEx 5 for Kingdom Inc.
2. Install Foundation (0.11.0 or newer).
3. Extract the release archive into your Kingdom Inc game directory.
4. Verify that `BepInEx/plugins/BetterDeconstruct/BetterDeconstruct.dll` and `BepInEx/plugins/BetterDeconstruct/mod.json` are present.
5. Launch the game and enter Deconstruct mode in your workshop.

## Compatibility

| Class.Method | Hook Type | Impact |
|---|---|---|
| DeconstructOverlay.Update | Harmony Prefix | Intercepts drag-box selection and confirmation modal states in Deconstruct mode while leaving normal single-click deconstruction untouched. |
| GameUI.ProcessCancel | Harmony Prefix | Cancels an active deconstruct selection box drag or closes the confirmation dialog on right-click before exiting Deconstruct mode. |
| GameUI.ProcessEscape | Harmony Prefix | Cancels an active deconstruct selection box drag or closes the confirmation dialog on Escape before opening the pause menu. |
| GameUI.ToggleDeconstructMode | Harmony Postfix | Resets any active selection box drag and closes the confirmation dialog when Deconstruct mode is turned off. |

## Removal

Close the game and delete `BepInEx/plugins/BetterDeconstruct/`. Better Deconstruct does not alter save file formats, so existing kingdoms remain 100% compatible after removal.

## Credits and Source

Created by **Urakka** (rockyamyx). [Source code on GitHub](https://github.com/urakkaamyx/KingdomInc_BetterDeconstruct). Game artwork belongs to its respective owners.

