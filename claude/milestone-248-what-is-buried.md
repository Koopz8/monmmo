# Milestone 248: what is buried, and the number that remembers it

246 printed a limit it could not close: *a routine that computes an id from a base would hold the
BASE and not the id, and nothing here would see it.* 247 left one thread at the top of the list —
183 of this cartridge's 702 signs are the buried kind, and in this family of games their flag is
taken from a base.

This reads their record. The base is still not found. Almost everything else is.

---

## Four bytes nobody had read

`MapLinkExtractor.ReadSigns` has known since 239 that a buried sign's last four bytes are not a
script pointer — that was the finding, and following one is how a reader ends up at `0x0000`. It
then throws them away. Read:

```
    1.0 (3,22)   0D 00 00 01   item 13, third 0, fourth 1     [POTION]
    1.0 (28,57)  0E 00 01 01   item 14, third 1, fourth 1     [ANTIDOTE]
    1.2 (46,2)   67 00 54 01   item 103, third 84, fourth 1   [TINYMUSHROOM]
    1.2 (26,2)   67 00 55 01   item 103, third 85, fourth 1   [TINYMUSHROOM]
    1.3 (20,16)  5E 00 02 01   item 94, third 2, fourth 1     [MOON STONE]
    1.4 (58,28)  26 00 BE 01   item 38, third 190, fourth 1   [LAVA COOKIE]
```

**All 183 first halfwords resolve to a name in the item table's 308 entries.** That is what makes
the split a reading and not a guess: the item table is a location this project made for another
question entirely, and 183 out of 183 is not what a wrong field offset produces.

```
    the first halfword: 183 value(s),  66 distinct, 0 to 200, 135 gap(s)
    the third byte    : 183 value(s), 183 distinct, 0 to 190,   8 gap(s)
    the fourth byte   : 183 value(s),   6 distinct, 1 to 129, 123 gap(s)
    the fourth byte's values: 1 x165, 10 x8, 20 x2, 40 x1, 100 x1, 129 x6
```

**The third byte is an index** — 183 distinct values, no repeats, 0 to 190 with eight unused
(7, 16, 40, 43, 44, 45, 46, 124). The fourth is a count in its low seven bits at all 183, so the
top bit is something else; six records set it.

And the same item is buried in many places — twelve of one, nine ULTRA BALLs, seven RARE CANDYs.
**Each has to be remembered separately, so the memory cannot be the item.** It is the index, and a
flag that is a base plus an index is exactly the shape 246 could not see.

## Twelve records, two bytes, one map

Twelve of the 183 name item 0, which the item table calls `????????`. Twelve carry a count above
one. **They are the same twelve, with nought either way:**

```
    12 carry a count above one (10 x8, 20 x2, 40 x1, 100 x1) and 12 of those are the ones naming
    no item — against 12 naming no item in total, and 0 that carry a count above one AND name an
    item
```

Two independent bytes of the record agreeing exactly. All twelve are on one map, at indices 51 to
62 — consecutive — and:

```
    all 12 are on 10.14, and 5 of the 5 coin hand-over chain(s) 208 found are on that map's own
    scripts
```

`10.14` is the map this project has read as an eighteen-by-fifteen interior with eleven people of
whom six hand something over against a coin count, a shared sign block that says *"A slot machine!
Want to play?"*, and every one of 208's five chains whose bound plus gift is ten thousand. Twelve
squares on its floor bury something that is **not an item** and carry a count of 10, 20, 40 or
100 where every other buried thing in the game carries one.

What they hand over is not in this record. **That it is not an item is**, and it comes off two
bytes and a second instrument built for a different question. 234 declined to name that map and
this does too.

## The base: unanswerable, with a number on it

If the flag is a base plus the index, the base plus every index has to land where nothing names a
flag — a hidden item's flag is the pickup routine's own business and no script says it. So the
range is a gap in the flag number line:

