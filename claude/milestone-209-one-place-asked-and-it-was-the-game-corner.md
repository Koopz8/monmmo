# Milestone 209: one place asked, and it was the GAME CORNER

208 read a coin counter off the cartridge and finished with an owed line saying it could not
tell whether the run stands in front of one, because `--play` prints **"8 places asked it for
money"** and has never printed which eight.

It does now, and the answer was not what the count suggested.

---

## The floor's only money question is the coin counter

```
--play
  1 place(s) asked it for money and it answered neither way — a CEILING, and the only one with no lever
    10.14    CELADON CITY     0x0816C68D  wants 1000
```

One place, and it is `10.14` person 2 — the counter 208 read. The floor's entire money ceiling
is the GAME CORNER, and the reading has been describing it as an anonymous "1 place" since
milestone 200.

At `--say-yes` and above there are eight, and they are four addresses' worth of thing:

```
    10.14    CELADON CITY     0x0816C68D  wants 1000
    11.0     FUCHSIA CITY     0x0816D36D  wants 500
    11.0     FUCHSIA CITY     0x0816D379  wants 500
    11.0     FUCHSIA CITY     0x0816D385  wants 500
    16.0     ROUTE 4          0x0816F75F  wants 500
    6.0      PEWTER CITY      0x0816A38F  wants 50
    6.0      PEWTER CITY      0x0816A3A5  wants 50
    6.0      PEWTER CITY      0x0816A3BB  wants 50
```

`16.0 0x0816F75F` is 201's — the one that asks for 500 and hands over `#129` at level 5 anyway.
It is the only one of the eight that gives anything, and now it has neighbours with addresses
rather than a number standing in for them.

## The arm the run never takes

`--coins` says two places in the image sell coins: `0x0816C714` at ¥10000 for 500, and
`0x0816C742` at ¥1000 for 50. The run only ever meets the second, and the bytes say why:

```
  0816C6E6  2B 43 02             checkflag 0x0243          the COIN CASE
  0816C6E9  06 00 90 C7 16 08    if clear  goto 0x0816C790
  0816C6EF  21 09 80 00 00       compare 0x8009, 0
  0816C6F4  06 01 34 C7 16 08    if equal  goto 0x0816C734  <- fifty coins, ¥1000
  0816C6FA  21 09 80 01 00       compare 0x8009, 1
  0816C6FF  06 01 06 C7 16 08    if equal  goto 0x0816C706  <- five hundred coins, ¥10000
  0816C705  02                   end
```

Which arm is taken is `0x8009`, and `--who-writes 0x8009` says:

```
  0x8009 — 22 site(s) in the file, 22 of which read as script, 22 of which the map scan opened
    = 1: 2 site(s) — 0x168589 (3.42 trigger), 0x170513 (28.0 trigger)
    = 50, 80, 300, 1000, 3000: 7.9 person 1
    = 304, 308, 321: 10.5 person 2
```

**Twenty-two writers, and not one of them is on 10.14.** Nothing in any script on that map puts
anything in `0x8009`, so the run holds nought and takes the ¥1000 arm every time. The other arm
is chosen by whichever row of a menu the player picked — compiled code, past the code boundary,
the same place the wall flags live.

So this is the image-against-run distinction with an address on it: **`--coins` reads both
exchanges because it reads every arm of every branch; a run takes one arm, and here the thing
that picks the arm is not in the file's scripts at all.** Neither reading is wrong and the
difference is not noise.

---

## What changed

`Attempt.WalkedPastAMoneyCheck` was a `HashSet` whose `Count` was the only thing that ever left
the run. It is a dictionary now and `Attempt.MoneyChecks` carries the places, each with **the
amounts asked there as a list** — because one place can ask for more than one, which the coin
counter does.

The rule lives on the run rather than in the printer, for the seventh time.

### Four breaks, four catches

| break | caught by |
|---|---|
| the list is keyed on the address alone | the same script on two maps, and both counts |
| only the first amount a place asks is kept | a place that asks two amounts in one script |
| the list is empty and the count is not | all four |
| the amounts do not merge across passes — the last one wins | a place that asks a different amount on the next pass |

The last fixture is new in shape and it is the one worth carrying: **a script that answers
differently on pass two**. Everything else in this file hands the runner the same answer every
time, so a run that overwrote rather than merged would have looked identical. The pass count is
asserted out loud in that test, because with one pass neither half of it can fail.

**One assertion is deliberately not made.** That a place's amounts are distinct is guaranteed by
the collection they are kept in, not by a rule, so asserting it would be a test that cannot fail
— which is the thing 208 deleted a control for. It is said in the test instead.

2827 → 2831 tests, all green. The run's own numbers did not move: this is a list where there was
a count.

---

## What is still owed

* ~~The floor's flag count is still a number with no list.~~ Done at 210, in the same session.
  207 needed to know which three flags 199 added and had to hand-patch a print into two
  worktrees to find out. It was `0x026E`/`0x026F`/`0x0270` and they are all on 10.14 — the same
  map as everything above.
* `0x8009` is read at the counter and written twenty-two times elsewhere with what look like
  prices and ids. What it is **for** is not claimed here; only that nothing on 10.14 writes it.
* Whether a `--money N` run reaches the ¥10000 arm is unknown, because the arm is not chosen by
  money — it is chosen by `0x8009`, and there is no lever for that. A `--choose N` lever would
  be MODELLED and it is deliberately not added.
* The payout table is still unlocated. Everything above is a price; nothing is an income.
