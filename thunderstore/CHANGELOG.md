# Changelog

## 0.2.0 (testing)

- Replace the placeholder PIN/ON text with a compact gold pixel-art pin icon, using muted, selected and hovered states. Position it away from completion checkmarks and rewards.
- Show up to five pinned missions under daily quests, with live progress and green completion state.
- Persist pinned mission IDs **and their exact localized game titles** between launches; completed missions no longer revert to reward IDs such as TOGGLER after restarting. Old pins recover their names when MISSIONS is opened.
- Persist the title cache in BepInEx configuration without modifying game quest data.
- Preserve game notifications above the HUD and keep native/legacy panel layouts consistent.

## 0.1.3

- Updated GitHub and Thunderstore READMEs to the shared thereisnouser description standard.
- Clarified live quest tracking, the original daily total, completed-quest display modes, UI layering, controls, and configuration.
- No gameplay or configuration changes compared with 0.1.2.

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
