# Milestone 178 — The biggest number was the wrong number

Delivered as `claude-214.bundle` on the tip of `213`. 2686 tests green from a clean clone at
the base. Measured against the cartridge.

## The measurement

`--play` has always reported one error bar: the routines it could not answer. It has never
reported the commands it could not **read**. Those are not the same boundary and they must not
be one number:

* a routine is the game's own compiled code, and nothing in this project will ever follow one;
* a command with no width is **a gap in a table in this repository**, and a script that hits
  one comes back short with no error anywhere.

Asked for the first time:

```
399 script run(s) stopped at 3 command(s) this project has no width for
  0x73 stopped 378 run(s)
  0xB3 stopped  15 run(s)
  0x36 stopped   6 run(s)
```

Three hundred and seventy-eight, on one byte — more than every other unknown command on this
cartridge put together, and it has been invisible for the life of the instrument.

## And then the ranking, which is the actual finding

`0x73` is worth nothing. All four of its sites are the same idiom, and what follows is two
bytes long:

```
69 | 04 15 B3 1B 08 | 04 3E C0 1B 08 | 25 8E 00 | 2F 1E 00 | 73 ?? ?? ?? ?? | 6B 02
```

Whatever its width turns out to be, the block ends immediately afterwards with a release and an
`end`. **Nothing is behind it.** Meanwhile `0x9E` stopped three blocks and one of those three
sat eleven bytes from the `call` that puts nineteen people on eleven maps.

Ranking unknown commands by how often they stop a read puts the harmless one at the top by two
orders of magnitude and buries the expensive one. Milestone 174 made exactly this mistake with
people in doorways, and wrote the rule down: **a count is not a ranking.** The list of unknown
commands has been ordered by count since it existed.

`WhatIsBehindAStop` re-ranks it. **The width is unknown — that is the whole problem — so it
does not pick one.** It tries every plausible width, keeps only the ones that decode to a
proper end, and reports what they find between them. "Every width that parses finds nothing"
needs no guess to stand on.

```
  The commands stopping the most reads, and what is behind each:
    0x3F  stops  15  — 6 command(s), including clearflag, 0x19, 0x06, call
    0xE6  stops   6  — 15 command(s), including setvar, 0x06
    0xC0  stops   5  — 12 command(s), including setvar, 0x06
    0xA7  stops   4  — 21 command(s), including setvar, 0x07, 0x06
    0x73  stops   4  — nothing but the block ending (2 command(s))
    0x92  stops   4  — no width reads on from here at all — probably not a command
    ...
  29 of those 35 have something behind them at every width that reads on. THAT is the list:
    0x3F (15), 0xE6 (6), 0xC0 (5), 0xA7 (4), 0xCA (3), 0x43 (3), 0xC4 (3), 0xB3 (3), ...
```

**`0x3F` is the top of the real list**, and there is a `clearflag` and a `call` behind it.

## A third category nobody had a name for

Four of the thirty-five — `0x92`, `0x9B`, `0xD3`, `0x62` — read on from **no width at all**.
That is not a missing width. It is a read that was already lost: a misalignment that landed on
a data byte, and the byte reported as "the command stopping this read" is not a command. Kept
apart from "nothing is behind it" deliberately, because one of those is a stop worth ignoring
and the other is a block this project is not reading correctly at all.

## `0x73` deliberately not adopted

Both 4 and 5 parse at all four sites; 4 preserves the `releaseall` that pairs with the
`lockall` every one of these blocks opens with, so 4 is very probably right. It is **not**
adopted, because adopting it would open nothing: the block ends either way. A width taken on
weak evidence for no gain is the worst trade this project has available, and the honest note is
worth more than the entry.

## The guards

**Ten breaks, ten caught** — after two came back green and were fixed rather than written off:

* *a width that dies counts as one that found nothing.* Green first time round, because the
  fixture only offered widths that either parsed cleanly or read **zero** commands, and the
  zero-command case was already excluded by a different check. Rebuilt so one width reads a
  `setflag` and then runs into the unknown byte: now the break both inflates the count and
  smuggles a consequence in, and the test catches both.
* *the run stops recording the commands it could not read.* Green first time, because nothing
  drove `Autoplayer.Play` over a script that stopped. There is a light fixture for that walk
  already; it now has one.

The rest: text counted as a consequence; a stop nothing reads on from called free; `setflag`
dropped from the list of things that change the world; and the width and boundary rules from
177 re-broken.

## What is next

* **`0x3F`, `0xE6`, `0xC0`, `0xA7`** — the top of the list that is ranked by what it costs.
  Twenty-nine commands have something behind them.
* **The four that no width reads on from.** Those blocks are being misread somewhere earlier,
  and finding where is a different job from finding a width.
* **`--play`'s floor still has not moved** — 179 maps, 139 flags. It now says the reading is
  not what is holding it back at only three commands, so the next suspect is the party: six at
  level 25, 63 fights lost, and GIOVANNI is one of them.
* **The other five wall flags** — `0x0013`, `0x0012`, `0x0089`, `0x0053`, `0x0017`.
* **The ~28 unguardable enumerations in `Program.cs`**, unchanged and now found four times.

## Still open, unchanged

Held items; signs never run; `--say-yes` costing party members; the nine `ARRIVED ON AN
ISLAND`s; eleven maps with no way in; shortest-chain ways in; `Bag.PocketCapacity` in shipped
saves; money modelled; `SpecialContracts.ComparedAfter`; co-op step 4; `StoryClosure` as the
no-bag control; `MapScripts` with no coverage at all; milestone docs for `StoryClosure`,
`Autoplayer` and `SpecialContracts`; sound; and whether `Reachable` should honour a trigger's
own condition.
