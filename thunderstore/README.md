# Stonewards Daily Quest Tracker

**Keep daily quests and pinned missions visible while playing Stonewards. Follow live progress in a compact, game-styled HUD without repeatedly opening the Missions menu.**

## ✨ Features

- **Live daily progress** — see current objectives, numeric progress, and the correct remaining daily count.
- **Pin other missions** — use a gold pushpin icon to track up to five missions from **General**, **Characters**, **Tomes**, or **Rings**.
- **Persistent selections** — pinned missions and their original names stay saved between launches.
- **Completion options** — hide completed daily quests or mark them with checkmarks; finished pinned missions remain until unpinned.
- **Compact game-style HUD** — pixel-art details, adaptive scaling, and positioning below the coin counter when available.
- **Game notifications on top** — native UI rendering allows quest and player notifications to overlap the tracker.

## 🎮 How to use

Join a game to see your daily quests automatically. Open **MISSIONS** and select a standard mission tab (**General**, **Characters**, **Tomes**, or **Rings**). Click the **gold pushpin icon** on a row to add that mission to **PINNED QUESTS** below the daily list. Click it again to remove the pin. You can keep up to **five** pinned missions.

Press **F6** to toggle the tracker. The optional **F9** key saves a diagnostic report. Both keys are configurable. Daily quests use the **Hide** completion mode by default; switch to **Mark** to keep finished daily objectives visible.

## ⚙️ Configuration

Settings are stored in the active profile at `BepInEx/config/community.stonewards.dailyquesttracker.cfg`:

| Setting | Default | Purpose |
| --- | --- | --- |
| `General.CompletedQuestMode` | `Hide` | Hide completed daily quests or use `Mark` for checkmarks |
| `Appearance.AnchorToCurrencyHud` | `true` | Follow the game's coin HUD when detected |
| `Appearance.RenderBehindGameUI` | `true` | Prefer the native renderer beneath game notifications |
| `Appearance.Scale` | `1.0` | Change the overlay size |
| `Appearance.AutoScaleToResolution` | `true` | Adapt overlay size to screen resolution |
| `Hotkeys.TogglePanel` | `F6` | Show or hide the tracker |

Your pins and their display names are stored automatically under `PinnedMissions.QuestIds` and `PinnedMissions.QuestTitles`; no manual setup is needed.

## 📦 Installation

Install **DailyQuestTracker** with **r2modman** or another compatible Thunderstore mod manager. The declared **BepInExPack** dependency is installed by the manager. Launch Stonewards through the modded profile.

## 🔧 Compatibility

- **Game:** Tested with **Stonewards 0.2.3**.
- **Mod loader:** **BepInEx 5**.
- **Multiplayer:** **Client-side** — only players who want the HUD need the mod; no host installation is required.

## ⚠️ Known limitations

- Completed pinned missions may display **DONE** instead of numeric progress if the game no longer provides an active counter.
- After updating from an older version, open **MISSIONS** once if a previously pinned mission's name has not yet been restored.
- Coin anchoring and notification layering depend on game UI elements being available; the mod falls back to manual positioning or legacy rendering when necessary.

## 🔗 Links

[GitHub](https://github.com/thereisnouser/stonewards-daily-quest-tracker) · [Report an issue](https://github.com/thereisnouser/stonewards-daily-quest-tracker/issues) · [Changelog](https://github.com/thereisnouser/stonewards-daily-quest-tracker/blob/main/thunderstore/CHANGELOG.md)

---

*Made by thereisnouser*
