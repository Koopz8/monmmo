# Milestone 228: the flags a map moves on its own

227 asked the alone-rule of routine numbers and it named 224's twenty. This asks it of flags, and
the answer is larger than the routines were.

---

## Sixty-five flags nobody touches

```
    person      moves 182 flag(s), 152 moved by no other kind
    sign        moves  16 flag(s),   6
    trigger     moves  18 flag(s),   7
    on load     moves  61 flag(s),  54 moved by no other kind
    on arrival  moves  27 flag(s),  11
```

**Sixty-five of this game's flags are moved only by a map's own scripts** — the list a map runs
when it loads and what it runs on arrival. Not by a person, not by a sign, not by walking onto a
square. By the world setting itself up.

That is not a correction: `--flags` has always read all five kinds and its 264 was right. It is a
characterisation nobody had, and it is the sharpest description of what those two kinds are for.

## And the sharpest of the sixty-five

`0x0180` is one of the nine routines only the on-load scripts ask. It is asked at nineteen sites
which are **one byte position**, in the department stores' own on-load script:

```
  0x1BB1BA   26 0D 80 80 01     specialvar 0x800D, 0x0180
  0x1BB1BF   21 0D 80 00 00     compare 0x800D, 0
  0x1BB1C4   06 01 CE B1 1B 08  if EQUAL goto 0x081BB1CE
  0x1BB1CA   2A 70 00           clearflag 0x0070
  0x1BB1CD   03                 return
  0x1BB1CE   29 70 00           setflag 0x0070
  0x1BB1D1   03                 return
```

Asked of the whole image, flag `0x0070` has **three** byte runs that could move it and only two
that read as script — and those two are the two arms above. Nothing else in sixteen megabytes
sets or clears it.

**So one unanswerable routine decides that flag, entirely, at one address, on arrival at nineteen
maps.** A run that cannot answer takes the nought arm and sets it; a run that could answer might
clear it. Nothing this build can see reads it, which is its own finding and the reason it is not
on any wall list.

## What changed

* `WhatTheScanOpens` gathers flags per kind and reports the alone column for them, the same rule
  a third time — byte positions, routine numbers, and now flags.
* `RoutineAsked` and `FlagMoved` — what one command contributes, pulled out of the sweep.

Three breaks, three catches after one re-siting.

**The green one is the sixth in ten milestones and the cause has not changed**: the both-halves
rule — that a `clearflag` counts as much as a `setflag` — lived inside the sweep, so a break
removing the clear passed everything. Pulled out as `FlagMoved`, it fails one test. And the rule
matters here specifically: `0x0070`'s two movers are one set and one clear, so counting only sets
would have made this milestone's own finding read as a flag with a single mover.

2944 → 2946 tests, all green. Nothing the run does changed.

---

## What is still owed

* **The other fifty-three on-load flags and eleven arrival flags.** `0x0070` is read; the rest are
  a list.
* **`0x0180`**, and the eight other routines only the on-load scripts ask — `0x135`, `0x142`,
  `0x16D`, `0x182`, `0x1A1`, `0x1AC`, `0x1B9`.
* **The eleven the arrival scripts ask alone**, which is where the story bookkeeping is.
* **What `0x63` and `0x65` do** (226 read what they take).
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.
