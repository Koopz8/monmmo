# Milestone 259: what the readers throw away, and the second kind of record

Four event-list readers drop a record whose square is off the map. The filter is right — a square
nobody can stand on is not a square — but it runs **before anything else sees the record**, and it
says nothing. 247, 250, 257 and 258 all rest on *228 triggers* and nobody had printed the number
underneath.

Three milestones in a row have been findings off the trigger list. Before a fourth, the list.

---

## Three of the four lose nothing

```
    object      9 dropped against  1639 kept — 0.55% of what the tables claim
    warp        0 dropped against  1294 kept — 0.00%
    trigger     0 dropped against   228 kept — 0.00%
    sign        0 dropped against   702 kept — 0.00%
```

**228 is 228.** Every reading built on the trigger list is complete, and so is every reading built
on signs and warps. That is the answer the owed item wanted and it is a nought, which is the
outcome an instrument like this has to be able to produce.

The count comes off **the same readers**. The drop is collected at the drop site rather than by a
second pass over the tables — a second copy of a record layout is how 251 lost `copyvar` and how
258 lost the downward arm of a walk, both inside the last four milestones.

## Two controls, and the one that was wrong

The object table loses nine. Are they records the cartridge meant, or bytes past the end of a
table a count claimed was longer than it is?

```
      0 of 9 carry a pointer into the cartridge — against 1583 of 1584 of the KEPT records
      9 of 9 have localId == index + 1        — against 1576 of 1576 of the KEPT records
      7 of 9 were the LAST record their table claimed, 2 came from the MIDDLE
```

The first reads as overwhelming. Ninety-seven per cent of kept objects carry a script pointer and
nought of the nine do; drawn at random that is one chance in ten thousand billion. It says they
are noise.

The second says they are real, and it is right. **A byte against an arithmetic beats a byte, a
pointer and a decode** — when two of your own readings disagree, ask which follows fewer edges
before deciding which is more rigorous (190's rule, and this is the fourth time it has decided
something). Bytes past a table cannot number themselves in sequence nine times.

Both are in the output permanently. A control that misled is worth more in the printout than out
of it.

## The second kind of record

```
0x3B5278:  0A 5F FF 00  32 00  12 00  0A 00 00 00  1B 00  03 00  00000000  0000
           id gfx KIND     x      y   ^^                  ^^^^^  ^^^^^^^^
```

Every one of the nine has **`0xFF` in the byte after the graphics id**, where all 1639 kept
records have nought. Its neighbours in the same table differ from it in five fields at once — they
have elevation 3, a movement type, a range nibble, a script and often a flag; it has none of
those. And its local id continues the table's sequence, so it holds a slot the cartridge counted.

It is a **clone**, and every field after the square means something else:

* the byte the ordinary layout calls an **elevation** is the local id of the object being cloned
* the two halfwords it calls a **trainer type and a sight range** are a map number and a bank

```
    3.3     (  50,  18) off the RIGHT  gfx  95  -> 3.27  #10   gfx  95 at (2,8)     MATCH
    3.6     (  -7,  21) off the LEFT   gfx  95  -> 3.34  #7    gfx  95 at (41,11)   MATCH
    3.16    (  32,   9) off the RIGHT  gfx  95  -> 3.56  #4    gfx  95 at (8,9)     MATCH
    3.20    (   6,  85) off the BOTTOM gfx  95  -> 3.1   #8    gfx  95 at (18,5)    MATCH
    3.22    ( 109,   3) off the RIGHT  gfx  41  -> 3.3   #12   gfx  41 at (1,13)    MATCH
    3.25    (  -8,  12) off the LEFT   gfx  95  -> 3.6   #10   gfx  95 at (52,22)   MATCH
    3.33    (  73,   7) off the RIGHT  gfx  95  -> 3.32  #13   gfx  95 at (1,47)    MATCH
    3.39    (  13,  -3) off the TOP    gfx  27  -> 3.0   #2    gfx  27 at (13,17)   MATCH
    3.63    (   7,  -2) off the TOP    gfx  41  -> 3.17  #1    gfx  41 at (7,18)    MATCH

    9 of 9 match, against a floor of 0.21
```

**The record's own graphics id is the graphics id of that object on that map, nine times out of
nine.** The floor is the expected number if each id were asked of an object drawn at random from
all 1639 — 0.21. This is 248's rule: when you split a record into fields, find something already
in the repository that can disagree with the split, and print how often it does not. The object
tables were built for a different question and cannot have been tuned to agree.

And the map each names is the one it sits beside: `3.3` CERULEAN CITY names ROUTE 9 and hangs off
its right edge; `3.39` ROUTE 21 names PALLET TOWN and hangs off its top. It is the person you can
see across the join.

**The reading that would have made the problem vanish was tried first and failed.** If the (x, y)
belonged to the *target* map rather than this one it would fit inside it — and it fits **1 of 9**,
no better than chance. A test whose convenient answer can come back no is worth running before the
inconvenient one.

## The filter that caught them was the wrong filter

All nine sit outside their own map, so the off-map test removed every one of them and the kind
byte was never needed. **The right answer for the wrong reason.** Decided on the kind byte now,
and the object list's off-map count goes **9 → 0**: with clones taken out properly, nothing in any
of this cartridge's four event lists is off the map at all.

Nothing else moved — 3166 tests were green before the fix and after it, because the records were
already being dropped. What changed is what would happen to a clone whose square landed inside its
own map: it would have been read as somebody standing at elevation ten with a trainer type of
twenty-seven. This cartridge has none, so the fixture now carries one. **A rule the cartridge
never exercises is a rule no break can be aimed at.**

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the kind byte never fires | 6 | **7** |
| the id column is off by one | 1 | 1 |
| the last-in-table control is off by one | 1 | 1 |
| the collector is not filled for clones | 2 | 2 |
| the clone's map comes off the wrong halfwords | 1 | 1 |
| **CONTROL:** the off-map test admits `x == width` | **0** | **0** |

The first miss is the useful kind: the world-file round trip guards object extraction too and I
had not counted it. A prediction that misses tells you which fixture covers more than you thought,
which is the same information a green break gives and cheaper.

And the control's nought was a real hole again. The fixture's stray object sat at `width + 5`,
where `>` and `>=` agree, so the boundary the off-map test is *about* was never asked. It sits at
exactly `width` now — the first square off a map numbered `0..width-1` — and the same break kills
six.

## What is left

* **What the clones are for on the server.** A person visible across a map join is a rendering
  question this project has not asked, and the nine are now readable rather than discarded.
  Whether `LoadedMap` should carry them is a DECISION.
* **The other three lists' record kinds.** Signs have a kind byte this project already reads (the
  buried ones, 248). Whether warps and triggers have one has not been asked.
* **`0x400D == 17` on `2.10`, `0x4085 == 1` on `3.9`, `0x406E == 1` and `== 3` on `11.0`** — 258's
  four leftovers, still unread.
