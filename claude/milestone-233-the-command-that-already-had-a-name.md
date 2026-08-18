# Milestone 233: the command that already had a name

232 measured `0x9C` as an unnamed argument column: seven byte positions, seven distinct words, and
three of them in the three obstacle scripts. It was careful not to guess what the word meant.

It did not need to. **The command has been called `DoFieldEffect` in this repository since
milestone 191**, in a `private const byte` inside the sweep that reads who knows a move, and
`--who-knows` has been printing *"offers it: field effect 40"* off it ever since. Forty-one
milestones, two files, and neither of them asking the other.

---

## One name, in one place

```
  src/RomExtract/Scripts/EverywhereInTheImage.cs   private const byte DoFieldEffect = 0x9C;   (191)
  src/RomExtract/Scripts/ScriptReader.cs           [0x9C] = 2,                                (no name)
  ScriptCommands.NameOf(0x9C)                      "0x9C"
  milestone 232                                    "0x9C at 7 byte positions, seven distinct words"
```

This is the shape the prompt already warns about — *a rule fixed in one arm and left standing in
the other* — with a name instead of a rule. `ScriptCommands.DoFieldEffect` is the one now, the
private copy is gone, and every dump in the project prints `dofieldeffect`.

**The break says the unification is real.** Pointing the shared constant at `0x9D` fails
`WhatCrossesWaterTests.ABlockThatAsksAndThenOffersIsFound` — 191's own test — as well as this
milestone's two. Before today, breaking the private copy would have been invisible to everything
but one file.

## So: is the number a function of the move?

The name says it is and nobody had checked. It is checkable, because the same block says both — a
`findmove`, then a yes-or-no, then a `dofieldeffect`:

```
  7 block(s) in the image pair a move with a number:
    move  15 CUT          ->   2   at 0x081BDF2B
    move  57 SURF         ->   9   at 0x081A6AD6
    move 249 ROCK SMASH   ->  37   at 0x081BE024
    move  70 STRENGTH     ->  40   at 0x081BE13E
    move 127 WATERFALL    ->  43   at 0x081BE2C6
    move 291 DIVE         ->  44   at 0x081BE38C
    move 291 DIVE         ->  44   at 0x081BE3D5

  6 move(s), 6 number(s), and no move has two
```

**Nothing contradicts it, which is not the same as evidence for it.** Six moves and six numbers
would read identically if the numbers were arbitrary — one each is what you get from any
one-to-one assignment. The direct evidence is exactly one thing: **DIVE is the only move that
appears in two blocks, and it gets 44 both times.** One agreement, at roughly one in six, and the
instrument says so in those words rather than reporting six.

The move names are three-ways agreed and that is a different claim: the `findmove` says 249, the
cartridge's move table says ROCK SMASH, and the block's own sentence says *"This rock appears to
be breakable. Would you like to use ROCK SMASH?"*. That confirms **which move each block is
about**. It says nothing about what the number is.

## The four that no move drives, and the only floor worth having

Seven byte positions on maps take a `dofieldeffect` number. Three are the obstacle scripts above.
The other four have no `findmove` anywhere in their block:

```
    0x08162DAE  number  62    1.80  on arrival
    0x081652D0  number  68    2.56  person 1
    0x0816C994  number  64   10.14  sign (0,7)      <- the GAME CORNER, a third time
    0x081B2910  number  69   10.14  sign (17,13)
```

Ten distinct numbers between the two sets, and **every move-driven one is below every other one**:

```
  move-driven   2  9  37  40  43  44
  the others                        62  64  68  69
```

If which six of the ten were the move-driven ones were down to chance, the odds of them being
exactly the six smallest are **one in C(10, 6) = 210**. That is the strongest single statement this
milestone can make, and it is about the numbering rather than about any one number: the field
moves come first and something else lives above them.

What the four are is not read. `1.80`'s is on an arrival script, and two of the four are GAME
CORNER signs, which is the third time this year that map has turned up holding something.

## And the raw sweep, printed to be thrown away

```
  the raw whole-image sweep: 11446 site(s), 757 reading to a proper end, 6408 distinct number(s)
    the same sweep REVERSED:  11446 site(s), 834 reading on,            6397 distinct number(s)
    THE REVERSAL IS AHEAD.
```

One byte and a word is a three-byte pattern in sixteen megabytes. The reversal finds *more* blocks
reading to a proper end than the real image does. The instrument prints both halves and says the
raw number is not a finding — the only sites worth reading are the ones a map or a jump opens.

---

## One more stale number, found the same way

Reading `--who-knows`'s output against the prompt rather than past it, again:

```
  the block says   7 places ... are jumped into; 0 in the reversal; 4 offer
  the instrument   7 jumped into, 0 in the reversal, 5 OFFER
```

DIVE twice, SURF, STRENGTH and ROCK SMASH. KARATE CHOP and FLAMETHROWER are the two that do not.
And the two offering blocks that are **not** jumped into are CUT's and WATERFALL's — 7 offers in
the image, 5 of the 7 jumped-into sites, and those are two different denominators that the block
had collapsed into one.

That is the third stale line in three milestones, all found by the same act. Fixed here.

## The breaks

Eight, each against the whole suite, **eight caught**.

| break | what went red |
|---|---|
| one-number-per-move decided by comparing the two counts | `TwoMovesAndTwoNumbersIsNotOneNumberPerMove` |
| every repeat counted as an agreement | `ARepeatedMoveThatDisagreesIsNotAnAgreement` |
| the split is smallest-below-smallest | `TheSplitIsEveryOneBelowEveryOne` |
| the floor counted off one side instead of the union | that one, and `TheFloorIsOffBothSetsTogether` |
| an empty side still gets a floor | `NothingOnOneSideIsNotASplit` |
| the noise floor forgets to reverse | `TheReversedImageHasTheSameBytesAndNotTheSameReads` |
| the name goes back to a number | `TheCommandHasAName` |
| the shared constant points at 0x9D | **191's own test**, and two of this milestone's |

And one discrimination this milestone's tests deliberately **cannot** make, written into the test
rather than left to be discovered: `C(n, k)` equals `C(n, n − k)`, so a reading that took the floor
off the other side's size is arithmetically identical and no fixture can separate the two.

2975 → 2983 tests, all green. **Nothing the run does changed.**

---

## What is still owed

* **The four numbers no move drives** — `62` on `1.80`'s arrival script, `68` on `2.56`, and `64`
  and `69` on two GAME CORNER signs.
* **What a field effect number IS.** That the number follows the move rests on one repeat; that
  the move-driven ones are a low band rests on one in 210. Neither says what the game does with it,
  and nothing in this project has run the game.
* **The 41 routines a `0x27` follows** (232).
* **The three numbers nothing computes** (231): `62 gates hold 240 people`, `146 trees and rocks`,
  `158 objects`, and `the ceiling is 45 of 437 byte positions`.
* **`0x406F`** and the other 27 unsatisfiable arrival conditions (229).
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.
