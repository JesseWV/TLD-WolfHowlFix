# Background

How the wolf howl bug works, and what the mod does about it.

## The bug

A wolf howls when it gives up stalking something. That is intended behaviour — the "ah, forget it" howl.

The game decides *when* to give up with a timer: **how long have I been too far from my target?** If that exceeds 30 seconds, give up and howl.

That timer is broken. Each tick it should add a fraction of a second; instead it adds a much larger value, so after a **single tick** it is already far past the 30-second threshold.

So the wolf gives up on the first frame of every stalk, howls, drops back to wandering, notices you again a second later, and repeats. A howl fires roughly once per second — but the howl animation is 6 seconds long, so it never gets to finish.

Observed on an unmodded install: **48 restarts on a single wolf**, with individual howls running 15 to 60 seconds instead of the normal 5.7.

## Why it looks like an aurora bug

The broken code path only runs while a stalking wolf is beyond its "break stalking" range. Sense ranges were raised per variant without scaling that range to match, so only variants whose senses reach beyond their own break range are exposed:

| Variant | Detection | Smell | Hearing | Break-stalking range | Affected |
|---|---|---|---|---|---|
| Aurora | 105 | 150 | 90 | 100 | **yes** |
| Aurora (fluffy) | 105 | 150 | 90 | 100 | **yes** |
| Starving | 90 | 125 | 60 | 100 | **yes** |
| Grey, aurora | 90 | 120 | 120 | 100 | **yes** |
| Grey / timberwolf | 90 | 120 | 120 | **200** | no |
| Normal | 75 | 100 | 60 | 100 | no |
| Town | 40 | 50 | 30 | 100 | no |

A normal wolf can barely sense you past 100 m, so it is almost never "too far" while stalking and never reaches the bad path. An aurora wolf senses you to 150 m and can see you to roughly 210 m, so it frequently stalks from beyond 100 m — and does.

Grey wolves (the same animal as timberwolves) already have their break-stalking range raised to 200 and are unaffected — but **their aurora variant was left at 100**, so it is exposed like the rest.

**Four variants are affected, not just aurora wolves.** Starving wolves show it too.

## What the mod does

On affected wolves only, it raises the break-stalking range above the distance at which they can detect you at all. The broken code path becomes unreachable, the timer stays at zero, and the give-up check never fires spuriously.

This is the same relationship the game already uses for grey wolves (200 m against 120 m senses), applied to the variants that were missed.

Wolves that are already correctly tuned — grey/timberwolf, normal and town — are **skipped entirely** and keep their stock values.

**One value is adjusted, and only on affected wolves. Nothing else is touched** — detection, field of view, hearing, scent tracking, stalking, attacking, fleeing, struggles, howl scheduling and pack behaviour all work exactly as they do in the base game.

## Why wolves get more dangerous

The bug made wolves abandon *every* stalk within a frame, so they could rarely commit to a charge. With it fixed they acquire you, hold the stalk and come in.

That is what the game always intended, but it does make wolves meaningfully more threatening than the buggy behaviour most players have been living with.
