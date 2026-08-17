# Milestone 208: the number none of them holds

199 settled how wide `0xB3`, `0xB4` and `0xB5` are. 200 settled `0x91` and `0x92` and wrote, in
as many words: *"the pair the GAME CORNER is built out of: the one that asks and the one that
takes. What each does is NOT claimed here; only how wide it is."*

Eight milestones later nobody had claimed it. This does, and the number it turns on is written
nowhere in the cartridge.

---

## The capacity

Five places in the image read a count into a variable, compare that variable against a bound,
branch on the answer, and hand a quantity over on the fall-through. `--coins` finds them by that
shape and prints all five:

```
  0x0816C706  variable 0x4001  bound   9500  gift   500  ->   10000
  0x0816C734  variable 0x4001  bound   9950  gift    50  ->   10000
  0x0816C803  variable 0x4001  bound   9990  gift    10  ->   10000
  0x0816C8BA  variable 0x4001  bound   9980  gift    20  ->   10000
  0x0816C91A  variable 0x4001  bound   9980  gift    20  ->   10000

  EVERY ONE OF THEM SUMS TO 10000 — from 4 distinct (bound, gift) pair(s) at 5 site(s).
```

**Four different bounds, four different gifts, and one sum.** A guard that refuses at the bound
before adding the gift is a guard against passing bound + gift, so ten thousand is the capacity
of whatever these commands count — READ, and not present as a number at any of the five sites.

This is the **inverse** of trap 10. 200's `0x92` had nine sites agreeing on one resume byte and
the agreement was worth nothing, because all nine were landing in the same run of zeroes. Here
the sites agree on nothing you can see — not the bound, not the gift, not the branch target —
and only on a quantity you have to compute. *Count what the sites agree ON, not how many agree.*

### The control that could not fail

The first control written for this paired each bound with a gift it did not come with and
reported that none of those sums was the answer. It passed. It also **cannot do anything else**:
if every bound plus its own gift is `S` and no two sites share a pair, then `bound(a) + gift(b)`
with `a ≠ b` equalling `S` forces `gift(a) = gift(b)` and then `bound(a) = bound(b)` — the same
pair. The line was true before the cartridge was opened.

That is this project's own "a guard nothing can fail: decoy or deletion", arriving on something
written the same session. It was deleted. The control is the reversed image instead, which can
come back either way:

```
  CONTROL — the same chain hunt on this file REVERSED finds 0 chain(s), so nothing with
  these byte statistics makes this shape by accident.
```

Five against nought, which is the shape `--who-knows` had.

### And the raw sweep is not a finding

```
  52135 site(s) carry one of the three coin commands; 4262 read on to a proper end
  the same sweep REVERSED finds 52135 site(s), 7142 reading on — 1159 place(s) against 1406
    THE TWO COMPARISONS DISAGREE ABOUT THE SIGN — behind the floor by site, ahead by place.
```

The same sign disagreement 206 found in `--flags`, on a different sweep, first time of asking.
The instrument says so itself rather than quoting whichever number flatters.

---

## What it costs, and what it buys

```
  2 place(s) sell them for money:
    0x0816C714  asked 10000  gave 500  took 10000  ->  20 each
    0x0816C742  asked  1000  gave  50  took  1000  ->  20 each
    one price at every place that sells them: 20 — READ
```

**This is the first money number in the project with a source.** "Money is modelled" needs
splitting from here on: the *purse* is modelled and the *payout table* is still unlocated, but
the *prices* are read.

Three price lists, written as script — rows of two `setvar`s leaving by a shared door, priced in
the variable something subtracts:

```
  0x0816CC15  a CREATURE           0x0816CEA5  an ITEM        0x0816D010  an ITEM
     63 ABRA        180              301 TM13     4000          194 SMOKE BALL     800
     35 CLEFAIRY    500              311 TM23     3500          205 MIRACLE SEED  1000
    147 DRATINI    2800              312 TM24     4000          215 CHARCOAL      1000
    123 SCYTHER    5500              318 TM30     4500          209 MYSTIC WATER  1000
    137 PORYGON    9999              323 TM35     4000           40 YELLOW FLUTE  1600
```

Fifteen rows, all READ. The third list was not known to exist before this ran.

### The fallback that named a cause

The first version printed the first list as **HP UP, MAX ETHER, IAPAPA BERRY, GLITTER MAIL and
ASPEAR BERRY**, and looked exactly as convincing as the table above.

The printer read each id against the item table and fell back to the species table. **Every id
in every one of these lists is inside both tables**, so the fallback never ran, and the output
said "read against both" while having consulted one. Trap 5's shape — a fallback that names a
cause is worse than one that says nothing — on a line nobody would have re-read.

What fixes it is not a better fallback. It is that **the door says which**: the shared exit of
the first list reaches `0x79`, and the other two reach `0x46`, and both of those commands were
already claimed in this repository (`WhatItIsWaitingFor`, `WhatIsBehindAStop`, `ScriptRunner`,
`WorldExporter`). Nothing new is claimed to make the discrimination — the reading comes off the
door and not off the number, and the instrument prints which door it read.

---

## Two breaks came back green, and both were the fixture

Twelve breaks were run. Ten were caught first time. The two that were not:

| break | first attempt | why it passed | re-broken |
|---|---|---|---|
| the second command need not be a `compare` | **green** | the fixture was `B3 v; B4 g; end`, and the hand-over landed at index **one** — which the fall-through scan never reaches. It answered correctly for a reason that had nothing to do with the compare. | caught, against `copyvar` in the compare's place: same four bytes, same variable, not a compare |
| nothing has to branch on the comparison | **green** | the replacement fixture put filler where the branch was, so the **block stopped being readable** and failed the "reads as a script" filter first. It passed because the block was broken, not because the branch was missing. | caught, against something exactly as wide as the branch in the branch's place |

Both are fixtures-lie #1 wearing new clothes: the fixture was more forgiving than the cartridge,
in a way that made the assertion true for free. Neither was a fault in the code.

And every break was pointed at the whole class rather than at one test, so which test went red
is part of the record — 206's fault was a break aimed at one of two near-identical functions
while the test watched the other.

2811 → 2827 tests, all green. **Nothing the run does changed**; this is a reading.

---

## What is still owed

* **Whether the RUN stands in front of any of this is not answered here.** 10.14 is on no
  shut-door list at any lever setting and all seven sites are inside scripts the map scan opens,
  but 201's "8 places ask the run for money" is a count without a list, so whether two of those
  eight are the coin counter's cannot be read off. That is the same "a number with no list"
  shape as the five flags that look moved and are not.
* `0x4001` holds the count at all five ceiling sites and is also this cartridge's scratch pad
  (173: 285 scripts write it). Nothing here needs it to be more than scratch — the compare reads
  back what the read just wrote, four bytes earlier — but a reader who assumes `0x4001` means
  coins anywhere else will be wrong.
* The byte after every money amount is not claimed. `0x92` and `0x91` are four bytes of value
  and a fifth this project has no reading for, and `--coins` prints the four and ignores the one.
* `0xC7`, which sits immediately after four of the five hand-overs, is still unclaimed.
* The payout table is still unlocated. What a fight pays is the number that would make `--money`
  unnecessary, and nothing in this milestone touches it.
* The clumping threshold and the entropy cut are still MODELLED and still unvaried (206).
