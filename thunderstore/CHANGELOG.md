# Changelog

## 0.1.2

- **Game notifications now appear above the daily quest panel**, including quest completion and player join messages.
- Restored the original stepped gold frame and burgundy header after moving the tracker to native UI Toolkit rendering.
- Fixed the remaining-quest counter so completed quests don't shrink the total (e.g. `4/5 LEFT` rather than `4/4 LEFT`).
- Added `Appearance.RenderBehindGameUI` (enabled by default), with a legacy renderer fallback.
- Kept coin anchoring, adaptive scaling, and Hide/Mark completed-quest modes.

## 0.1.1

- Fixed the Thunderstore description by removing the oversized duplicate preview image.
- Kept the package icon, live quest tracking and all in-game behavior unchanged.

## 0.1.0 — Initial public release

- Show all current daily missions with their real-time progress directly during a match.
- Display a compact, text-only HUD styled to fit Stonewards.
- Automatically hide completed quests or mark them with green checkmarks.
- Show remaining/total daily quest counts and a completion message.
- Position the panel beneath the coin HUD when detected, with configurable fallback offsets.
- Support adaptive scaling across different screen resolutions.
- Include configurable visibility, size, hotkeys and refresh interval.
- Keep the mod client-side and read-only, without modifying saves or quest progress.
