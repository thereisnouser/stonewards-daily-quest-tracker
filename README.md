# Stonewards Daily Quest Tracker

Client-side BepInEx 5 mod for Stonewards that displays **the currently selected daily quests and their live progress** during a match.

## Version 0.2.0 — first direct game-data implementation (test build)

Version 0.1.x used broad reflection and accidentally displayed quest templates instead of live objectives. After inspecting an actual Stonewards 0.2.3 diagnostic snapshot, the tracker now uses the exact game state:

- `QuestManager.Instance.CurrentDailyQuests` is the authoritative list of today's five selected `QuestSO` objectives (not the full quest database).
- `QuestManager.runtimeMissions` contains the active mission objects, including the `DAILY` entries. Each selected quest is paired with its runtime mission by `QuestSO` identity or ID.
- Each `RuntimeMission` provides the live `currentValue` and `target` counters, plus `Description`, `Title` and completion state. `QuestManager.IsQuestCompleted` is also consulted.
- The panel reads only these small lists at the configured refresh interval. No `FindObjectsOfTypeAll` or full type/member graph traversal occurs during normal gameplay.

This mapping comes from the game's diagnostic information rather than guessed field names. **The v0.2.0 build still needs compilation and in-game validation**, especially live counter updates and game localization.

The compact panel uses the Stonewards / PartyHUD burgundy-and-gold visual language and displays up to five selected quests, progress bars, completion indicators, and live counters.

## Installation

Requires Stonewards 0.2.3, BepInEx 5, r2modman and a .NET SDK. The build script compiles against the copies of the game's assemblies installed locally; game assets or proprietary DLLs are not bundled.

Open PowerShell in the cloned repository:

```powershell
git pull

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir "F:\Steam\steamapps\common\Stonewards" `
  -ProfileDir "C:\Users\user\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\stonewards-daily-quest-tracker"
```

The profile must already exist. Run the game from the same profile after building.

## Controls and configuration

- **F6** toggles the tracker panel.
- **F9** manually exports quest diagnostics (only when troubleshooting). The fallback IMGUI shortcut is supported.
- `Appearance.Anchor`, `HorizontalOffset`, `VerticalOffset`, `Scale`, and `ShowProgressBars` control the panel appearance.
- `General.Enabled`, `HideCompleted`, `RefreshSeconds`, and `ShowMissingQuestData` control display and refresh.

Configuration is saved to `BepInEx/config/community.stonewards.dailyquesttracker.cfg`. Existing settings from older versions persist; check `Appearance.VerticalOffset` if the panel is not in your preferred location.

**Automatic diagnostic capture on opening Missions has been removed** in v0.2.0 to avoid costly one-shot scans during ordinary gameplay. The previous `Diagnostics.AutoCaptureWhenMissionsOpened` setting is ignored and can be removed from old config files. F9 remains an explicit, optional debugging action; its report is saved to `BepInEx/config/StonewardsDailyQuestTracker-diagnostic.txt`.

The overlay is read-only: it sends no network messages, alters no quest state or save files, and does not intercept the mouse. It hides automatically in normal game menus and when no gameplay session is active.

## Source layout

- `src/StonewardsDailyQuestTracker/DailyQuestTrackerPlugin.cs`: plugin lifecycle, configurable HUD and hotkeys
- `src/StonewardsDailyQuestTracker/DailyQuestReader.cs`: precise read-only mapping to today's live daily mission state
- `src/StonewardsDailyQuestTracker/QuestHud.cs`: standalone PartyHUD-inspired overlay
- `src/StonewardsDailyQuestTracker/QuestDiagnostics.cs`: optional one-shot API/UI report, used only with F9
- `build-and-install.ps1`: .NET build and r2modman installation script

No copyrighted game textures, fonts or DLLs are included.
