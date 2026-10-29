# Stonewards Daily Quest Tracker

**See your daily quests and live progress during a run without constantly opening the Missions menu.** Daily Quest Tracker adds a compact, game-styled HUD that follows your active objectives.

## ✨ Features

- **Pin standard missions** — select objectives from General, Characters, Tomes, or Rings and see them under daily quests.
- **Live progress** — daily quest descriptions and current/target counters taken from the game.
- **Correct quest totals** — remaining objectives are counted against the full daily total, even after completing quests.
- **Completed quest options** — automatically hide finished quests or keep them with green checkmarks.
- **Convenient placement** — follow the coin display when detected, or choose a corner and offsets manually.
- **Adaptive display** — adjustable size, resolution scaling, and automatic hiding in supported game menus.
- **Native notifications** — show game notifications above the tracker when native UI rendering is available.

## 📌 Pin other missions

Open **MISSIONS** and use the **PIN** buttons in the **General**, **Characters**, **Tomes**, and **Rings** tabs. Pinned objectives appear under daily quests in a separate **PINNED QUESTS** section with live numeric progress.

Press **ON** to unpin a mission. Up to **5 missions** can be pinned, and the selection persists after restarting Stonewards. Completed pinned missions are marked green until unpinned.

## 🎮 How to use

Join a session to see your daily objectives automatically. Press **F6** to show or hide the tracker.

In the default **Hide** mode, completed quests disappear and the remaining count keeps its original total. When all daily objectives are finished, a short completion message appears. Use **Mark** mode if you want completed quests to remain visible.

**F9** exports a diagnostic report for troubleshooting only. Both keys are configurable.

## ⚙️ Configuration

Edit the BepInEx configuration in your active profile:

`BepInEx/config/community.stonewards.dailyquesttracker.cfg`

Important settings:

| Setting | Default | Purpose |
| --- | --- | --- |
| `PinnedMissions.QuestIds` | *(empty)* | Saved mission pins; managed from the Missions screen |
| `PinnedMissions.QuestTitles` | *(empty)* | Saved localized mission names so titles survive game restarts |\n| `General.CompletedQuestMode` | `Hide` | Hide finished quests or use `Mark` for checkmarks |
| `Appearance.AnchorToCurrencyHud` | `true` | Place the panel below the coin HUD when detected |
| `Appearance.RenderBehindGameUI` | `true` | Prefer native rendering beneath game notifications |
| `Appearance.CurrencyGap` | `10` | Spacing below the coins, in pixels |
| `Appearance.Anchor` | `TopRight` | Manual placement corner |
| `Appearance.HorizontalOffset` | `18` | Manual horizontal inset |
| `Appearance.VerticalOffset` | `240` | Manual vertical inset |
| `Appearance.AutoScaleToResolution` | `true` | Adapt the panel to screen resolution |
| `Appearance.Scale` | `1.0` | Panel size multiplier |
| `Appearance.ReadabilityScale` | `1.12` | Extra readability scaling |

To show completed quests with checkmarks:

```ini
[General]
CompletedQuestMode = Mark
```

## 📦 Installation

Install **DailyQuestTracker** using **r2modman** or another compatible Thunderstore mod manager. The required BepInExPack dependency is installed automatically by the manager. Launch Stonewards through your modded profile.

## 🔧 Compatibility

- **Game:** Stonewards; developed against version **0.2.3**.
- **Mod loader:** BepInEx 5.
- **Multiplayer:** Client-side; other players and the host do not need to install the mod.

## ⚠️ Known limitations

If the currency HUD cannot be detected, the tracker uses your configured corner and offsets. Notification layering relies on the game's native UI; legacy fallback rendering may appear above some notifications. Future game UI or quest-system updates may require compatibility changes.

## 🔗 Links

[GitHub](https://github.com/thereisnouser/stonewards-daily-quest-tracker) · [Report an issue](https://github.com/thereisnouser/stonewards-daily-quest-tracker/issues) · [Changelog](https://github.com/thereisnouser/stonewards-daily-quest-tracker/blob/main/thunderstore/CHANGELOG.md)

---

*Made by thereisnouser*