```
  525 flag number(s) are named by a script or by a person's record (297 by a command)
  and 3 gap(s) of at least 191 consecutive numbers below 0x4001 that nothing names:
    0x0300-0x04AF (432 wide)    0x04BD-0x0804 (840 wide)    0x089C-0x4000 (14181 wide)
```

Three, so the gap alone says nothing. And then the one thing that can narrow it — every candidate
base is above 255, so none fits in a THUMB instruction and the code must hold it as a literal it
loads (246):

```
  14883 base(s) would fit inside one of those gaps
    loaded at least 1 time(s): 889   REVERSED: 84
    loaded at least 2 time(s): 486   REVERSED: 28
    loaded at least 3 time(s): 320   REVERSED: 16
```

Ten times the floor and eight hundred and eighty-nine candidates. **It does not pick one, and the
command says so out loud rather than offering the best-looking number.** The most-loaded
candidates are `0x3214` at fifty-six loads and `0x3290` at forty-three, which are common constants
in compiled code and not a flag base — exactly what a filter this weak produces.

## What it costs every flag count in this project

**183 things are remembered by a number nothing in the file names.** Every flag figure here —
`322 gate something`, `264 are moved by a script somewhere`, `233 are the code boundary` — is a
count of flags something *names*, and these are not among them.

That is the fourth kind of read that is not a command, and the first on the flag side. 246 found
the map header; 247 found the trigger; both were variables. This one is a flag, it is computed
rather than written down, and no sweep in this project could ever have found it by looking for a
command.

## A break came back green, predicted at one

Five breaks; four killed exactly what was predicted. The fifth:

| break | predicted | went red |
|---|---|---|
| every sign kind is read as buried | 1 | `OnlyTheBuriedKindIsRead` |
| a run reaching the ceiling is dropped | 1 | `AGapThatRunsToTheCeilingIsReported` |
| gaps narrower than every index are reported | 1 | `AGapTooNarrowForEveryIndexIsNotReported` |
| **distinctness drops out of the index test** | **1** | **nothing** |
| the gap count is taken from nought, not the lowest value | 1 | `TheGapCountIsWithinTheRangeTheValuesOccupy` |

The fixture was `[0, 1, 1, 2]`. Four values from nought with a largest of two fails the
*largest is one less than the count* rule as well, so removing the distinctness check changed
nothing — **the fixture was caught by the other half of the rule and could not tell the two
apart.** That is trap 20 and fixture-lie 12 in three lines. `[0, 1, 1, 3]` satisfies every rule
but distinctness, and the same break then kills exactly one.

Second time in three milestones that predicting the count first turned a green break into a
finding on the spot.

3087 → 3096 tests. **The floor table did not move and `--play --signs` still reports 317 of 519.**

---

## What is still owed

* **The base.** Unanswerable from the flag number line and the load count. What would settle it
  is reading the routine that handles a buried item, which means reading compiled code — a thing
  this project has never done and would be a real decision rather than a milestone.
* **The run never picks any of them up.** 239 put signs into the walk and the buried kind have no
  script, so 183 items on 79 maps are lying on the ground that no lever setting collects. Nothing
  has printed how many of those 79 maps the run reaches.
* **The eight unused indices** — 7, 16, 40, 43, 44, 45, 46, 124. Four of them consecutive.
* **The spare bit.** Six records set the top bit of the count byte and every other reading of
  those six is ordinary. Nothing distinguishes what it means.
* **The trigger's other half** (247): `--arrivals` asks whether anything writes the VALUE a header
  condition wants; nobody has asked it of a trigger's 228 conditions.
* `0x4001`'s other two flag sites (244); `10.6 (4,1)` (242); the 17 walls (242); the floor's seven
  flags (241); `0x026C` and `0x0807` (240); `0x194`'s nineteen doors (236); `0x82`'s seven words
  (238); the three numbers nothing computes (231); `0x406F` (229); `9.6`'s puzzle.
