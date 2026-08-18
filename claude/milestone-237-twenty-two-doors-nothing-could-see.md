# Milestone 237: twenty-two doors nothing could see

`0x011E` was on the owed list because `10.14`'s nineteen shared signs ask it and nobody had read
the block. Reading it took one `--read-from`, and what it found was not the routine.

---

## What the nineteen signs are

```
  0x0816C96C
    2B 43 02              checkflag 0x0243
    06 00 A0 CA 16 08     if -> 0x0816CAA0    ("A COIN CASE is required…")
    0F 00 46 6F 19 08     loadpointer         ("A slot machine! Want to play?")
    09 05                 callstd 5           — the yes-or-no
    21 0D 80 00 00        compare 0x800D, 0
    06 01 6A C9 16 08     if EQUAL -> 0x0816C96A   (faceplayer ; end)
    9D 00 FF 00 / 9D 01 0A 00 / 9D 02 0E 00
    9C 40 00              dofieldeffect 64
    9E 40 00              wait for 64
    26 0D 80 1E 01        specialvar 0x800D <- 0x011E
    89                    <- STOPPED
```

**The cartridge says what it is in its own words.** *"A slot machine! Want to play?"* and, on the
other arm of `checkflag 0x0243`, *"A COIN CASE is required…"*.

That closes 234's correction from the other side. 234 was right that "GAME CORNER" was a name this
project had never read and had carried for three milestones out of a commit message. It is read
now — not as a name off a map table, but as the sentence the block puts on the screen.

## And there are twenty-two of them

Immediately after the block sits a run of twelve-byte stubs:

```
  0x16C95E  lockall ; setvar 0x8004,  0 ; goto 0x0816C96C ; end
  0x16C9A4  lockall ; setvar 0x8004,  1 ; goto 0x0816C96C ; end
  0x16C9B0  lockall ; setvar 0x8004,  2 ; goto 0x0816C96C ; end
  ...
  0x16CA94  lockall ; setvar 0x8004, 21 ; goto 0x0816C96C ; end
```

**Twenty-two doors, numbered 0 to 21, one per machine, each announcing which one it is.** That is
exactly the shape 194 built `--entries` for — PEWTER CITY's cutscene written four times, one per
square you can cross to start it.

Nineteen signs on `10.14` name them. Three stubs — `0x0816C9C8` (4), `0x0816CA4C` (15) and
`0x0816CA70` (18) — are named by nothing the map scan opens.

---

## `--entries` could not see any of it

```
  before   227 handover blocks, 24 rooms, 22 scenes, 68 doors
           announced in: 0x4001 x63, 0x4002 x6
  after    275 handover blocks, 28 rooms, 26 scenes, 112 doors
           announced in: 0x4001 x63, 0x8008 x25, 0x8004 x23, 0x4002 x6
```

The rule was **derived on `0x4001` at 173 and cut at the scratch cliff**: a stub is a door if the
one thing it writes is below `0x4010`. `0x8004` is numerically *above* that, so every one of these
twenty-two stubs read as a block doing something of its own, and a twenty-two-door scene was
invisible for forty-three milestones.

**Two bands, not one.** The scratch pads and the argument band `0x8000`–`0x800F` are both places a
door can say which door it is. What stays out is the story's own memory, and that is the whole
point of having a cut at all: a block that writes `0x4055` before handing over is moving the story
on, not announcing itself, and folding those together would fold two scenes into one because they
share an exit. The break that admits it fails **194's own test** as well as this milestone's three.

**The run does not move.** 183 / 153 flags / 103 hand-overs at the floor and 381 / 294 / 200 at
`--say-yes --boat --in-order`, before and after, and the folded-by-door count stays at 6. Forty
more runs fold and nothing the run reports changes — which is 195's finding again from a new
direction, and it is stated rather than assumed because 193's fault was exactly the opposite.

## `0x89`: read, measured, declined

The block stops on `0x89` and everything past it is unread. Read at each width:

```
  0 and 1   resume on 0x0D / 0x80 — the two halves of 0x800D read as opcodes
  2         89 0D 80 ; faceplayer ; end      <- the argument is the variable just written
  3         89 0D 80 6B ; end                — swallows the faceplayer
  4+        runs into the first door stub
```

**Width two is the only one that makes the argument `0x800D`** — which the `specialvar 0x011E` on
the line above just wrote — and the only one that gives this arm the same `faceplayer ; end` its
two sibling arms both have.

That is a good argument and it is **one site**. The whole-image column is one: of 21 places where a
`specialvar` ends exactly where an `0x89` begins, one takes the same variable it was handed, and
the reversed image gives 0 of 20. One against nought is not a column.

So it was measured before it was decided:

```
  with [0x89] = 2:   3857 blocks read to a proper end, 31 stopped   (from 3856 / 32)
                     the run: 183 / 153 and 381 / 294 — IDENTICAL
```

**One block, and no number this project quotes.** Declined. What would settle it is a second site,
and there is not one.

That is the mirror of 199's `0xC1`, adopted at two sites with the bar-miss said out loud — and
found at 230, thirty-one milestones later, to open no flag at any lever setting. Same situation,
same honesty, opposite decision, and this time the number was in front of the decision rather than
after it.

## The breaks

Four, four catches:

| break | what went red |
|---|---|
| the story's own memory counts as a door number | **194's own test**, and three of this milestone's |
| the argument band has no far end | `TheArgumentBandHasBothEnds` |
| back to the scratch cliff alone | two of this milestone's — the slot machines vanish |
| the cliff written here instead of handed in | `TheScratchCliffIsHandedIn` |

2998 → 3001 tests, all green.

---

## What is still owed

* **`0x011E`'s answer**, which is what `0x89` does with it — behind the one width above.
* **The three doors nothing names**: `0x8004` = 4, 15 and 18.
* **`0x9D`**, three of which run before the field effect here with `0, 255`, `1, 10`, `2, 14`.
* **`0x194`'s nineteen doors** on TRAINER TOWER (236) — now that `--entries` admits the argument
  band, some of them may be in the 26.
* **The three numbers nothing computes** (231).
* **`0x406F`** and the other 27 unsatisfiable arrival conditions (229).
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.
