# Stonewards Daily Quest Tracker

A lightweight, independent BepInEx 5 mod that aims to display the **current game's daily quest objectives** inside a match, along with their actual completion state and `current / required` progress.

## Status: 0.1.0 — experimental / API discovery

The HUD, settings, source-code integration and diagnostic collector are implemented. The game quest structures **have not been verified against the Stonewards 0.2.3 Assembly-CSharp.dll**, so displaying and tracking particular quests is **not yet guaranteed**. The reader uses guarded reflection to discover daily quest collections and to pull their counters directly. It will not invent progress from gameplay events. If discovery fails, it shows nothing by default and writes a warning to BepInEx's log.

**What we need for a confirmed integration:** run a session, press **F9**, and share `BepInEx/config/StonewardsDailyQuestTracker-diagnostic.txt`. With this report we can replace the generic adapter with exact game field/property access and verify progress semantics. The report contains quest API member names and short quest-state values; inspect it before sharing.

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
- `F9` — export quest-state diagnostics (also works in the lobby).

### Config

Settings saved by BepInEx to `BepInEx/config/community.stonewards.dailyquesttracker.cfg`:

- `Appearance.Anchor`: `TopRight`, `TopLeft`, `BottomRight`, `BottomLeft`
- `Appearance.HorizontalOffset`: 18 (physical screen pixels)
- `Appearance.VerticalOffset`: 335 (physical screen pixels; clear of the gold display and Bell Control HUD)
- `Appearance.Scale`: 0.65–1.5 (adaptive to display resolution)
- `Appearance.ShowProgressBars`: true
- `General.Enabled`, `General.HideCompleted`, `General.RefreshSeconds`, `General.ShowDiscoveryStatus`
- `Hotkeys.TogglePanel`, `Hotkeys.DumpQuestDiagnostics`

The overlay is read-only. It intentionally disappears in in-game menus, loading screens, upgrades and inventory, and when no Mirror gameplay session is active. It never intercepts mouse input or sends Mirror/RPC calls.

## Structure

- `DailyQuestTrackerPlugin.cs`: BepInEx lifecycle, hotkeys, UI visibility and configuration
- `DailyQuestReader.cs`: read-only quest reflection and API diagnostics
- `QuestHud.cs`: Stonewards/PartyHUD-inspired IMGUI panel
- `build-and-install.ps1`: build with local Unity/BepInEx refs, then copy to test profile

No copyrighted game assets are bundled. The HUD searches for the installed game's Jersey10 font and falls back to Unity's built-in UI font.
