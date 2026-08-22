# 307 — the list nothing runs

**3529 tests green.** Base: the tip of `claude-306`, 3516.

306 ended with a question it could not answer from where it stood: `0x0005` fences a door on
`2.1 TRAINER TOWER` and costs **nine maps**, *a script does move it*, and no run ever reaches
that script. The next task said, in as many words: **what sets `0x0005`, and why does no run get
there?**

It is set at `0x081C4F62`, on `2.1` itself, three commands into the map's own script list. The
run never gets there because **the run has never opened that list**, and the exported world
record does not carry it.

---

## The fifth list

A map header has a pointer this project found at 179 by running out of other explanations.
Entries are five bytes — a kind and a pointer — and the list ends at a zero kind. Kinds **2 and
4** point at a table of variable, value and script and become `MapData.OnEntry`, which the walk
has run since 176. **Every other kind points straight at a script**, and `MapScripts` says out
loud why those are left alone:

> Running one of those means knowing *when* the cartridge runs it — on load, on the first frame,
> once per visit — which is not written down anywhere in the data and is not going to be guessed
> at here. They are read and counted and left alone.

That reservation is about **running** them and it is right. What nobody had printed is what they
**move**.

```
      kind  entries  maps  addresses  resolve  reads as script  the walk runs it
      0x01       55    55         36       55               55  NO
      0x02       58    58         31       58       (a table)  yes, as on arrival
      0x03      130   130        105      130              129  NO
      0x04       33    33         15       33       (a table)  yes, as on arrival
      0x05       47    47         20       47               45  NO
      0x07        2     2          2        2                2  NO
```

**234 unconditional entries at 163 addresses on 159 maps.** And what they move, against the four
kinds the walk does run — the control rows are in the same table, so the number can be read
against something rather than admired:

```
      kind          scripts  addresses  maps  sets  clears  ONLY  the walk runs it
      person           1584       1250   350   169      19   152  yes
      sign              519        360   143    15       4     6  yes
      on arrival        350         58    61    12      17    11  yes
      on load           234        163   159    38      28    54  NO — until --on-load
      trigger           228        128    52    12       6     7  yes
```

**ONLY** is flags that kind moves and that no other kind touches either way — what dropping the
kind costs. The list nothing runs carries **54 of them, second only to `person`'s 152**, and
**47 of the 54 hide somebody: 74 objects between them.**

This is **239's shape a second time.** Signs were the fourth list; `MapData` carried none of them,
so "the playthrough never runs signs" was never a choice anybody made — there was nothing for it
to run. Here the CONDITIONS travelled and the scripts did not.

---

## What running them is worth

`--on-load` is the lever and it is **MODELLED**, for the reason `MapScripts` already gave. What is
READ is that these are scripts on this map, that 233 of 234 decode to a proper end, and what they
name.

Both runs in one process, which is 19's rule — 239 priced signs across two builds one commit
apart, every number in it was right, and nobody without that build could have found out:

```
      setting                                    maps  flags  passes  party  on-load scripts run
      --play                                                183    160       6      6  (off)
        the same run with --on-load                         183    165       6      6   82 on 60 map(s)
        the difference                                        0      5       0      0

      --play --say-yes                                      243    234       6      4  (off)
        the same run with --on-load                         243    240      10      6  121 on 86 map(s)
        the difference                                        0      6       4      2

      --play --say-yes --boat                               388    300       7      4  (off)
        the same run with --on-load                         397    313      10      6  224 on 153 map(s)
        the difference                                        9     13       3      2  gained 2.11, 2.2 … 2.9

      --play --say-yes --boat --surf --in-order             388    300       5      5  (off)
        the same run with --on-load                         397    314       7      6  224 on 153 map(s)
        the difference                                        9     14       2      1  gained 2.11, 2.2 … 2.9
```

**+9 maps, and every one of them is TRAINER TOWER.** The floor table has a seventh row and the
difference is subtracted from those same seven rows:

