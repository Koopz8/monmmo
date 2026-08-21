# Milestone 264: the block, re-run

231 audited 45 lines of this project's standing block and said the other forty had never been
looked at. 263 said the same. 230's rule is that a block nobody re-runs drifts **and stays
self-consistent while it does**, so nothing in it will ever complain.

Re-run against a fresh run of every instrument. Most of it holds. **Nine lines have drifted, and
one command contradicts itself.**

---

## What drifted

```
  766 places call 63 routines the widest run cannot answer  ->  860 / 75
  186 have an answer nothing branches on                    ->  276 places, 58 routines
  the widest run sets 212 of 322 gating flags, 110 shut     ->  213 / 109
  those 110: 35 / 30 / 16 / 15 / 7 / 7                      ->  35 / 30 / 16 / 15 / 8 / 5
  7 of the 106 variables the map scan WRITES                ->  7 of 115
  the load denominator 33 of 106 against a reversed 5       ->  34 of 115 against 5
    (50 without the instruction)                            ->  (57 without)
  the whole-image namespace version 2117 / 12659 / 1182     ->  2117 / 14308 / 1333
  variables 0x4000+ 77n/841p and 0x8000+ 14n/2897p          ->  77n/856p and 16n/3428p
  0x4001 against a floor of 1.71                            ->  a floor of 1.73
```

## And the cause is one milestone

**Five of the nine are downstream of 252 alone.** It put `specialvar`'s destination and `0x42
arg0` into the operand tables — a good change, well evidenced, and correct. It moved the variable
population from **106 to 115**, and every place count, denominator and floor computed off that
population moved with it, in five different lines of the block.

252 re-ran none of them, and nothing could have told it to: **the number it changed appears
nowhere in the block.** What appears there is the population it feeds. The block even records the
previous step — *"it was 90 variables until 251 put copyvar's destination in the write table"* —
and stops there.

The other four are the run's own numbers moving as the run changed.

## What held

Most of it, and worth writing down so the next audit knows what it does not have to re-check:

2915 scripts on 425 maps reaching 3888 blocks; 3856 read to a proper end and 32 stop at 19 codes;
729 trainerbattles on 104 maps with 27 second exits and 10 skipping a guard; 7 jumped-into
who-knows sites against 0 in the reversal, 5 offering; 275 hand-overs, 26 scenes, 112 doors; 702
signs — 519 scripts at 360 addresses on 143 maps, 317 run at the floor (214, 79) and 465 at the
widest (327, 134); the map scan's 2915 entries at 1959 addresses, 90624 reads at 24491 byte
positions, 11 of 108 codes read once per byte, and all five per-kind ALONE counts; 1118 branching
sites at 437 byte positions and 48 routines branched on; 178 routines, 4461 calls, 936 byte
positions; `0x0AB` at 97 calls in one place; five coin sites all summing to 10000 with 0 chains in
the reversal; `0x63`'s floor of 0.45 and `0x65`'s of 22.7; 68 of 936 call places followed by a
wait at 36 routines; the buried record's 183, eight gaps, 65, 21 and 30.3, and the twelve on
`10.14`; 5660 askings at 2791 places of 9 numbers; the field effects' six and six, one in 210 and
one in 64; the operand sweep's 453 of 30766 at 1.5%; every reading operand's written-ness; and
both flag bands.

## And one command contradicts itself

`--buried` printed **"the item table's 308 entries"** and **"the 307 item(s) in the table"** nine
lines apart. Both were right and they meant different things: 308 counts entry nought, which this
cartridge's table calls `????????`.

The inconsistency is cosmetic. **What it exposes is not.** 248's evidence that a buried sign's
first halfword is an item id was:

> all 183 first halfwords resolve to a name in the item table's 308 entries — a location built
> for a different question, which cannot have been tuned to agree

**Twelve of the 183 resolve to the blank.** The same command has said so, nine lines further down,
for sixteen milestones: *"12 name item 0, which the table calls ????????"*. Counting a placeholder
as a hit is how a test that could have failed stops being able to — a wrong offset landing on
entry nought scores as a match.

Stated honestly the reading is **stronger**:

```
    and 171 of the 171 first halfwords that are not NOUGHT resolve to a name in the item
    table's 307 named entries — the other 12 resolve to entry 0, which the table calls
    "????????" and which is a placeholder rather than a name
    counted the way 248 counted it that is 183 of 183 against 308 entries, and the
    difference is the blank
```

Both counts stay in the output with the difference named, and the predicate is
`Buried.NamesAnItem` rather than a comparison inline in the printer.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the blank counts as a name | 1 | 1 |
| an id the table lacks counts anyway | 1 | 1 |

## What is left

* **An instrument that prints the block.** 231 said the honest end of this job is a command rather
  than a person, and `--the-floor` proves the shape works: six rows that cannot go stale because
  the differences are subtracted from the rows themselves. Nothing has been built for the other
  hundred and seventy.
* **The four lines this audit could not check** without building something first: they are the
  run's own numbers at lever settings this milestone did not run.
* **252's other consequences.** Five block lines moved; whether anything else in the repository
  quotes the 106 has not been grepped.
