# Stonewards Daily Quest Tracker

**Keep your daily objectives and their live progress visible during a run.** Daily Quest Tracker adds a compact, game-styled HUD, so you can follow your missions without repeatedly opening the Missions menu.

## ✨ Features

- **Live daily progress** — show the game's active daily objectives with their current/target counters.
- **Accurate remaining count** — track unfinished quests against the original daily total, even after an objective is completed or claimed.
- **Completed quest options** — hide finished objectives or keep them at the bottom with green checkmarks.
- **Coin-aware placement** — position the panel below the currency HUD when detected, with manual corner and offset controls as a fallback.
- **Game-friendly display** — compact, text-focused rows with a Stonewards-style frame, adaptive scaling, and automatic hiding in supported menus.
- **Notification layering** — render the tracker beneath native game notifications when the native UI layer is available.

## 🎮 How to use

Join a Stonewards session and your current daily quests appear automatically. Progress updates from the game's own quest data; you do not need to open a separate menu.

| Default key | Action |
| --- | --- |
| `F6` | Show or hide the quest panel |
| `F9` | Export a quest-system diagnostic report (troubleshooting only) |

Both keys can be changed in the BepInEx configuration. `F9` is not needed for normal gameplay.

By default, completed objectives disappear from the panel. Set `CompletedQuestMode` to `Mark` if you prefer completed quests to remain visible with checkmarks. When every daily quest is finished, the default `Hide` mode shows a short completion message.

## ⚙️ Configuration

Settings are stored in your active mod-manager profile at:

`BepInEx/config/community.stonewards.dailyquesttracker.cfg`

The file is generated after the mod has launched. The principal options are:

| Setting | Default | Description |
| --- | --- | --- |
| `General.Enabled` | `true` | Enable the tracker |
| `General.CompletedQuestMode` | `Hide` | `Hide` completed quests or `Mark` them |
| `General.ShowMissingQuestData` | `true` | Show a small notice when daily quest data is unavailable |
| `General.RefreshSeconds` | `1.25` | Seconds between quest-data reads |
| `Appearance.AnchorToCurrencyHud` | `true` | Follow the coin HUD when detected and using the top-right anchor |
| `Appearance.RenderBehindGameUI` | `true` | Prefer the native layer below game notifications; otherwise use the legacy renderer |
| `Appearance.CurrencyGap` | `10` | Gap below the coin HUD, in screen pixels |
| `Appearance.Anchor` | `TopRight` | Corner used for manual placement |
| `Appearance.HorizontalOffset` | `18` | Horizontal inset in pixels |
| `Appearance.VerticalOffset` | `240` | Vertical inset in pixels |
| `Appearance.AutoScaleToResolution` | `true` | Adapt the HUD to screen resolution |
| `Appearance.Scale` | `1.0` | Panel size multiplier |
| `Appearance.ReadabilityScale` | `1.12` | Additional readability scaling |
| `Hotkeys.TogglePanel` | `F6` | Panel visibility shortcut |
| `Hotkeys.DumpQuestDiagnostics` | `F9` | Diagnostic export shortcut |

For example, to keep completed quests visible, use:

```ini
[General]
CompletedQuestMode = Mark
```

The optional F9 report is written to `BepInEx/config/StonewardsDailyQuestTracker-diagnostic.txt` within the active installation/profile.

## 📦 Installation

### Mod manager

Install [DailyQuestTracker on Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/DailyQuestTracker/) with **r2modman** or another Thunderstore-compatible mod manager. The declared **BepInExPack** dependency is installed by the manager. Launch Stonewards through the selected modded profile.

Only players who want the quest overlay need to install it.

### Build from source

Clone this repository, install the .NET SDK 8 or newer, and make sure Stonewards and a BepInEx 5 mod-manager profile are available. Close the game before installing a rebuilt DLL.

```powershell
git clone https://github.com/thereisnouser/stonewards-daily-quest-tracker.git
cd stonewards-daily-quest-tracker

$gameDir = "C:\path\to\Stonewards"
$profileDir = Join-Path $env:APPDATA "r2modmanPlus-local\Stonewards\profiles\stonewards-daily-quest-tracker"

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

Replace the example game directory with your installation path. Use your actual r2modman profile folder if it differs, and disable the published DailyQuestTracker package in your test profile to avoid loading the plugin twice.

## 🔧 Compatibility

- **Game:** Stonewards; implemented against the daily quest API in **0.2.3**.
- **Mod loader:** BepInEx 5 (Thunderstore dependency: `BepInEx-BepInExPack-5.4.2305`).
- **Multiplayer:** Client-side; other players, including the host, do not need the mod installed for your overlay to work.

## ⚠️ Known limitations

- Coin anchoring depends on finding the game's currency HUD. If it cannot be detected, the configured corner and offsets are used.
- Rendering below game notifications depends on finding the supported native UI layer. The legacy fallback may not maintain the same notification order.
- Changes to Stonewards' quest or UI internals in later game updates may require a mod update.

## 🛠️ Development

The plugin targets **.NET Standard 2.1** and reads the selected daily quests from `QuestManager.CurrentDailyQuests`, matching them with live `QuestManager.runtimeMissions` values. It does not modify quest completion or game save data, or send gameplay network messages.

Its HUD uses a native UI Toolkit layer when available and falls back to OnGUI rendering. The bounded currency HUD lookup supplies positioning, while the quest reader maintains the original daily-quest count independently from the shrinking list of active objectives.

To build a Thunderstore archive from the repository's existing script:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-thunderstore.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

The script packages `thunderstore/README.md`, `thunderstore/CHANGELOG.md`, the manifest, icon, and compiled DLL into `dist/`. Publishing to Thunderstore is a separate step.

## 🔗 Links

[Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/DailyQuestTracker/) · [Report an issue](https://github.com/thereisnouser/stonewards-daily-quest-tracker/issues) · [Changelog](thunderstore/CHANGELOG.md)

---

*Made by thereisnouser*
