# Milestone 249: nothing is a wall with a shovel in front of it

248 read the 183 buried records and left two questions on the list: does anything exist only
underground, and how much of it does the run walk over?

Both have answers. One of them is a negative, and it is the better of the two.

---

## Twenty-one items, and the denominator cuts the other way

`ItemMentions` has read every way a script can name an item since it was built for the vending
machine — handed over, taken away, asked for, loaded into a routine's argument slot, sold. Asked
of the 65 kinds this cartridge buries:

```
  65 distinct item(s) are buried, and 44 of them are also named by a script the maps open
    the denominator: 164 of the 307 item(s) in the table are named by a script at all, so 65
    picked at random would leave about 30.3 named nowhere
    and 21 are named NOWHERE ELSE — buried is the only source any script the maps open offers,
    which is BELOW that floor:
       45 SACRED ASH        71 PP MAX          111 HEART SCALE
      133-142 the first ten berries, 148-152 the next five
      181 MACHO BRACE      184 SOOTHE BELL     …and one more
```

**Twenty-one sounds like a finding until the base rate is beside it.** A bit under half of every
item in this game is named by no script at all, so sixty-five drawn at random would leave about
thirty named nowhere. The buried kinds are *better* covered elsewhere than an item picked at
random, and the twenty-one is **below** what chance would produce rather than above it.

That line is printed with the comparison, not left for the reader to divide. It is the same
discipline that made 246 throw away its own aligned-word aggregate one milestone after building
it.

## And then the negative, which is the finding

```
    9 item(s) are ASKED FOR by a script somewhere; 3 of those are buried: POKé BALL,
    TINYMUSHROOM, BIG MUSHROOM — and 0 are asked for AND have no other source
```

Nine items in the whole game are asked for by a script. Three of them are things you can dig up.
**None of the three is only found buried, and nothing that is only found buried is ever asked
for.**

So the run's total inability to dig costs it **no reach at all**. There is no door in this game
behind an item that is only in the ground — no wall with a shovel in front of it. That is a
result the instrument could have come back the other way on, and it is worth more than the
twenty-one, which is noise against its own floor.

## The size of what 239 left

239 put signs into the walk. The buried kind have no script and the walk runs scripts, so it
collects none of them. Six runs in one process, so the reader can re-run the control (241):

```
    --play                                     101 of 183 on 43 of 79 map(s),  51 distinct item(s)
    --play --say-yes                           122 of 183 on 57 of 79 map(s),  58 distinct item(s)
    --play --say-yes --in-order                122 of 183 on 57 of 79 map(s),  58 distinct item(s)
    --play --say-yes --boat                    182 of 183 on 78 of 79 map(s),  65 distinct item(s)
    --play --say-yes --boat --in-order         182 of 183 on 78 of 79 map(s),  65 distinct item(s)
    --play --say-yes --boat --surf --in-order  182 of 183 on 78 of 79 map(s),  65 distinct item(s)
      never even stood on: 1.62 (35,5) index 33, item 36 [ELIXIR]
```

**The widest walk stands on 182 of the 183 and picks up none of them.** Every distinct kind the
game buries is on a map it reaches. The single exception is an ELIXIR on `1.62` at index 33 — one
buried thing in the whole game that the run cannot even get to the map of.

The gap is not that the run cannot reach the items. It is that a buried sign is a record with no
script, and everything the walk does it does by running one.

## Where the rules live

`WhatIsBuried.OnlyBuried` takes the records and the set of item ids a script names, and
`NeverStoodOn` takes the records and the maps a run reached. Both are in the reading rather than
in the printer, and both take collections rather than a cartridge, because a rule inside a
whole-world sweep is a rule no fixture can ask.

Two of the rules exist only because the cartridge has an oddity: **item nought is not an item** —
the twelve records that name none of them would otherwise put the item table's `????????` at the
top of a list of things found only underground — and **the buried records must not be in the
named-elsewhere set**, or every kind reads as having another source and does so silently.

## The breaks

| break | predicted | went red |
|---|---|---|
| what a script names is ignored | 1 | `AnItemAScriptNamesIsNotOnlyBuried` |
| item nought counts as an item | 1 | `ItemNoughtIsNotAnItem` |
| a kind buried twice is listed twice | 1 | `AKindBuriedSeveralTimesIsListedOnce` |
| what the run reached is ignored | 1 | `WhatTheRunNeverStoodOnIsWhatIsNotOnAReachedMap` |

Four for four. 3096 → 3101 tests. **The floor table did not move.**

---

## What is still owed

* **Collecting them.** The walk could pick a buried thing up by standing on its square, and that
  is a change to the run rather than to a reading — it would move what the party ends with and
  nothing else, because 249 has just shown it moves no reach. Whether it is worth doing is a
  DECISION and it is deliberately not made here.
* **The base** (248) — unanswerable from the flag number line and the load count. Settling it
  means reading compiled code.
* **The eight unused indices** (248) — 7, 16, 40, 43, 44, 45, 46, 124 — and **the spare bit**, set
  by six records whose every other field is ordinary.
* **The trigger's other half** (247): nobody has asked whether anything writes the VALUE a
  trigger's 228 conditions want, which is `--arrivals`' question pointed at a different list.
* `0x4001`'s other two flag sites (244); `10.6 (4,1)` (242); the 17 walls (242); the floor's seven
  flags (241); `0x026C` and `0x0807` (240); `0x194`'s nineteen doors (236); `0x82`'s seven words
  (238); the three numbers nothing computes (231); `0x406F` (229); `9.6`'s puzzle.
