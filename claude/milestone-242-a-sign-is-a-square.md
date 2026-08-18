# Milestone 242: a sign is a square

241 printed **"215 of the 519 sign scripts ran"** and nothing about the other 304. That number
reads the same whether the run is a few rooms short or three hundred signs are written on walls
nothing can walk up to, and those are opposite findings — so this milestone set out to sort them,
the way `WhyTheGatesAreShut` sorts flags.

Sorting them needed a key with one entry per sign. **241 did not have one.**

---

## 215 was 317

```
                                     241 SAID           IT IS
  --play                             215 of 519         317 of 519
  --play --say-yes                   288                396
  --play --say-yes --boat --in-order 328                465
```

A sign is a **square**. The address is a script, and 519 sign scripts sit at 360 addresses because
blocks are shared — so a read set keyed on (map, address) counts two signs on one map written on
the same block as one sign read. Every column of that line was right except the one the key was
wrong for: **214 of 360 addresses and 79 of 143 maps have not moved.**

**This is 224 for the third time**, and the first time this project has done it to itself inside a
session: 241's own document quotes 224 while making 224's mistake one line above the quote. The
number was written a day after the rule.

## Which is why the buckets are worth having

```
  --play                              202 unread
      195  on a map the run never reached
        6  it reached the map and never got to that wall
        1  NOTHING COULD EVER STAND BESIDE IT

  --play --say-yes --boat --in-order    54 unread
       36  on a map the run never reached
       17  it reached the map and never got to that wall
        1  NOTHING COULD EVER STAND BESIDE IT
```

**Exactly one sign in the cartridge cannot be read by anything** — `10.6 (4,1)`, block
`0x0816C153` — and it is the same one at both settings, which is the check 211 left behind: a
bucket that is about the FILE must not move when a lever moves. This one is asked with the water
opened, so it means *no player and no run at any setting*, not *no walker*.

Everything else is reach. At the widest setting 36 of the 54 are on maps the run never gets to and
17 are walls on maps it walks — which is a very different picture from "191 signs never run",
which is what 241's wrong key implied.

## The rule is five squares and not one

A sign's own square is solid — that is what a sign *is*. A rule asking only whether the sign's
square is walkable reads **every sign in the game** as one nothing could stand beside, and the
first version of these tests could not tell that from the right answer: the only fixture that
exercised the difference was the water one, by accident. Breaking the rule went red in one test
and it should have gone red in two.

So a fixture was added first — a sign on a solid square with floor beside it — and then the break
was re-run. **That is the guard doing its job on the guard**: a break that fails less than it
should is the same signal as a break that passes.

## The breaks

Six, six catches:

| break | what went red |
|---|---|
| only the sign's own square is asked | two tests |
| the file's answer asked without the water opened | `ASignBesideNothingButWaterIsStillOneSomethingCanReach` |
| reach ranked ahead of the file's answer | two tests |
| the read set keyed without the map | four tests |
| the run's sign set keyed by address again | two tests |
| a hidden item counted as an unread sign | `EveryScriptedSignIsEitherReadOrUnread` |

The last one is the completeness check and it is the one worth keeping: every scripted sign is
read **or** unread, never both and never neither. A classifier that quietly drops a case reads as
a clean answer, and nothing else here would have noticed.

3038 → 3049 tests, all green. **The six rows of the floor table did not move.**

---

## What is still owed

* **`10.6 (4,1)`** — the one sign nothing can stand beside. Whether it is a mistake in the
  cartridge, a sign behind furniture, or a square this project's collision reading gets wrong.
  It is one `--read-from` and one `--script-map 10.6`.
* **The 17 walls on maps the widest run walks.** Eight of them are `12.0`, in pairs at
  `(15,2)/(16,2)`, `(13,10)/(14,10)`, `(13,17)/(14,17)` — which looks like a room the walk does
  not enter rather than seventeen separate misses.
* **Why the floor's seven flags are what they are** (241) — still owed.
* **`0x026C` and `0x0807`** (240), **`0x4001` in two namespaces** (240), **`0x194`'s nineteen
  doors** (236), **`0x82`'s seven words** (238), the three numbers nothing computes (231),
  `0x406F` (229), and everything owed at 215 onwards.
