# Stonewards Daily Quest Tracker

Lightweight client-side **Stonewards** BepInEx 5 mod that displays the **five currently selected daily quests and their live progress** during a match.

## v0.2.2 — compact text-only HUD

The panel now shows one quest per line with its numeric `current/target` progress on the right. **No progress bars.** Completed objectives are highlighted in green, titles are width-fitted to avoid counter overlap, and the compact panel retains the in-game burgundy/gold design. It is roughly half as tall as the old five-quest layout.

Quest progress comes directly from `QuestManager.CurrentDailyQuests` paired with `QuestManager.runtimeMissions` (`missionType = DAILY`), not from a quest database or inferred gameplay events. Regular updates only inspect these small live collections; they do not perform scene-wide scans.

### Controls

- `F6`: show/hide the panel
- `F9`: export a diagnostic report manually (not necessary for normal gameplay)

### Appearance and configuration

Settings are stored in `BepInEx/config/community.stonewards.dailyquesttracker.cfg`.

- `Appearance.Anchor`: `TopRight`, `TopLeft`, `BottomRight` or `BottomLeft`
- `Appearance.HorizontalOffset` / `Appearance.VerticalOffset`: offset in actual screen pixels
- `Appearance.Scale` (default `1.0`): manual size multiplier
- `Appearance.ReadabilityScale` (default `1.12`): additional size multiplier
- `Appearance.AutoScaleToResolution` (default `true`): use PartyHUD-style proportional scaling relative to 1920×1080, fitting to screen bounds
- `General.Enabled`, `General.HideCompleted`, `General.RefreshSeconds`, `General.ShowMissingQuestData`

The previous `Appearance.ShowProgressBars` entry is **no longer used**. Existing config files can retain that obsolete key without affecting the text-only layout. Manual scale, anchor and offset values from earlier versions are preserved.

### Build and install

Requirements: Stonewards 0.2.3, BepInEx 5 via r2modman, and .NET SDK. Run PowerShell from the cloned repository:

```powershell
git pull

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir "F:\Steam\steamapps\common\Stonewards" `
  -ProfileDir "C:\Users\user\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\stonewards-daily-quest-tracker"
```

The named profile must already exist. The script builds against installed game assemblies and installs the resulting DLL to that profile; no game assets or proprietary DLLs are included in this repository.

### Sources

- `DailyQuestTrackerPlugin.cs`: BepInEx lifecycle, hotkeys, configuration and visibility
- `DailyQuestReader.cs`: direct read-only daily mission data access
- `QuestHud.cs`: compact resolution-aware text overlay
- `QuestDiagnostics.cs`: optional one-shot quest API report

The mod reads game state but does not modify quests, saves or networking. **v0.2.2 still requires a build and in-game visual check.**
