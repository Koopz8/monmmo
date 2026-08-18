# Milestone 227: twenty, from the other direction

224 recovered two kinds of script and said twenty routines appeared. That number came from
comparing two runs of the whole instrument — the difference between a before and an after, which
is the weakest kind of evidence this project accepts. This derives it directly.

---

## Which routines only one kind of script asks

```
    person      asks 130 routine(s),  97 asked by no other kind
    sign        asks  31 routine(s),  20 asked by no other kind
    trigger     asks  24 routine(s),   6 asked by no other kind
    on load     asks  15 routine(s),   9: 0x0A7, 0x135, 0x142, 0x16D, 0x180, 0x182,
                                          0x1A1, 0x1AC, 0x1B9
    on arrival  asks  26 routine(s),  11: 0x0A9, 0x0CE, 0x0EB, 0x0EC, 0x114, 0x157,
                                          0x15C, 0x161, 0x162, 0x184, 0x1A5
```

**Nine and eleven.** The twenty 224 found by subtracting one run from another, named, from a
single run — and it is the same rule that computes what each kind opens alone, asked of routine
numbers instead of byte positions. One rule, two questions.

## And the routine that opens the fan club

`0x00A7` is the first of the nine. It is the line before the eight fan questions:

```
  0x16F15B   25 A7 00              special 0x00A7
             16 04 80 00 00        setvar 0x8004, 0
             26 0D 80 A3 00        specialvar 0x800D, 0x00A3
```

The whole image holds **three** byte runs of `25 A7 00` and only one of them is a command
position. So: one place in this game asks `0x00A7`, nothing branches on its answer, and it is the
map's own on-load script for the fan club, run before it asks whether each of eight people is a
fan of you.

## The audit, mostly clean

226 left the question of which other instruments count reads where they mean places. The full
table answers it, and for the ones the headlines rest on the answer is that they were already
right:

```
    0x5C trainerbattle   794 read(s) at 729 place(s)  x1.1
    0x86 pokemart         23 read(s) at  23 place(s)  x1.0
    0x29 setflag         528 read(s) at 284 place(s)  x1.9
    0x53 0x53            316 read(s) at  95 place(s)  x3.3
    0x7C findmove        200 read(s) at   3 place(s) x66.7
```

`--fights` reports **729 trainerbattles**, and the scan says `0x5C` is at 729 byte positions.
Two readings from different code, agreeing. `--who-knows` answers about the whole image with a
reversed-image floor, so `findmove`'s sixty-six never reached it. The flag work counts flags, not
sites, and a flag is a flag however many times it is read.

**A clean audit is a result.** The two instruments that were wrong were found and fixed at 220
and 223; the rest were built about places from the start, and now there is a table that says so
rather than a hope.

`--the-scan` also stopped printing only the worst two dozen codes. A filter that keeps output
readable must never decide which question gets asked, and the code somebody wants to look up is
as likely to sit near one as near sixty-seven.

---

## What changed

* `WhatTheScanOpens.OnlyIn` — the alone rule, returning the items rather than counting them, and
  asked of routine numbers as well as byte positions.
* `WhatTheScanOpens.Assemble` — the per-kind rows built from what was gathered, so the two alone
  columns are decided somewhere a test can reach.

Four breaks. Two caught outright; two came back green.

**The first green one is the fifth in nine milestones with the same cause** — the rule sat inside
a sweep needing a whole cartridge, and a break that made the routines column the kind's own set
passed every test in the file. Extracted, it fails one.

**The second green one had a fixture fault under it**: the rows fixture gave the same alone-count
for places and for routines, so a break computing one from the other answered correctly. That is
fixture-lie 5 — *a fixture built on the shape where two readings agree cannot tell them apart* —
and the fix is two places alone against one routine alone, asserted as different numbers.

The fourth break, truncating the printer back to two dozen codes, came back green and correctly
so: there is no rule left in the printer to break. It prints what the instrument returns.

2941 → 2944 tests, all green. Nothing the run does changed.

---

## What is still owed

* **The nineteen other routines only one of the recovered kinds asks.** `0x0A7` is read; the
  other eight of `on load` and all eleven of `on arrival` are not.
* **`0x0A9`, `0x0CE`, `0x0EB`, `0x0EC`** and the rest of the arrival list — the arrival scripts
  are where this game keeps its story bookkeeping, and these are the routines only they ask.
* **What `0x63` and `0x65` do.** What they take is read (226); what they do needs something this
  project does not have.
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.