```
--play --say-yes --boat --surf --in-order --on-load   397 / 314 in 7, party of 6 at 75, 1 of 204 handed twice

  --on-load (MODELLED): +9 map(s), +14 flag(s), +2 pass(es), +1 in the party
```

Named to the byte position, with the control beside it:

```
  --play --say-yes --boat --in-order --on-load --moved 0x0005
    EVERY MOVE OF FLAG 0x0005: 1 set(s), 0 clear(s)
      pass 4  2.1  0x081C4F62  set 0x0005  (OnLoad)

  the same run without the lever
    EVERY MOVE OF FLAG 0x0005: 0 set(s), 0 clear(s)
      NOTHING THE RUN EXECUTED MOVED IT AS A FLAG
```

And the script, read rather than described:

```
    0x1C4F62  setvar 0x8004, 0
    0x1C4F67  special 0x194
    0x1C4F6A  copyvar 0x8000, 0x800D
    0x1C4F6F  compare 0x8000, 0     -> 0x081C4FA7
    0x1C4F7A  compare 0x8000, 1     -> 0x081C4FC5
    0x1C4F85  compare 0x8000, 2     -> 0x081C5019
    0x1C4F90  setflag 0x0002 ; setflag 0x0003 ; setflag 0x0004 ; setflag 0x0005
    0x1C4F9C  setvar 0x400E, 1 ; setvar 0x400F, 1
```

Four arms, and **three of the four set `0x0005`** — the arm at `0x081C4FC5` is the only one that
does not. `special 0x194` is the routine 291 read as an INDEX; what it answers here is compiled
code.

---

## THE NEGATIVE, which is worth as much as the list (30)

**54 flags no other kind moves, 47 of them hiding 74 objects — and running every one of them
opens NINE maps, behind ONE person.** The size of the blind spot is not the size of what was
behind it, and only the run says which.

Two of the flags in it make that concrete:

* **`0x0006`** is set by the on-load scripts of `2.1`–`2.8` and hides **9 objects on 9 maps**.
  The same list that opens TRAINER TOWER empties its floors.
* **`0x0040`–`0x004C`** are moved by `3.38 ROUTE 20`'s on-load alone — six set, five cleared —
  and it is worth **nought maps**. The boulders in SEAFOAM were never the wall.

---

## What it closed by accident

**"Seven boulder flags with no setter"** has been on this prompt's open list since the STRENGTH
work. Asked of the whole image, every one of the twelve is moved by exactly one script the map
scan opens and that script is a map's own:

```
    climbing from 0x168254 clearflag 0x0040 — opened by 3.38 on load (kind 3)
    climbing from 0x168257 clearflag 0x0041 — opened by 3.38 on load (kind 3)
    climbing from 0x16825A setflag   0x0042 — opened by 3.38 on load (kind 3)
    …
    climbing from 0x1684F4 setflag   0x0058 — opened by 3.42 on load (kind 3)
```

**Ten by ROUTE 20, two by ROUTE 23, and nothing else anywhere.** The prompt said *two set by
arrival scripts on ROUTE 20 and ROUTE 23, two set out of sight, seven set by nothing* — all
three of those buckets are one bucket, and it is this list. "Set by nothing" was a sentence
about a list nothing ran.

---

## And a second finding, which is about the instrument

`0x0070` is one of the 54, and it hides **19 objects across 19 maps**. Traced:

```
    pass 1  5.5   0x081BB1B4  set 0x0070      (OnLoad)
    pass 3  12.6  0x081BB1B4  CLEARED 0x0070  (OnLoad)
    pass 4  12.6  0x081BB1B4  set 0x0070      (OnLoad)
    pass 5  12.6  0x081BB1B4  CLEARED 0x0070  (OnLoad)
```

One shared block, reached from nineteen maps' on-load entries, alternating pass to pass. Read:

```
    0x1BB1BA  specialvar 0x800D, 0x0180
    0x1BB1BF  compare 0x800D, 0
    0x1BB1C4  if EQUAL goto 0x081BB1CE   -> setflag 0x0070
    0x1BB1CA  clearflag 0x0070
```

