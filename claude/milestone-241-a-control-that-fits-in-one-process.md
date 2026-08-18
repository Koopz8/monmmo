# Milestone 241: a control that fits in one process

239 owed two things and they turned out to be one thing. *Which signs actually ran* could not be
asked, because everything the run executed was an address and a map — the walk remembered nothing
about which of the map's four lists a script came off. A flag moved by a sign and a flag moved by
the person standing next to it were the same record. **The map scan has told the five kinds apart
since 224; the run knew none of them.**

And *what the seven flags at the floor are* was measured by running the whole playthrough twice,
one commit apart, and writing the two tables side by side. That is a number nobody can re-check
without building the earlier commit, which by this project's own standard is a number nobody can
catch.

---

## Both are now one command

```
  dotnet run … --play --signs
```

> **CORRECTED AT 242: `215` is `317`.** The read set was keyed on (map, address), which counts
> two signs on one map written on the same block as one sign. 214 addresses and 79 maps are
> right; every sign count below is one milestone too low — 317 / 396 / 465, not 215 / 288 / 328.

```
  WHAT THE FOURTH LIST DID
    215 of the 519 sign script(s) ran, at 214 of the 360 address(es), on 79 of the 143 map(s)
    read 1864 time(s) over the whole run; the most-read:
      7.6  0x0816AC94 x48, 6.1 0x0816A580 x36, 1.48 0x081A891B x24, 1.48 0x081A8935 x24
    20 flag move(s) by a sign, 3 distinct flag(s); 2 of those NOTHING ELSE in this run moved
    by kind, every flag move this run made: APerson 45, ASign 20, ATrigger 14, OnArrival 6

    THE CONTROL — the same run with no signs in it: 183 maps, 153 flags in 6 pass(es),
      stopped because a pass opened nothing new
    so signs are worth 0 map(s) and 7 flag(s)
      only WITH signs: 0x0031 (gates), 0x0032 (gates), 0x0233, 0x0234, 0x0235, 0x026D, 0x0834
        0x0031  APerson, holds 1 object(s) — 3.43 p1
        0x0032  APerson, holds 1 object(s) — 30.0 p2
    of those 7, 2 were moved by a sign itself — the rest are what the signs' own doors
      opened for somebody else
```

**The control reproduces 239's before-numbers exactly** — 183/153 in 6, and 243/231 in 5 and
381/294 in 6 at the other settings — off a single build. It is the same shape as the reversed-image
floor every reading in this project is measured against, and it did not exist for the run.

## The seven, at last

`0x0031`, `0x0032`, `0x0233`, `0x0234`, `0x0235`, `0x026D`, `0x0834`. Two gate something and each
holds exactly **one person** — `3.43 p1` and `30.0 p2`. That is the whole of what the fourth list
is worth at the floor: two people, and no square anywhere.

**Only two of the seven were moved by a sign at all.** The other five are what the signs' own doors
opened for somebody else — a sign sets something, a person's script branches on it, and the flag
that lands is the person's. One of the two is `0x0233`, set by a sign on `30.0`; the person
`0x0032` hides is also on `30.0`.

Across the settings the fourth list is worth **0 maps every time** and 7 / 3 / 2 flags. Signs are
not how this game gates anything, and now that is subtracted rather than asserted.

## And the control never cycles

Every one of them stops with *a pass opened nothing new*. **So 239 was right that the signs are
what makes the run go round**, even though 240 showed it had named the wrong flag. The two
oscillators 240 found close the circle: `0x026C` is set by a sign — `1.59`, `0x08162212` — and
cleared by something else, which is exactly a state whose value at the end of a pass depends on
which map the walk reached last.

## What the numbers are of

Three counts, not one:

```
  519 sign scripts   at 360 addresses   on 143 maps       (the file)
  215 ran            at 214 addresses   on  79 maps       (the floor)    <- 317 (242)
  328 ran            at 327 addresses   on 134 maps       (the widest)   <- 465 (242)
```

**And the two corrected numbers are the ones this very paragraph explains.** The sentence below
about blocks being shared is right; the count above it was made the other way. 242 has it.

Blocks are shared, so a script read in two towns is **two signs read and one address**, and
`SignsRead` is keyed by (map, address) for that reason. That is 224's finding standing in the run
rather than in the scan — the milestone where five copies of "every script on a map" disagreed.

Free and not claimed: the most-read sign in the widest run is `1.114 0x08163F5A`, read **154
times** in one run. A sign beside a square the walk stands on is read once per pass per square it
is beside, and nothing has asked whether 154 is a wide sign or a wide walk.

## The breaks

Five, five catches:

| break | what went red |
|---|---|
| a sign filed as a person | four tests |
| the control reads signs anyway | `WithTheFourthListOffNoSignRuns` |
| signs keyed by address and not by (map, address) | two tests |
| the bytes after a fight filed under a person rather than the starter | that test |
| a trigger filed as an arrival script | two tests |

The third is the one worth naming, and the fourth is the one that would have passed by accident:
a person is what the kind would default to, so a continuation filed as one looks right in every
real case and says nothing.

3031 → 3038 tests, all green. **The six rows of the floor table did not move**, re-run rather than
assumed.

---

## What is still owed

* **Why the seven are what they are.** Two people is the answer to *how much*; which sign opens
  which person is one `--read-from` away and this milestone did not take it.
* ~~The 191 sign scripts that never run at any setting.~~ **CLOSED AT 242** — and there are 54,
  not 191: 36 on maps the run never reaches, 17 walls on maps it walks, and exactly ONE that
  nothing could ever stand beside.
* **`1.114`'s 154 reads.**
* **`0x026C` and `0x0807`** (240), **`0x4001` in two namespaces** (240), **`0x194`'s nineteen
  doors** (236), **`0x82`'s seven words** (238), the three numbers nothing computes (231),
  `0x406F` (229), and everything owed at 215 onwards.
