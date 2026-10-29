# Changelog

## 0.2.0

- Added mission pinning from the **General**, **Characters**, **Tomes**, and **Rings** tabs in **MISSIONS**.
- Added a separate **PINNED QUESTS** HUD section for up to five standard missions, with numeric progress when available and a completed state.
- Added the original gold pixel-art pushpin with a compact stepped frame, native-size alignment, and selected/hover states.
- Saved pinned mission IDs and localized mission names across restarts; fixed completed objectives showing reward names such as `TOGGLER`.
- Kept native game notifications above the HUD, daily quest tracking, and legacy rendering fallback.

## 0.1.3

- Updated GitHub and Thunderstore READMEs to the shared thereisnouser description standard.
- Clarified live quest tracking, original daily totals, completed-quest display modes, UI layering, controls, and configuration.
- No gameplay or configuration changes compared with 0.1.2.

## 0.1.2

- Game notifications appear above the daily quest panel, including quest completion and player join messages.
- Restored the original stepped gold frame and burgundy header using native UI Toolkit rendering.
- Fixed the remaining-quest counter so completed objectives do not shrink the total.
- Added `Appearance.RenderBehindGameUI` (enabled by default), with a legacy renderer fallback.
- Retained coin anchoring, adaptive scaling, and `Hide`/`Mark` completion modes.

## 0.1.1

- Removed the oversized duplicate preview image from the Thunderstore description.
- Kept the package icon and in-game behavior unchanged.

## 0.1.0 — Initial public release

- Show live daily objectives, numeric progress, and remaining totals during a run.
- Hide completed daily quests automatically or mark them with checkmarks.
- Position the HUD beneath the coin counter when available, with configurable placement and scaling.
- Add configurable display controls, hotkeys, and a read-only, client-side HUD.
