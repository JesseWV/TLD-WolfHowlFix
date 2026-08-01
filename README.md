# Wolf Howl Fix

Fixes the wolf howl loop bug. Under certain conditions, most noticeable on aurora nights, a wolf loops and stutters its howl endlessly instead of finishing it.

The cause is a broken timer in the game's "give up the chase" logic. This mod repairs it — nothing is retuned.

![A wolf's howl restarting over and over instead of finishing](BuggedHowl.gif)

## Requirements

- The Long Dark **2.55**
- **MelonLoader 0.7.2**
- **ModSettings** — optional, adds the in-game toggle. Without it the fix simply runs enabled.

## Install

Put `WolfHowlFix.dll` in your `Mods/` folder. To uninstall, delete it.

Nothing is written to your save.

## Settings

**Options → Mod Settings → Wolf Howl Fix** (requires ModSettings)

![The mod's settings page](WolfHowlFix-ModSettings.png)

| Setting        | Default |                                              |
| -------------- | ------- | -------------------------------------------- |
| **Enable fix** | on      | Turn off to restore the unpatched behaviour. |

## Wolves behave as intended

Previously a wolf that noticed you from too far away forgot about you instantly — that was the bug. Now it stays interested for the full 30 seconds the game intends, then gives up once.

Wolves are no more aggressive, no faster, and no better at finding you. Nothing about their senses, ranges or behaviour is retuned; only the broken timer is repaired.

## Works with other mods

The fix corrects one miscalculated value inside the game's own logic and changes no tuning fields, so it does not conflict with mods that adjust wolf senses or behaviour — including **MajorMiseries**.

---

Curious how it works? See [BACKGROUND.md](BACKGROUND.md).

MIT — see [LICENSE](LICENSE).
