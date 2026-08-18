# Milestone 232: ninety-seven objects and one command

231 built the column that found `0x0AB` — **97 calls at one byte position**, the largest routine
inflation in this cartridge — and left it unread. This reads it, and then builds the command that
should have existed since 190.

---

## Where it is

```
  0x081BE07C, and nowhere else in the file the map scan opens
```

That address is inside `0x081BE00C`, the shared ROCK SMASH script. Counted from the other end —
walking every map's object records rather than the script scan:

```
  0x081BDF13  CUT          49 object(s) on 21 map(s)
  0x081BE00C  ROCK SMASH   97 object(s) on 15 map(s)
  0x081BE11D  STRENGTH     54 object(s) on 15 map(s)
```

**Ninety-seven.** The same 97 `--routines` reports, from a completely different reading — one
counts script entries reaching a byte position, the other counts objects carrying a script
address, and they agree exactly.

## What it decides, and the block that says so

The two obstacle scripts have the same tail, up to a point:

```
  CUT        0x081BDF76        ROCK SMASH  0x081BE06F
    4F 0F 80 85 DF 1B 08        4F 0F 80 8F E0 1B 08     0x4F  — a person in 0x800F, a pointer
    51 00 00                    51 00 00                 0x51
    53 0F 80                    53 0F 80                 0x53  — take that object off the map
    6B                          25 AB 00                 special 0x00AB
    02                          21 0D 80 00 00           compare 0x800D, 0
                                06 01 8D E0 1B 08        if EQUAL goto 0x081BE08D
                                27                       0x27
                                6B                       faceplayer
                                02                       end
                             0x081BE08D
                                6B                       faceplayer
                                02                       end
```

**CUT ends where ROCK SMASH asks the routine.** Three identical commands, and then one script
stops and the other asks `0x00AB` and branches on the answer — and the whole of what the branch
decides is **one 0-argument command, `0x27`**. Both arms end `faceplayer; end`.

So the routine with the largest inflation in this file, reached by ninety-seven objects across
fifteen maps, decides exactly one byte. The run answers nought, takes the `if EQUAL`, and skips
it.

## And what `0x27` is, with a floor

```
  0x27 at 98 byte position(s) the map scan opens
    68 of them follow a `special` IMMEDIATELY, across 41 distinct routines
    17 follow 0x39, 9 a setvar, 3 a 0x9C, 1 a conditional

  the floor: 576 of 24491 byte positions are a `special` — 2.35%,
             so chance would put 2.3 of the 98 there, not 68
```

**Thirty times chance, and spread across forty-one routines rather than one routine's habit.**
`0x27` is a command that belongs after a routine call. What it *does* is not read here and this
milestone does not guess — 226's discipline: what it takes and where it sits are READ, what it
means needs the game's own code.

`0x9C`, the command three lines above it in all three obstacle scripts, is a column:

```
  0x9C at 7 byte position(s), SEVEN DISTINCT WORDS — one per site
    0x0002  1.109 person 2   (CUT)          then 0x27
    0x0025  1.72  person 4   (ROCK SMASH)   then 0x27
    0x0028  1.39  person 5   (STRENGTH)     then 0x27
    0x003E  1.80  on arrival                then 0x9E
    0x0044  2.56  person 1                  then 0x33
    0x0040  10.14 sign (0,7)                then 0x9E
    0x0045  10.14 sign (17,13)              then 0x28
```

Seven sites and seven different words is the argument test that settled `0xA1` at 55 and `0x97`
at 188 — arguments have columns and opcodes do not. And the three sites followed by `0x27` are
exactly the three field-move scripts.

**And one more denominator, which is the shape of the finding:** of every conditional the map scan
opens, **exactly one** has a fall-through that is a `0x27` its target does not have. It is
`0x081BE084`. `0x0AB` is the only routine on this cartridge whose answer decides a `0x27` and
nothing else.

---

## So: print the bytes

Reading the above took a hexdump, a width table copied by hand into a scratch script, and a
scratch console project to ask `ScriptReader` a question. **190 did that, 199 did it for three
widths, 228 did it for `0x0180`, and this milestone did it again** — while the prompt's own method
section says *stop inferring and print the bytes*.

There was no command that printed them. `--script-map` dumps a map and stops at the first `goto`.
`--stops` prints one command's stopped reads. `--climb` walks upwards. Nothing said *here is this
address*.

```
dotnet run ... --read-from 0x081BE06F,0x081BDF76
```

```
  0x081BE06F — 2 block(s), 11 command(s), 0 of them stopped

    0x081BE06F   <- asked for   hands over to 0x081BE08D
      0x1BE06F  4F 0F 80 8F E0 1B 08      0x4F
      0x1BE076  51 00 00                  0x51
      ...
```

**The bytes and the decode come off the same command**, so a hexdump and a disassembly of one
address cannot disagree — which is exactly what a hand-copied width table invites. It follows the
four pointer forms, reads each block once, and says which byte stopped a read and where.

## The breaks

Six, each against the whole suite. Four caught outright; two green, both diagnosed by the prompt's
own notes before the code was suspected.

| break | what went red |
|---|---|
| the printed bytes drop the opcode | `TheBytesAndTheDecodeComeOffTheSameCommand` |
| a compare's operand counts as a hand-over | `OnlyThePointerFormsHandControlOver` |
| a block reached twice is read twice | **GREEN** — then 2 tests |
| a proper end reports the last command as a stop | **GREEN** — then 1 test |

**The first green was fixture-lie 10.** The fixture put *both arms of one branch* on one target —
but a block already refuses to list the same target twice, so the duplicate never reached the walk
the seen-set guards. Re-sited as a diamond — two different blocks both ending on a third — it
fails two tests, and the loop case (a block handing back to one already read) fails with it.

**The second green was 219: a guard nothing can reach.** `stoppedOn is null ? null : stoppedAt`
looked like a rule and was a second statement of one that already held, because
`ScriptReader.StoppedAtOffset` returns nothing from a proper end itself. Nothing reached the line,
so breaking it changed nothing. Deleted, and the line that actually decides broken instead: it
fails one test — and **only** one, because `StoppedAtOffset` is used at five places in the printer
and had no test of its own anywhere in the repository until now.

2968 → 2975 tests, all green. **Nothing the run does changed.**

---

## What is still owed

* **What `0x0AB`, `0x27` and `0x9C` DO.** Where they sit and what they take is READ. Naming them
  needs the game's own code, an emulator, or a second cartridge to diff against — the same wall
  226 stopped at, and stopping at it is the point.
* **The four `0x9C` sites that are not obstacle scripts** — `0x003E` on `1.80`'s arrival script,
  `0x0044` on `2.56`, and `0x0040`/`0x0045` on two `10.14` signs, which is the GAME CORNER again.
* **The three numbers nothing computes** (231): `62 gates hold 240 people`, `146 trees and rocks`,
  `158 objects`, and `the ceiling is 45 of 437 byte positions`.
* **`0x406F`** and the other 27 unsatisfiable arrival conditions (229).
* **The eleven routines and eleven flags the arrival scripts have to themselves** (227, 228).
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.
