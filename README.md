# Stonewards Daily Quest Tracker

**Keep daily quests and pinned missions visible while playing Stonewards. Follow live progress in a compact, game-styled HUD without repeatedly opening the Missions menu.**

## ✨ Features

- **Live daily quests** — see active objectives and numeric progress, with an accurate remaining count that keeps the original daily total.
- **Pinned missions** — track up to five objectives from **General**, **Characters**, **Tomes**, and **Rings** in a separate section below daily quests.
- **Persistent pins** — mission selections and their in-game display names are remembered between launches, including completed objectives.
- **Completion options** — hide finished daily quests or keep them with green checkmarks; completed pinned missions stay visible until unpinned.
- **Stonewards-style HUD** — compact text rows, stepped gold borders, pixel-art pins, responsive sizing, and optional positioning below the coin counter.
- **Notification-friendly display** — the native UI renderer keeps game notifications above the tracker when available.

## 🎮 How to use

Join a session to see your daily quests and their progress automatically. Open **MISSIONS** and select **General**, **Characters**, **Tomes**, or **Rings** to pin other missions. Click the **gold pushpin icon** beside a mission to track it; click the same icon again to unpin it. A highlighted frame marks pinned missions.

Your selected missions appear under **PINNED QUESTS**, beneath **DAILY QUESTS**. You can track up to **five** standard missions at once. Completed pinned missions remain listed until you remove them.

Press **F6** to show or hide the HUD. **F9** exports a quest-system diagnostic report for troubleshooting; it is not needed for normal play. Both keys are configurable.

By default, completed daily quests are hidden. Change **CompletedQuestMode** to **Mark** to keep them visible with green checkmarks. When all daily quests are complete in **Hide** mode, a short completion message remains.

## ⚙️ Configuration

Settings are generated after the first launch in the active mod-manager profile:

`BepInEx/config/community.stonewards.dailyquesttracker.cfg`

| Setting | Default | Purpose |
| --- | --- | --- |
| `General.Enabled` | `true` | Enable or disable the tracker |
| `General.CompletedQuestMode` | `Hide` | `Hide` finished daily quests or `Mark` them with checkmarks |
| `General.RefreshSeconds` | `1.25` | Interval for reading quest progress, in seconds |
| `Appearance.AnchorToCurrencyHud` | `true` | Follow the coin HUD when it can be detected |
| `Appearance.CurrencyGap` | `10` | Space below the coin HUD, in screen pixels |
| `Appearance.Anchor` | `TopRight` | Screen corner used for manual positioning |
| `Appearance.HorizontalOffset` | `18` | Horizontal inset in screen pixels |
| `Appearance.VerticalOffset` | `240` | Vertical inset in screen pixels |
| `Appearance.AutoScaleToResolution` | `true` | Scale the overlay relative to screen resolution |
| `Appearance.Scale` | `1.0` | Manual panel size multiplier |
| `Appearance.ReadabilityScale` | `1.12` | Additional size multiplier for readability |
| `Appearance.RenderBehindGameUI` | `true` | Use the native UI layer so notifications appear above the panel |
| `Hotkeys.TogglePanel` | `F6` | Toggle HUD visibility |
| `Hotkeys.DumpQuestDiagnostics` | `F9` | Save a troubleshooting report |
| `PinnedMissions.QuestIds` | *(empty)* | Automatically maintained list of pinned mission IDs |
| `PinnedMissions.QuestTitles` | *(empty)* | Automatically saved display names for pinned missions |

Pin settings are managed through the in-game **MISSIONS** menu; you do not need to edit them manually. For example, to retain finished daily quests:

```ini
[General]
CompletedQuestMode = Mark
```

An optional F9 diagnostic file is written to `BepInEx/config/StonewardsDailyQuestTracker-diagnostic.txt`.

## 📦 Installation

### Mod manager

Install [DailyQuestTracker on Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/DailyQuestTracker/) with **r2modman** or another compatible Thunderstore mod manager. Its declared BepInExPack dependency is installed by the manager. Launch Stonewards through your chosen profile.

### Build from source

To install a local build, use **.NET SDK 8 or newer**, a Stonewards installation, and a BepInEx 5 profile. Close Stonewards before building and installing. From the repository root:

```powershell
$gameDir = 'C:\path\to\Stonewards'
$profileDir = Join-Path $env:APPDATA 'r2modmanPlus-local\Stonewards\profiles\stonewards-daily-quest-tracker'

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

Change the example paths to match your installation. Disable the Thunderstore copy of DailyQuestTracker in that test profile to avoid loading two copies of the plugin.

## 🔧 Compatibility

- **Game:** Tested with **Stonewards 0.2.3**.
- **Mod loader:** **BepInEx 5** (package dependency: `BepInEx-BepInExPack-5.4.2305`).
- **Multiplayer:** **Client-side** — only the player who wants the overlay needs to install it; the host and other players do not.

## ⚠️ Known limitations

- Some completed pinned missions no longer have an active runtime counter. They may display **DONE** instead of a numeric value.
- Pins saved by older versions may need **MISSIONS** to be opened once after updating, so their original display names can be recovered and stored.
- If the coin HUD or native notification layer cannot be detected, the tracker uses its configured placement or legacy renderer. Notification order can differ in legacy mode.
- Future Stonewards changes to quest data or UI elements may require a compatibility update.

## 🛠️ Development

The plugin targets **.NET Standard 2.1**. It reads quest objectives and progress from the game's `QuestManager` runtime data. Pin buttons are added to the existing **MISSIONS** UI, and selected mission IDs and display names are stored in the local BepInEx configuration. It does not change mission completion, rewards, or unlocks.

The HUD uses **Unity UI Toolkit** when available, with an OnGUI fallback. Its pin sprite is embedded in the compiled DLL. To build a distributable Thunderstore ZIP using the same `$gameDir` and `$profileDir` as above:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-thunderstore.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

The archive is written to `dist/`. The packaging script uses **`thunderstore/README.md`**, not this GitHub README, and verifies the expected archive files. Uploading the ZIP to Thunderstore is a separate step.

## 🔗 Links

[Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/DailyQuestTracker/) · [Report an issue](https://github.com/thereisnouser/stonewards-daily-quest-tracker/issues) · [Changelog](thunderstore/CHANGELOG.md)

---

*Made by thereisnouser*
