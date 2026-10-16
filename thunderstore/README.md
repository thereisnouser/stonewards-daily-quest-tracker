# Stonewards Daily Quest Tracker

Keep your **daily quests and their real progress** on screen while you play. No need to keep opening the Missions menu.

## Features

- **Live progress:** text-only objectives with current/target counters read directly from the game.
- **Complete and move on:** choose `Hide` (finished quests disappear automatically) or `Mark` (keep them with a green checkmark).
- **Coin-aware position:** places the panel just below the coin counter when detected, with a manual-position fallback.
- **Screen adaptive:** resolution scaling, panel size and readability options for Full HD, 1440p, 4K and ultrawide.
- **Clean integration:** hides in supported game menus and uses a burgundy-and-gold HUD style.
- **Client-side:** teammates and the host do not need to install it.

## Getting started

Install this package using r2modman or another Thunderstore-compatible mod manager. **BepInExPack** is installed automatically. Start Stonewards through your mod manager and enter a match.

Press **F6** to toggle the quest panel.

## Completed quests

The default behavior is **Hide**, so the panel becomes shorter as you finish objectives.

To leave finished quests visible with green checkmarks instead, edit the BepInEx configuration:

```ini
[General]
CompletedQuestMode = Mark
```

Use `Hide` to switch back. When all quests are done in Hide mode, a small completion message remains.

## Position and scale

The mod attempts to anchor the panel under the game's coin counter by default. Change these BepInEx settings if you prefer a manual position:

| Setting | Default | Description |
| --- | --- | --- |
| `Appearance.AnchorToCurrencyHud` | `true` | Follow the coin counter when detectable |
| `Appearance.CurrencyGap` | `10` | Gap below the counter, in screen pixels |
| `Appearance.Anchor` | `TopRight` | Manual corner if coin anchoring is disabled |
| `Appearance.HorizontalOffset` | `18` | Manual horizontal inset |
| `Appearance.VerticalOffset` | `240` | Manual vertical inset |
| `Appearance.AutoScaleToResolution` | `true` | Adapt size to resolution |
| `Appearance.Scale` | `1.0` | Panel size multiplier |
| `Appearance.ReadabilityScale` | `1.12` | Extra size/readability |

Config file: `BepInEx/config/community.stonewards.dailyquesttracker.cfg` in your active profile.

**F9** writes an optional quest API diagnostic report if you are troubleshooting. It is not needed during regular play.

## Compatibility

Designed for **Stonewards 0.2.3**, using **BepInEx 5**. The plugin is read-only and does not change daily quest completion, player saves or networking. Quest or UI changes in future Stonewards updates may need a mod update.

## Links

- [Source code and developer instructions](https://github.com/thereisnouser/stonewards-daily-quest-tracker)
- [Report an issue or suggest a feature](https://github.com/thereisnouser/stonewards-daily-quest-tracker/issues)
