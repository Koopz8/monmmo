# Milestone 223: a count of reads

220 caught one arm of the routine work counting reads and printing them as places. This is the
other arm — `SpecialCalls`, which is what `--specials` and `--play`'s ceiling are read off — and
the numbers move further.

---

## The headline

```
  before   17 routines branch away on the zero they are getting by default,
           at 212 of 1055 branching sites
  after    ... which are 25 of 411 byte position(s), and that is the number about the cartridge
```

**Twenty-five places.** The whole of what a run's silence decides in this cartridge, in addresses
rather than in reads of addresses.

And the table it comes from:

```
  before   3509 calls to 158 different routines
  after    3509 calls at 861 byte position(s) to 158 different routines
```

## The inflation is not a constant

```
    0x0194   747 calls at   26 place(s) on  10 maps
    0x001F   267 calls at   13 place(s) on  20 maps
    0x0199   210 calls at   10 place(s) on  20 maps
    0x0039   234 calls at  234 place(s) on  34 maps
    0x0188   127 calls at   39 place(s) on  59 maps
```

**`0x0194` is twenty-nine reads per address. `0x0039` is one.** In the same table, ranked next to
each other, with nothing in either row to say which is which.

That is why both numbers are printed rather than one replacing the other. A conversion factor
would be a fourth wrong number. `0x0194` is the one 215 called "747 sites, 1 of 18 branches
taken" — it is twenty-six places and one of four.

## Three instruments now agree about one address

`0x0188` is the routine 215 called the last of the run's ceiling. Three separate readings of the
cartridge, arrived at from three directions:

* 215 grepped all sixteen megabytes for `25 88 01 21 0D 80` and got **one hit**.
* 220's corrected `--routines` says **2 branch at 1 place**.
* 223's corrected `--specials` says **zero branches away at 2/2 — 1/1 place**.

Before this milestone the third of those said `2/2` with no denominator that meant anything, and
before 220 the second said `2` and could not be compared with the first at all.

`--routines` and `--specials` also both now report **411 branching byte positions**, from
independent code. That agreement is new and it is the check that says the correction is right in
both arms rather than consistent in one.

---

## What changed

* `SpecialCall` carries `At`, the byte position of the call. Nothing downstream could tell
  nineteen reads of one address from nineteen addresses before it.
* `SpecialCalls.Profile` gains `Places`, `BranchPlaces` and `PlacesTakenByZero` beside `Calls`,
  `Branches` and `BranchesTakenByZero` — three pairs, each printed together.
* `SpecialCalls.In` reads one script's calls, so the rule that files a call under its own address
  is reachable from a test with a handful of bytes.

Four breaks, four catches. The fourth went green: the reading that records the position lived
inside `All`, which needs a whole cartridge, so every test above ran on records written by hand
and the one thing that could actually be wrong was unguarded. Split out, it fails one test.

**That is four milestones running.** The pattern is no longer a coincidence: this project puts
its rules inside whole-world sweeps, and the sweep is exactly what a fixture cannot reach. The
prompt now says to ask where the rule lives before suspecting the fixture.

2912 → 2919 tests, all green. **Nothing the run does changed** — every number the playthrough
prints about its own passes was already about the run.

---

## What is still owed

* **The rest of the project's numbers.** Flags, moves, items, doors — every sweep that counts
  `(map, script, offset)` records has the same question waiting, and only the routine tables have
  been asked. `--who-writes` and `--in-the-image` answer about byte positions already; the
  map-scan instruments mostly do not.
* **The 396 the run asks.** That number is deduped by `(map, scene, routine)`, which is a fact
  about the RUN and correct as it stands — but it cannot be compared with 861 or 411, and nothing
  says so where it is printed.
* The standard-routine table (222), `callstd 0x05`'s 250 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.
