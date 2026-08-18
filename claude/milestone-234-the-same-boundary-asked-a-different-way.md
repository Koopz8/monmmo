# Milestone 234: the same boundary, asked a different way

233 split the ten `dofieldeffect` numbers into two bands by value — six a move drives, four
nothing does, and every one of the six below every one of the four, at one in 210. That is a
statement about the numbering and it rests on one arrangement.

This reads the four, with `--read-from`, and the split turns up again as something else entirely.

---

## What waits for it

```
    number   2 at 0x081BDF61   an unnamed wait (0x27)   [a move drives this one]
    number  37 at 0x081BE05A   an unnamed wait (0x27)   [a move drives this one]
    number  40 at 0x081BE164   an unnamed wait (0x27)   [a move drives this one]
    number  62 at 0x08162DAE   a wait NAMING 62 — the same number
    number  64 at 0x0816C994   a wait NAMING 64 — the same number
    number  68 at 0x081652D0   a wait NAMING 68 — the same number
    number  69 at 0x081B2910   nothing waits for it
```

**The move-driven ones are followed by a wait that names nothing. The ones no move drives are
followed by a wait that names a number — and it is the number the effect was started with, three
times out of three.**

`0x9E` is **three byte positions in the whole map scan** and all three are one of those. Drawing
from the four numbers no move drives, three matches is one in 64; from all seven these sites use,
one in 343. The alphabet is a MODELLED choice and the instrument prints both and says so.

So the boundary 233 found by value shows up again as a different command. **Seven sites either
way** — this is corroboration, not a second sample, and it is worth exactly that much: two
questions about one small set agreeing is better than one, and it is not seven independent
witnesses.

The fourth high number, `69`, waits for nothing at all. Named as the exception rather than
smoothed into the pattern.

## And what the four sites are

```
  62   1.80   SECTION 49    on arrival (0x4001 == 0)
  64   10.14  an interior   NINETEEN signs share this one block
  68   2.56   BIRTH ISLAND  person 1
  69   10.14  an interior   one sign at (17,13)
```

`0x0816C994` is one byte position reached from **nineteen sign entries on one 18×15 map** — a
room whose walls all say the same thing, and 232's rule applies to it: one place, nineteen
entries. The block after `69` loads the cartridge's own sentence:

> *"Your POKéMON print is ready! Check your TRAINER CARD."*

`62`'s block goes on to clear `0x009D` and `0x04B8`–`0x04BC` and ask `0x00A9`; `68`'s sets
`0x0807`, asks `0x0138`, clears it again and branches on `0x00B4`. None of that is read further
here.

---

## A correction: 10.14 is not called the GAME CORNER

230, 232 and 233 all said `10.14` was "the GAME CORNER". **That is not a name this project has
read.** What the export says is:

```
  10.14   CELADON CITY   18x15   11 object(s)
```

Bank 10 is Celadon's interiors and the region-name table gives every one of them the city's name,
so `CELADON CITY` here means "somewhere in Celadon" and nothing finer. The name GAME CORNER came
out of milestone **199's commit message**, where it was a reasonable guess, and three milestones
carried it forward as though it were a reading.

What IS read about `10.14`: an 18×15 interior, eleven people of whom **5 to 10 hand something over
against a coin count** (208's chains, `0x0816C706` through `0x0816C91A`, every bound plus its gift
summing to 10000), twenty signs of which nineteen share one block, and the three flags milestone
199 opened — `0x026E`, `0x026F`, `0x0270` — set by persons 5 to 10.

That is a much better description than the name was, and it is the one to quote. The guess may
well be right; it is still a guess, and this project's rule is that a guess says so.

## The breaks

Six, each against the whole suite, six caught.

| break | what went red |
|---|---|
| a wait naming another number counted as naming the same one | `AWaitNamingTheSameNumberIsNotAWaitNamingAnother` |
| only the very next command counts as a wait | `TheWaiterIsNearbyAndNotAnywhere` |
| the window has no far end | that one again |
| a second effect does not end the window | `ASecondEffectEndsTheWindow` |
| the unnamed wait reports the effect's own number | `TheUnnamedWaitNamesNothing` |
| the coincidence is matches to the power of the alphabet | `TheCoincidenceIsTheAlphabetToThePowerOfTheMatches` |

The window is the whole instrument and it lies in both directions: **only the next command** misses
`2.56`, which puts a `0x33` between the two, and **arbitrarily far** would credit every block that
has a wait anywhere in it. One test asserts both halves, and a second effect ends the window
whatever is left of it.

2983 → 2988 tests, all green. **Nothing the run does changed.**

---

## What is still owed

* **What a field effect number IS.** Two readings of one boundary now, and neither says what the
  game does with a number. That needs the game's own code.
* **`0x9E`'s three sites are all there are**, so nothing about it can be checked against more of
  this cartridge. `0x27` has 98.
* **The 41 routines a `0x27` follows** (232) — the other half of the waiting question.
* **`10.14`**, described rather than named: what the nineteen signs say, and what `0x011E` (the
  routine the shared sign block asks) answers.
* **The three numbers nothing computes** (231).
* **`0x406F`** and the other 27 unsatisfiable arrival conditions (229).
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.
