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

## Animals behave as intended

Previously an animal that noticed you from too far away forgot about you instantly — that was the bug. Now it stays interested for the full 30 seconds the game intends, then gives up once.

The broken timer is shared by all predators, so **bears, moose and cougars are fixed too**. They do not howl, so it showed up as an animal repeatedly starting and abandoning a chase rather than as a stuttering howl.

⚠️ **Bears change the most.** A bear that used to lose interest instantly will now pursue you for the full 30 seconds. That is the intended behaviour, but it is a noticeable difference.

Nothing is more aggressive, faster, or better at finding you. No senses, ranges or behaviour are retuned; only the broken timer is repaired.

## Works with other mods

The fix corrects one miscalculated value inside the game's own logic and changes no tuning fields, so it does not conflict with mods that adjust wolf senses or behaviour — including **MajorMiseries**.

---

Curious how it works? See [BACKGROUND.md](BACKGROUND.md).

MIT — see [LICENSE](LICENSE).
