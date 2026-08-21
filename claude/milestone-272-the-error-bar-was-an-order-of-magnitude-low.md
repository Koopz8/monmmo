# Milestone 272: the error bar was an order of magnitude low

269 left a nudge owed for the three-byte sweeps. `Moves`, `Writes` and `AsksWhoKnows` scan for a
pattern — a command byte and a halfword — so "aim it a few bytes off" does not translate. What
does is asking the same sweep for a number the cartridge does not use.

---

## The control

`AnUnusedNumber` takes a flag or variable id and asks the same sweep of its sixteen nearest
neighbours **with the same high byte** that nothing uses — for a flag: named by no script the maps
open, gating nothing, not set by a new game (507 ids are used by that rule); for a variable:
named by no script the maps open (238). The high byte is kept on purpose: nought is the commonest
byte in the file and `0x08` is in every pointer, so the accident rate of `29 LL HH` depends on
`HH`, and a floor drawn from the other end of the number line is a floor for a different pattern.

`--in-the-image` and `--who-writes` print it under every count, with the median, the most, and
WHICH neighbour the most belongs to — an outlier is a name or it is nothing.

## What it says about the whole-image error bar

`--in-the-image` has opened, since 175, with *"a three-byte pattern turns up by accident about 1.0
time(s) in an image this size — which is the error bar on every count below"*. Measured:

```
  0x0089 —  9 site(s), 0 read as script   floor: median  1, most 11 (0x0078); reads median 0, most 1
  0x0014 — 15 site(s), 6 read as script   floor: median 13, most 36 (0x0025); reads median 2, most 3
  0x0012 —  5 site(s), 2 read as script   floor: median 13, most 36
  0x0013 —  8 site(s), 1 read as script   floor: median 13, most 36
  0x0053 —  1 site(s), 0 read as script   floor: median 10, most 36
  0x0017 —  2 site(s), 0 read as script   floor: median 13, most 36
  0x003E —  1 site(s), 1 opened           floor: median 12, most 36
  0x0805 — 12 site(s), 2 read, 1 opened   floor: median  7, most 17 (0x0800)
```

**For a flag in `0x00xx` the accident rate is ten to thirteen sites, not one.** `29 LL 00` is two
of the commonest bytes in the file around a third, and the uniform error bar could not see it.
The five wall flags — `0x0013`, `0x0012`, `0x0089`, `0x0053`, `0x0017` — are each at or below
their own floor's median on site count, and `0x0014`'s "15 sites, 6 reading as script" is one
median on sites and three above the floor's most on reads, where three of its six are 271's
new-game-adjacent noise. **Every whole-image site count this command has printed for a
`0x00xx` flag has been read against an error bar an order of magnitude too low**, and the
sentence it supported — *nothing in the file moves it; compiled code* — is stronger now, not
weaker, because the nine and the eight and the five were never above the floor to begin with.

`0x0025`, the floor's own outlier at 36: twenty of them in four clumps, fourteen inside 451 bytes
of a table, two reading as script, nought opened. What a floor entry should look like.

## On the variable side

```
  0x4055 — 12 site(s), 10 read, 9 opened   floor: median 5, most 15; reads median 0, most 3
  0x4059 —  1 site(s),  1 read, 1 opened   floor: median 5, most 15
  0x4001 — 274 site(s), 206 read, 166 opened  floor: median 4, most 151; reads median 0, most 18
  0x8004 — 403 site(s), 364 read, 317 opened  floor: median 4, most 60;  reads median 1, most 10
```

The opened counts were never in doubt; the floor says what the UNOPENED remainder is worth. For
`0x4055` it is 12 against a median of 5: the three unopened sites are the floor. The variable
band's floor is lower than the flag band's because `16 LL 40` carries a `0x40`, which is rarer
than a nought.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| neighbours cross the high byte | 1 | 1 |
| used ids are not skipped | 1 | **3** — the two sweep fixtures mark everything but one id used |
| the floor counts reads as sites | 2 | **1** — the break touched the flag sweep only |
| the median is the mean | 1 | 1 |
| neighbours returned unsorted | "control", 0 | **1** — not a control: the printer shows the first three, so order is a rule |

## What is left

* **`AsksWhoKnows`** has no nudge yet. A move id above the table's 355 is the unused number
  there, and the sweep takes a bound rather than an id, so it is a different shape of change.
* ~~The opening sentence of `--in-the-image`~~ says, since this milestone, that 1.0 is a uniform
  file's number and points at the floor under each count.
* **The 38** (271), the **seam** (269) — untouched.
