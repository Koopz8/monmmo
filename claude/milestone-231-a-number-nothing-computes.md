# Milestone 231: a number nothing computes

230 ended by saying the *Where the reading stands* block had never been re-run and that auditing
it would find more. This is that audit — forty-five lines, each against the instrument that
produced it — and the thing it found is a category the traps did not have a name for.

---

## The audit

Every instrument run once, and every line of the block read against its own output.

```
  39 lines   RIGHT — checked, unchanged
   4 lines   WRONG — stale by one commit or by six
   4 lines   quote numbers NO INSTRUMENT IN THIS REPOSITORY PRINTS ANY MORE
```

**Right, and worth saying so** — a clean audit is a result. `--fights` 729 sites on 104 maps, 27
with a second exit and 8+2 = 10 of them skipping a guard. `--coins` five sites, four distinct
(bound, gift) pairs, every one summing to 10000, nought chains in the reversal, two places selling
at ¥20 and three price lists of five rows each. `--the-scan`'s five kinds: 15966 / 3015 / 2134 /
1324 / 1167 places alone, and 1250+360+128+163+58 = 1959 addresses. `--who-knows` 7 jumped into
against 0 in the reversal, and exactly 4 blocks that offer. `--through-a-call` 336 / 225 / 57 / 40
/ 9, with 19+19 across the `copyvar` and 2+9 with no owner. `--arrivals` 350 / 69 / 28. The three
shut-gate buckets 35 / 31 / 17 / 15 / 12, and 35 and 15 the same at the floor. Eleven maps with no
way in.

**Wrong:**

```
  187 have an answer nothing branches on          -> 186          (off by one)
  0x083 and 0x084 asked TWICE between them        -> three (1 + 2)
  0x194 is 747 calls at 26 places                 -> 1066 at 34
  of 1055 branching sites, nought takes 212       -> 1118 sites at 437 byte positions
```

`0x194`'s line entered the prompt at **milestone 223**. **224 — the very next milestone — is the
one that found the shared script list was missing two of the five kinds** and moved every number
of that shape. 224 printed its own before-and-after for the totals and never touched this line.

## And the four that nothing computes

```
  178 routines called at 936 places
  the ceiling is 45 of 437 byte positions
  62 gates no walk opens hold 240 people; 146 of them are CUT trees and ROCK SMASH rocks
  3 scripts hold 27 gating flags and 158 objects
```

`936`, `45`, `62`, `240`, `146` and `158` appear in **no output of any instrument in this
repository**. They were true when somebody wrote them and there is now no way to find out whether
they still are without writing new code.

That is a worse failure than staleness and it needs its own name. **A stale number is wrong; a
number nothing computes cannot even be wrong.** It reads exactly like a measurement, it is quoted
like a measurement, and there is no instrument that could contradict it. It is trap 8 — a number
with no denominator — one turn further on: a number with no *instrument*.

Two of them were checkable in pieces and are: `27 gating flags` and `3 scripts` are confirmed
(15 CUT and ROCK SMASH across two scripts, 12 STRENGTH across one). The rest are marked in the
block rather than deleted, because "nobody can check this" is itself information the next session
needs.

---

## So one of them was made computable

`936` was the cheapest to recover and the most worth having, because of what it is: **how many
BYTE POSITIONS call a routine, as against how many times it is called.**

That is the places-not-reads rule — the one this project has walked into seven times and built
`--the-scan` at 224 to end. `--the-scan` asks it of a command *code*. **Nobody had ever asked it
of a routine *number*.** `--routines` counted calls and printed calls, and a block hanging off
nineteen Pokémon Centres calls whatever it calls nineteen times at one address.

```
  178 routines called, 4461 times between them, at 936 byte position(s)
    118 of the 178 are called once per byte position; for the rest a count of calls
    and a count of places are different numbers

    0x0AB     97 call(s) at    1 place(s) x97.0
    0x194   1066 call(s) at   34 place(s) x31.4
    0x180     19 call(s) at    1 place(s) x19.0
    0x039    234 call(s) at  234 place(s)        <- the honest kind
```

**`936` was right.** Six milestones of it being unverifiable and it was right the whole time,
which is the least satisfying way for an audit to end and the only honest one.

And the new number is sharper than the one 224 called sharpest. `findmove` at **66.7** reads per
address was 224's headline for how badly a count of reads can mislead. `0x0AB` is **97 calls at a
single byte position** — a routine that reads as asked all over the game and is asked in one
place. Sixty of the hundred and seventy-eight answer differently depending which question you ask.

## The breaks

Six, each against the whole suite.

| break | what went red |
|---|---|
| places counted as calls | `CallsAndPlacesAreDifferentNumbers` |
| a routine nothing calls gets a place anyway | `ARoutineNothingCallsIsNotInTheTallyAtAll` |
| the inflation upside down | `TheInflationIsCallsPerPlace` |
| called-once-per-place becomes at-least-once | `ARoutineAskedOncePerPlaceAnswersTheSameEitherWay` |
| **`Derive` credits calls where it should credit places** | **GREEN** |
| `Assemble` swaps the branch and across columns | `TheRowPutsCallsAndPlacesInTheirOwnColumns` |

**The green one is the fifth in this project with one cause, and the prompt's own advice found it
in one step.** The line that chooses which half of a `(calls, places)` pair goes in which column
lived inside `Derive`, which needs a `MapLibrary` and sixteen megabytes, so no fixture could reach
it. Pulled out as `SpecialContracts.Assemble` — the same move 227 made for the same reason — and
re-broken, it fails exactly one test, and so does a second break that swaps two other columns.

Worth recording precisely: the green run's only red was
`ServerIntegrationTests.AnExistingPlayerSeesSomebodyElseArrive`, on a 56-second suite against the
usual 25. That is the known flaky one, it is timing-dependent, and reading it as the guard would
have been reading the machine's load as a result.

Then the duplicate removed: `Derive` was counting calls twice, once into its own dictionary and
once into the tally, and quoting the first. One counter now, and `--routines` prints byte-for-byte
what it printed before.

2963 → 2968 tests, all green. **Nothing the run does changed** — `--play` is 183 / 153 in 6.

---

## What is still owed

* **The three numbers still nothing computes**: `62 gates hold 240 people`, `146 trees and
  rocks`, `158 objects`, and `the ceiling is 45 of 437 byte positions`. Each needs either an
  instrument or deleting. They are marked in the block.
* **`0x0AB`** — 97 calls at one byte position, branched on at that one place, and nobody has read
  it. The largest inflation in the file and one address to look at.
* **The lines the audit could not check** because they are prose rather than counts: `0x0070`'s
  two movers (228), `0x084A`/`0x084B` holding nobody (218), what `0x63` and `0x65` DO (226).
* **`0x406F`** and the other 27 unsatisfiable arrival conditions (229).
* **The eleven routines and eleven flags the arrival scripts have to themselves** (227, 228).
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.
