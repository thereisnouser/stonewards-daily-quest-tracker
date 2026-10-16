# Changelog

## 0.1.2 (testing)

- Keep the remaining-quest counter based on the game's total daily quest count, even when completed quests disappear from the active list (e.g. 4/5 LEFT).
- Render the daily quest panel behind the game's native UI Toolkit overlays so notifications can appear on top.
- Add `Appearance.RenderBehindGameUI` (enabled by default) with an option to return to legacy OnGUI rendering.

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
