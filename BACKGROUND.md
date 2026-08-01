# Background

How the wolf howl bug works, and what the mod does about it.

## The bug

A wolf howls when it gives up stalking something. That is intended behaviour — the "ah, forget it" howl.

The game decides *when* to give up with a timer: **how long have I been too far from my target?** Once that passes 30 seconds, give up and howl.

The timer is broken. Each tick it should add the elapsed frame time — about 0.017 seconds. Instead it adds a large clock value, so a **single frame** takes it far past the 30-second threshold.

So the moment a wolf is stalking you from beyond its pursuit range, it gives up instantly, howls, and drops back to wandering. A second later it notices you again and repeats. The howl is re-triggered roughly **once per second against a six-second howl**, so it never finishes — that stuttering, looping howl.

Measured on an unmodified wolf during ordinary play, that timer had reached **107,265** against a threshold of 30.

## Why it seems to happen mostly on aurora nights

Aurora wolves can see further (105 m) than the distance at which they give up pursuit (100 m). That small gap means they routinely acquire you from just outside their pursuit range, which is exactly the condition that triggers it. Other wolves reach that state less often, but nothing about the defect is specific to them — any wolf stalking from too far away hits it, including one that can't reach you because a ravine or shoreline is in the way.

## A second problem: the timer is never reset

The game never clears that timer when an animal starts a new pursuit — `EnterStalking` and
`ExitStalking` do not touch it at all. The only reset happens when the animal gets back *inside*
its pursuit range.

So an animal that has been too far for 30 seconds and then wanders off keeps a hot timer
indefinitely, and every stalk it begins afterwards dies on its first tick. This is why bears are
sometimes seen repeatedly starting and abandoning a chase in the unmodded game.

## What the mod does

It corrects the timer, and clears stale ones.

The mod hooks the function that maintains it and checks how much was added. A real frame adds hundredths of a second; the bug adds hundreds. When an impossible jump appears, the mod replaces it with the correct elapsed time and lets the game continue.

It also detects when a new pursuit begins — the timer is only updated while an animal is
actively stalking, so a gap means the previous pursuit ended — and starts that pursuit from zero
rather than inheriting the last one's total.

That is the entire fix. Nothing is retuned:

- No detection, smell, hearing or pursuit range is modified
- No AI state, mode or behaviour is overridden
- Nothing is written to your save

The give-up rule then works the way it was designed to: a wolf that loses you stays interested for 30 seconds, then gives up **once** and howls **once** — the sound the game always meant to make.

Because it repairs the underlying timer rather than avoiding the conditions that expose it, it covers every wolf variant, including timberwolves and wolves that cannot close the distance.

## Verifying it yourself

Enable verbose logging (see the README) and watch `MelonLoader/Latest.log`:

```
[WolfHowlFix] ready (enabled=True); patched BaseAi.UpdateScent
[WolfHowlFix] scent timer repaired: +23.5 -> +0.017 (now 0.02s of 30s)
[WolfHowlFix] new pursuit: carried-over timer 30.6s discarded
[WolfHowlFix] id=-348410 howl END CLEAN finalNt=0.98 wallClock=5.7s
```

The first line confirms the fix is active. The second shows a bad increment being corrected in flight. The third shows a stale timer discarded as a new pursuit begins. The fourth is a howl playing its full 5.7-second clip to completion — `END BUGGED` would mean it restarted.

## It is not only wolves

The broken timer lives in the AI code shared by all predators, so bears, moose and cougars were
affected too. They do not howl, so the symptom went unnoticed — it showed up instead as an animal
repeatedly starting and abandoning a chase.

## Will animals behave differently?

Yes, in the direction the game intended. Previously an animal that noticed you from too far away
gave up immediately; now it stays interested for the full 30 seconds before letting go.

**Bears change the most.** A bear that used to lose interest instantly will now pursue for the
full 30 seconds, which is a noticeable difference if you are used to the broken behaviour.

Nothing is more aggressive, faster, or better at finding you. Animals simply stop forgetting
about you instantly.
