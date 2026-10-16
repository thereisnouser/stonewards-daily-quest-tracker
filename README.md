# Stonewards Daily Quest Tracker

A lightweight, independent BepInEx 5 mod that aims to display the **current game's daily quest objectives** inside a match, along with their actual completion state and `current / required` progress.

## Status: 0.1.2 — experimental / API discovery

The HUD, settings, source-code integration and diagnostic collector are implemented. The game quest structures **have not been verified against the Stonewards 0.2.3 Assembly-CSharp.dll**, so displaying and tracking particular quests is **not yet guaranteed**. The reader uses guarded reflection to discover daily quest collections and to pull their counters directly. It will not invent progress from gameplay events. If discovery fails, it shows nothing by default and writes a warning to BepInEx's log.

**v0.1.1 hotfix:** the recurring full-scene Quest/Mission scan in v0.1.0 caused periodic frame-time spikes. Now quest source discovery runs once per scene/session, and only confirmed live quest progress sources are polled afterward. Asset databases and template objects (such as DailyQuestDatabaseSO) are not displayed. A quest must have both a current counter and a positive target, otherwise it is withheld until its actual runtime representation is known.

The redesigned HUD shows up to five real quests in a compact Party HUD-inspired panel, and can display "QUEST PROGRESS NOT DETECTED" instead of a misleading list of database templates. **Live quest API mapping is still unconfirmed**; please provide the F9 report to finish the actual progress integration.

**v0.1.2 diagnostic fix:** pressing F9 writes a bounded quest API report with on-screen success/error feedback and an IMGUI hotkey fallback. Because F9 might be swallowed by the game or keyboard function-key handling, **opening Missions -> Daily automatically exports the same diagnostic report after the menu finishes populating** (once per scene). No extra keyboard shortcut is required.

Report path inside the profile: `BepInEx/config/StonewardsDailyQuestTracker-diagnostic.txt`. If missing, check `BepInEx/LogOutput.log` for `Quest diagnostic export failed`. The report may include names or game state values; inspect it before sending it.

**What we need for a confirmed integration:** open **Missions -> Daily** (or press **F9**) and share `BepInEx/config/StonewardsDailyQuestTracker-diagnostic.txt`. With this report we can replace the generic adapter with exact game field/property access and verify progress semantics. The report contains quest API member names and short quest-state values; inspect it before sharing.

## Build and install

Requires .NET SDK (8+ recommended), game version 0.2.3 and a working BepInEx 5 r2modman profile. Keep testing isolated from your primary profile.

From PowerShell in the extracted project folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir "F:\Steam\steamapps\common\Stonewards" `
  -ProfileDir "C:\Users\user\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\sw-test-daily-quests"
```

Create and launch `sw-test-daily-quests` via r2modman first, or substitute an existing **test** profile. A profile isn't created by this script. `-NoInstall` builds without copying the DLL.

## Controls

- `F6` — show/hide the daily quest HUD.
- `F9` — manually export quest-state diagnostics. Opening **Missions -> Daily** also exports automatically.

### Config

Settings saved by BepInEx to `BepInEx/config/community.stonewards.dailyquesttracker.cfg`:

- `Appearance.Anchor`: `TopRight`, `TopLeft`, `BottomRight`, `BottomLeft`
- `Appearance.HorizontalOffset`: 18 (physical screen pixels)
- `Appearance.VerticalOffset`: 240 (physical screen pixels; clear of the gold display and Bell Control HUD)
- `Appearance.Scale`: 0.65–1.5 (adaptive to display resolution)
- `Appearance.ShowProgressBars`: true
- `General.Enabled`, `General.HideCompleted`, `General.RefreshSeconds`, `General.ShowMissingQuestData`
- `Hotkeys.TogglePanel`, `Hotkeys.DumpQuestDiagnostics`, `Diagnostics.AutoCaptureWhenMissionsOpened`

The overlay is read-only. It intentionally disappears in in-game menus, loading screens, upgrades and inventory, and when no Mirror gameplay session is active. It never intercepts mouse input or sends Mirror/RPC calls.

## Structure

- `DailyQuestTrackerPlugin.cs`: BepInEx lifecycle, hotkeys, UI visibility and configuration
- `DailyQuestReader.cs`: read-only quest reflection and cached live sources
- `QuestDiagnostics.cs`: bounded, on-demand/automatic quest API and UI snapshot
- `QuestHud.cs`: Stonewards/PartyHUD-inspired IMGUI panel
- `build-and-install.ps1`: build with local Unity/BepInEx refs, then copy to test profile

No copyrighted game assets are bundled. The HUD searches for the installed game's Jersey10 font and falls back to Unity's built-in UI font.

## Updating from 0.1.0

Existing BepInEx settings persist across plugin updates. If you customized `Appearance.VerticalOffset`, the new default position will not override it. `General.ShowMissingQuestData` is a new setting (true by default), replacing the old `ShowDiscoveryStatus` key. Quest database/template entries are intentionally no longer shown even if the source cannot yet be detected.