That is not a toggle. It is a straight function of one routine's answer — and the run cannot
answer `0x0180`. **An unanswerable `special` or `specialvar` writes NOTHING into the answer
slot**, which is in `ScriptRunner` in as many words, so the compare after it reads whatever the
last script left there.

*"The run answers nought"* has been in this prompt since 214. It was measured on `special
0x0187`, and it is true **of a variable nothing has written**. `--trace` prints the denominator
now:

```
  --play --say-yes --boat --surf --in-order --trace 0x800D
    3655 touch(es): 9 write(s), 3646 read(s)
    of the 3646 read(s), 968 found a value ALREADY IN THE SLOT — 26.5% — against 9 write(s).
```

**968 of 3646, against nine writes in the whole run.** A quarter of the time the run is not
answering nought; it is branching on the last script's leftovers. This is 8's trap wearing a
percent sign, and the share is printed with a truncation warning when the trace fills up, because
a percentage over a capped denominator is a fact about the cap.

Not fixed. Measured, named, and handed on — changing it is a change to every row of the floor
table and it needs its own milestone.

---

## The guards

Seven breaks, **seven predictions written before any of them ran, and seven matches:**

| break | predicted | killed |
|---|---|---|
| the lever is ignored, the list always runs | 1 | 1 |
| the fifth list runs AFTER the arrival scripts | 1 | 1 |
| the export keeps the condition tables too | 1 | 1 |
| kind 3 is called conditional | 3 or more | **4** |
| arrival scripts bucket as the fifth list | 2 | 2 |
| the on-load row leaves the floor table | 2 | 2 |
| **the control**: the column width goes back to a literal | **0** | **0** |

The fourth over-shot in the useful direction — the extra kill is
`WhatItIsWaitingForTests.EveryScriptOnAMapIncludesTheOnesWithNoConditionAtAll`, a fixture from
another milestone that covers more than I thought (32).

The seventh is green and **is not a missing fixture** (64). What it changes is a column width in
printed output: nothing reads it, and no answer can turn on it. Writing that down is the point of
predicting nought.

The rules got moved somewhere a fixture can reach before they were broken — `MapScripts.Unconditional`
is the export's filter, lifted out of a function that needs a whole cartridge, which is 219/221/222/223's
four-green-breaks-running cause.

And one guard is 35's rule rather than a count: `EveryLeverTheTableNamesIsBothOnAndOffSomewhereInIt`
names all five levers and asserts each is on somewhere, off somewhere, and **exactly one lever
apart from some other row** — so a lever the table cannot price fails a test instead of printing
a blank.

---

## One number that was in eight places

`--the-floor`'s command column was the literal `42`, in eight lines across two files. A fifth
lever makes the widest command fifty characters and all eight broke at once. It is
`TheFloorTable.CommandColumn` now, computed off `Settings` — 126's trap in a formatting string.

---

## What this leaves

* **`0x800D` is read on stale data at a quarter of its reads.** Whether the run should clear the
  answer slot before an unanswerable routine is a modelling decision that moves every row of the
  floor table. The number is printed; the decision is not made.
* **The kind byte is kept and not used.** 55 entries are kind 1, 130 are kind 3, 47 kind 5, 2
  kind 7, and this milestone runs all four the same way. Asking the lever per kind is one
  parameter and would say which kind carries the nine maps.
* **One thing is handed over twice with `--in-order` on**, which that lever exists to make
  impossible: `12.4 person 2` on passes 2 and 4, because `0x0070` flickers them back into the
  world. Read far enough to name the mechanism; not read far enough to say what should happen.
* **`0x0089` is still the only fence**, and 306's decision about it stands untouched.
* **The seventh row changes every "union of the six" sentence** in the prompt to a union of
  seven. `--the-floor` prints them; nothing has to be maintained by hand.
* **`2.11` and `2.22` join the stranded list**: the way back goes 284 → 345 squares and 7 → 9
  maps. Reaching TRAINER TOWER is not leaving it.
