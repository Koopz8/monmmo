# Milestone 224: the list that was short

223 ended by saying the places-versus-reads question was waiting in every other map-scan sweep.
Building the one table that asks it of all of them at once found something else first: **the
shared list of every script, created at 221 to end this exact class of fault, had three of the
five kinds.**

---

## The table that found it

```
  2331 script entry(ies) at 1738 distinct address(es)
```

`--scripts` has said **2915** since it was written. Two numbers about the same cartridge, from
the same repository, and nothing anywhere compared them.

`WhatItIsWaitingFor.EveryScriptOn` knows five kinds: people, triggers, signs, **the scripts a map
runs on arrival**, and **the entries in the map's own script list**. The last two were added at
176 and 179, each time after "nothing in the world sets this flag" turned out to be a sentence
about a scan that had never opened one. 221 replaced five private three-kind copies with one
shared three-kind list, and moved `--routines`, `--specials`, `--standard`, `--through-a-call`,
the sound work and the item scan onto it.

**A shared wrong list is worse than five private ones.** Five disagree with each other and can be
caught by comparing them. One agrees with itself everywhere.

## What the two missing kinds were hiding

```
  entries        2331  ->  2915        addresses      1738  ->  1959
  command reads 78916  -> 90624        byte positions 21978 -> 24491

  routines called   158  ->  178       calls        3509  ->  4461
  branched on        46  ->   48       call places   861  ->   936
```

**Twenty routines nobody had ever seen called.** And one of them matters:

```
  0x0A3 asked 4x, 8 of its 16 branching site(s) taken by nought — 8 of 16 place(s)
```

A routine the widest run asks four times, whose silence decides **eight byte positions** — more
than everything else in the mixed bucket put together. The places where a run's silence decides
something go from **three to eleven**.

And 223's headline, published a few hours earlier, moves with it:

```
  before   25 of 411 byte position(s)      <- 223
  after    45 of 437 byte position(s)
```

223 was right about the correction and wrong about the number, because the correction was applied
to a scan that was reading four fifths of the cartridge's scripts.

## And the table itself

```
  90624 command read(s) at 24491 byte position(s) — 3.7 reads per byte

    0x7C findmove    200 read(s) at   3 place(s)  x66.7   on 47 map(s)
    0x17 addvar      290 read(s) at  10 place(s)  x29.0   on 15 map(s)
    0xA2 0xA2      10668 read(s) at 533 place(s)  x20.0   on 54 map(s)
    0x25 special    3864 read(s) at 576 place(s)   x6.7   on 238 map(s)

  11 of 108 code(s) are read once per byte position
```

**Eleven of a hundred and eight.** For every other code in this cartridge, a count of reads and a
count of places are different numbers, and `--the-scan` is now the one place to look up how
different before quoting either.

`findmove` at sixty-six reads per address is the sharpest: `--who-knows` has always answered
about the whole image and printed a reversed-image floor, which is why it was never caught by
this — but any map-scan count of that command would have been out by a factor of sixty-six.

---

## What changed

* `MapLibrary.EveryScript` asks `WhatItIsWaitingFor.EveryScriptOn` instead of listing kinds
  again. The sixth copy is gone; there is one reading and everything uses it.
* `MapLibrary.ScriptsOn(map)` — the per-map step, so a test can hand it a map rather than a
  cartridge. Four breaks, four catches, and the conditional-entry decoy is in there: an on-load
  entry whose pointer is a table of variable, value and script, which would parse as commands and
  be wrong quietly.
* `--the-scan` — reads against places for every command code, the error bar for every map-scan
  number in one table.

2919 → 2924 tests, all green. **Nothing the run does changed** — 183 maps in 6 passes, 396 places
asking 33 routines. The playthrough has always had its own enumeration and it was never short.

---

## What is still owed

* **Everything read off the short list between 221 and 224.** Milestones 221, 222 and 223 all ran
  on it. Their findings survive — the verdicts, the standard-routine reading, the places
  correction — but their numbers were four fifths of a cartridge and this milestone's are the
  ones to quote.
* **The 97 codes whose reads and places differ.** `--the-scan` says which; no instrument that
  counts them has been checked except the routine tables.
* **`0x0A3`.** Asked four times, decides eight places, never looked at.
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.
