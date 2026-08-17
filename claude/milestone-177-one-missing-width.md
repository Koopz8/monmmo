# Milestone 177 — One missing width was hiding nineteen people

Delivered as `claude-213.bundle` on `36f5588ae`. 2679 tests green from a clean clone at the
base. Measured against the cartridge.

## What moved

| | 176 | now |
|---|---|---|
| blocks that stop at a command with no width | (never measured) 142 | **80** |
| flags set or cleared by a script somewhere | 241 | **258** |
| of those, on an arm a run could take | 236 | **253** |
| of those, gating something | 77 | **89** |
| the code boundary | 245 | **233** |
| people who stand somewhere for ever | 388 | **382** |
| **people who never arrive at all** | **46** | **21** |
| flags nothing clears | 28 | **21** |

The wall list is unchanged at nine people behind five flags. **`--play`'s floor is unchanged at
179 maps and 139 flags** — sixty-two blocks became readable and the run reached nothing new,
which is worth saying rather than glossing: they are on maps it does not get to or behind
conditions it does not meet.

## The chain

`--in-the-image 0x009D` — nineteen people across eleven maps, nothing had ever looked at it —
found two sites, neither opened, and the climb reached `call 0x081A651A` from a block at
`0x08162DBB`. Climbing that said **`opened by 1.80 on arrival`**, which is a contradiction: if
the map scan reaches the caller it should follow the call.

It does not, because the read stops eleven bytes earlier:

```
08162DAD  9C 3E 00            <- 0x9C, two bytes, known
08162DB0  9E 3E 00            <- 0x9E, NO WIDTH. the read stops here
08162DB3  28 28 00
08162DB6  16 01 40 01 00      setvar 0x4001, 1
08162DBB  04 1A 65 1A 08      call 0x081A651A
                                 └─ 2A 9D 00   clearflag 0x009D
```

One byte with no entry in a table, and nineteen people are invisible on eleven maps.

## Three scans, one blind spot, again

`--scripts` reported **8** blocks stopping at an unknown command. The real figure is **142**.

It walked *people*, and only the *first block* of each — so a command had to be in a person's
opening straight line to be counted at all. Neither a person nor a first block, so 0x9E was
invisible to both halves, and the output was identical to a reading that had looked. Eight is
small enough to look like a solved problem.

`--derive` was worse. It rolled its own list of four kinds — people, signs, triggers, on-entry
— long after `EveryScriptOn` was written so that *what counts as every script* would live in
one place and be wrong in one place. It was not called. So the map's own script list was
invisible, and **`0xD0`, which stops fifty-one blocks — more than the next three commands
together — did not appear in that report at all.** Not scored low. Absent.

That is the fourth place this exact fault has been found and the third milestone running. It
was fixed in `EveryScriptOn` (173), in the wall list (174), in `--flags` (175). There are
roughly twenty-eight more hand-rolled enumerations in `Program.cs`, and **none of them can be
guarded**: breaking one on purpose fails no test, because `Program.cs` has no tests and no
fixture can hold a map library. That break was run and came back green, and it is recorded here
as green rather than dressed up. What was done instead is the project's standing answer to it:
`--derive` and `--scripts` now print what they opened, by kind, so a rolled-own list shows up
in the output as a missing line.

## The width every test got wrong

`0xD0`, sixteen sites:

```
D0 A4 08 | 02 | 0F 00 55 22 17 08 09 02 02
D0 A5 08 | 02 | 0F 00 E0 2A 17 08 09 03 02
D0 B0 08 | 02 | 0F 00 2D 96 17 08 09 03 02
D0 17 08 | 0F 00 B2 D0 17 08 09 04 68
```

Second byte varies, third is `0x08` at every site: a word. Then the question is whether the
`0x02` is this command's third argument or an `end`. **Every continuation test in `--derive`
said three** — at three it reads on into a textbox that parses beautifully. And the last site
has no `0x02` at all, which three cannot explain.

What settles it is the one signal in this file that says where a script *stops*: **eleven of
the sixteen following textboxes are pointed at by something else in the image.** You do not
fall into a block that has its own pointer. So the `0x02` is an `end`, the textbox is a script
in its own right, and the command is two bytes wide.

`--derive` now counts that as a column and rules out any width scoring 50% or more on it. It
immediately ruled out three for `0xD0` and seven for `0x78`. This is the trap the note on
`0x4F` has described from the other side since milestone 14 — *the continuation tests reward
the width that swallows whatever the reader cannot yet handle* — and a block boundary is the
one thing they could not see.

## The widths adopted

* **`0xD0` = 2** — 51 stopped blocks. Evidence above.
* **`0x78` = 4** — 17 stopped blocks, and it is a pointer: `69 | 78 9F 92 1A 08 | 6D 6B 02`
  repeating every nine bytes. No other width ends on a pointer at all.
* **`0x9E` = 2** — 3 stopped blocks, travelling in a pair with `0x9C` carrying the same word,
  at three sites on maps that share nothing. The one that mattered.

`--derive`'s verdict for `0xD0` is still wrong: with three ruled out it now says zero, because
the speech tie-breaker outranks a width that reads to a proper end at 98% of its sites. That is
left alone rather than tuned until it agrees. The verdict has always been advisory and the
doctrine has always been *read the bytes*; tuning a scorer until it produces the answer already
obtained is not evidence.

## The guards

**Six breaks, five caught.** 0x9E losing its width, 0xD0 taking the three every other test
preferred, 0x78 losing its width, the boundary rule ignoring where the width lands, and the
boundary rule being confident with no sites behind it.

The sixth — `--derive` rolling its own four-kind list again — **came back green**, and it is
the structural gap above rather than a missing decoy. The rule cannot be guarded where it
lives.

`ReadsOnIntoSomebodyElses` was written in `Program.cs` and moved into the library before it was
committed, which is the same lesson landing for the third time in two milestones.

## What is next

* **`--play`'s floor did not move.** Sixty-two newly readable blocks, no new ground. That is
  either honest or a second wall behind the first, and it needs its own measurement.
* **The remaining 80 stopped blocks**, led by `0x3F` (15), `0xE6` (6), `0xC0` (5), `0xA7` (4),
  `0x73` (4), `0x92` (4). Every one is a small puzzle of exactly the shape above.
* **The other five wall flags** — `0x0013`, `0x0012`, `0x0089`, `0x0053`, `0x0017` — nine
  people, still unread.
* **The 8 flags with an entry point nothing opens**, down from 20 as the widths opened them.
  A site something jumps into is 8.1% of the unopened sites against 1.3% in the reversal.
* **The twenty-eight unguardable enumerations in `Program.cs`.** Either the map walk moves into
  the library where a fixture can reach it, or this fault is found a fifth time.

## Still open, unchanged

Held items; signs never run; `--say-yes` costing party members; the nine `ARRIVED ON AN
ISLAND`s; eleven maps with no way in; shortest-chain ways in; `Bag.PocketCapacity` in shipped
saves; money modelled; `SpecialContracts.ComparedAfter`; co-op step 4; `StoryClosure` as the
no-bag control; `MapScripts` with no coverage at all; milestone docs for `StoryClosure`,
`Autoplayer` and `SpecialContracts`; sound; and whether `Reachable` should honour a trigger's
own condition.
