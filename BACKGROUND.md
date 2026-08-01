# Background

How the wolf howl bug works, and what the mod does about it.

## The bug

A predator decides to give up a chase using a timer: **how long have I been too far from my target?** Once that passes 30 seconds it gives up — and for a wolf, that means a howl. This is intended behaviour.

Two things are wrong with that timer.

**It counts wrong.** Each tick it should add the elapsed frame time, about 0.017 seconds. It adds a large clock value instead, so a single frame takes it past the threshold. An animal stalking you from beyond its pursuit range gives up instantly, and a wolf howls. It notices you again a second later and repeats — a six-second howl re-triggered roughly once per second, so it never finishes.

**It is never reset.** The game does not clear the timer when a new pursuit begins; the only reset happens when the animal gets back *inside* its pursuit range. An animal that has been too far for 30 seconds and then wanders off keeps a hot timer indefinitely, and every stalk it starts afterwards dies on its first frame.

Measured on an unmodified wolf during ordinary play, that timer had reached **107,265** against a threshold of 30.

## Why it looks like an aurora-night problem

The bug needs an animal to be stalking from beyond its pursuit range. Aurora wolves see 105 m but stop pursuing at 100 m, so they routinely acquire you from just outside it — a narrow ring where the bug fires immediately.

Cougars have it worst: they see 150 m and stop pursuing at 150 m, exactly equal, leaving no margin at all.

## Which animals are affected

Only animals that enter the stalking state reach the broken timer. There is one stalking routine shared by every species, so this comes down to behaviour rather than code paths.

| Animal | Stalks | Affected |
|---|---|---|
| Wolf (normal, aurora, starving, town, fluffy) | yes | **yes** |
| Bear | yes | **yes** |
| Cougar | yes | **yes** |
| Timberwolf | no — uses pack AI | no |
| Moose | no — flees on detection | no |
| Deer, rabbits, ptarmigan | no | no |

**Timberwolves** run different behaviour entirely — hide-and-seek and pack-joining — and never enter the stalking state, so they never reach the timer. Their stalking parameters are left over and unused.

**Moose** have a zero percent chance to stalk and always flee when they notice you, so they never enter the state either.

## What the mod does

It repairs the timer, and clears stale ones.

The mod hooks the function that maintains it and checks how much was added. A real frame adds hundredths of a second; the bug adds hundreds. When an impossible jump appears, it is replaced with the correct elapsed time.

It also detects when a new pursuit begins — the timer only updates while an animal is actively stalking, so a gap between updates means the previous pursuit ended — and starts that pursuit from zero instead of inheriting the last one's total.

That is the entire fix. Nothing is retuned:

- No detection, smell, hearing or pursuit range is modified
- No AI state, mode or behaviour is overridden
- Nothing is written to your save

The rule then works as designed: an animal that loses you stays interested for 30 seconds, then gives up **once**. For a wolf that means a single howl, playing its full clip.

## Verifying it yourself

Enable verbose logging (see the README) and watch `MelonLoader/Latest.log`:

```
[WolfHowlFix] ready (enabled=True); patched BaseAi.UpdateScent
[WolfHowlFix] scent timer repaired: +23.5 -> +0.017 (now 0.02s of 30s)
[WolfHowlFix] new pursuit: carried-over timer 30.6s discarded
[WolfHowlFix] id=-348410 howl END CLEAN finalNt=0.98 wallClock=5.7s
```

The first line confirms the fix is active. The second shows a bad increment corrected in flight. The third shows a stale timer discarded as a new pursuit begins. The fourth is a howl playing its full 5.7-second clip — `END BUGGED` would mean it restarted.

## Will animals behave differently?

Yes, in the direction the game intended. Previously an animal that noticed you from too far away forgot about you instantly; now it stays interested for the full 30 seconds before giving up.

**Bears and cougars change the most.** A bear that used to lose interest immediately will now pursue you properly. That is the designed behaviour, but it is a noticeable difference if you are used to the broken one.

Nothing is more aggressive, faster, or better at finding you. Animals simply stop forgetting about you instantly.
