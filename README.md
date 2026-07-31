# Wolf Howl Fix

Fixes wolf howl loop bug. Under certain conditions, most noticeable on aurora nights, a wolf can loop and stutter its howl endlessly.

![A wolf's howl restarting over and over instead of finishing](BuggedHowl.gif)

## Requirements

- The Long Dark **2.55**
- **MelonLoader 0.7.2**
- **ModSettings**

## Install

Put `WolfHowlFix.dll` in your `Mods/` folder. To uninstall, delete it.

Nothing is written to your save.

## Settings

**Options → Mod Settings → Wolf Howl Fix**

![The mod's settings page](WolfHowlFix-ModSettings.png)

| Setting        | Default |                                              |
| -------------- | ------- | -------------------------------------------- |
| **Enable fix** | on      | Turn off to restore the unpatched behaviour. |

## ⚠️ Some wolves will be more dangerous!

All wolves during the aurora, including timberwolves, and all poisoned wolves would detect you but be unable to pursue from certain distances due to the bug.

With the fix they will correctly close on you from distances they previously could not, which is how the game always intended them to behave.

Turn the fix off in Mod Settings if you would rather keep that.

---

Curious how it works? See [BACKGROUND.md](BACKGROUND.md).

MIT — see [LICENSE](LICENSE).
