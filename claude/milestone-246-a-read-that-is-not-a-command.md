# Milestone 246: a read that is not a command

245 asked what the twelve variables the map scan writes and nothing consults are for, and named
three possibilities it could not tell apart: dead space, a thing the compiled game reads by
address, or a counter kept for a routine.

The answer to the first question turned out to be a fourth thing, and it was not about any of the
twelve individually. It was about what the sweep had been enumerating.

---

## Nineteen maps were consulting one of them

```
  and 27 number(s) are looked at by a map header on arrival, which is a read and is not a command
    — no sweep in this project counted one before 246

  19 of the 90 variable(s) the map scan WRITES are never looked at by anything this project can find
    26 by the commands alone, which is what 245 printed — 7 of those are read by a map header
    on arrival and by nothing else:
      0x4050 —  1 arrival condition(s) on  1 map(s): 3.0
      0x4063 —  2 arrival condition(s) on  1 map(s): 1.87
      0x406E —  3 arrival condition(s) on  1 map(s): 11.0
      0x4075 —  3 arrival condition(s) on  2 map(s): 3.12, 32.4
      0x407C — 19 arrival condition(s) on 19 map(s): 5.5, 6.6, 7.4, 8.1, 9.2, 10.13, ...
      0x4083 —  1 arrival condition(s) on  1 map(s): 2.22
      0x4084 —  2 arrival condition(s) on  1 map(s): 3.54
```

**`0x407C` is read on nineteen maps and 245 reported it as looked at nowhere in sixteen
megabytes.** So is `0x4075`, on two, and `0x4084`, and four more.

An arrival condition is not a script. It is two halfwords in the map's own header — *run this
script when this variable holds this value* — and `--arrivals` has been printing all 350 of them
since 229. It names a variable. It is a read. And there is no command anywhere in the file.

Every sweep in this project walks a script stream and decides what a number is by which operand
of which command named it. That is the right shape for a question about a script stream and it is
silently the wrong shape for a question about the cartridge, which is **trap 1 exactly**: before
believing any "nothing in the world does X", check what the scan is enumerating. It was
enumerating commands. The sentence was about the world.

The deaf list goes **26 → 19**, and the nowhere-at-all list **12 → 9**.

## And for the nine that are left, the one question the cartridge can still answer

A script names a variable in an operand. **Compiled code cannot** — a sixteen-bit constant does
not fit in a THUMB instruction, so the compiler puts it in a four-byte-aligned literal pool and
loads it PC-relative. That is the only shape available to a routine that wants to read `0x4026`,
and it is a handle on the question 245 could not get at.

```
      0x4026 — 2 word(s) an instruction loads (1 that nothing loads or a script owns)   REVERSED: 0
                 <- 0x0CCE84 loaded from 0x0CCE44, 0x0CCFEC loaded from 0x0CCFBE
      0x403E — 3 word(s) an instruction loads (0 ...)                                   REVERSED: 0
                 <- 0x0CCE80 loaded from 0x0CCE38, 0x0CCEE0 loaded from 0x0CCEC2, 0x0CD02C from 0x0CD00A
      0x4059 — 0    0x405B — 0    0x405C — 0    0x405D — 0
      0x407D — 0    0x4088 — 0    0x408B — 0                              REVERSED: 0 for all
```

**Two of the nine are held by the game's own code. Seven are held by nothing in the file at all.**

## The word alone is a weak filter, and the denominator is what says so

The first version of this instrument asked only for an aligned word equal to the id. It looked
convincing on the nine — two hits, three hits, reversal nought — and the denominator killed it:

```
      at least 1 loaded word(s): 29   REVERSED: 4    (and without the instruction, 41)
      at least 2 loaded word(s): 18   REVERSED: 0    (and without the instruction, 29)
      at least 3 loaded word(s): 15   REVERSED: 0    (and without the instruction, 24)
```

**41 of 90 against a reversed 27 is the same order of number** — the shape 245 threw its own
whole-image aggregate away for, met again one milestone later in a sweep built to replace it. The
instruction is what turns it into a reading: `ldr rX, [pc, #imm]` is five fixed bits and an
eight-bit offset that has to come out at exactly this address, and 2.4% of aligned words in this
image have one at all.

That line is in the output permanently, with the without-the-instruction number beside it, so
nobody has to take on trust that the extra condition is doing the work.

## What the two that are held look like

`--read-from 0x0816521A` — BIRTH ISLAND's on-load, three commands:

```
    0x16521A  16 10 40 96 00            setvar 0x4010, 150
    0x16521F  16 26 40 00 00            setvar 0x4026, 0
    0x165224  16 3E 40 00 00            setvar 0x403E, 0
```

One map resets three variables and **all three are loaded by compiled code and by nothing else**:
`0x4010` four times, scattered across three regions; `0x4026` and `0x403E` five times between
them, and every one of those five inside `0x0CCE38`–`0x0CD02C`, about half a kilobyte of one
routine. Two variables a script writes side by side, held as adjacent words in one literal pool.

**Two of two is not a column** (238) and this project does not read compiled code, so what that
region does is not claimed. What is READ is that it exists and that it loads both.

## The limit, printed rather than left implicit

```
    the limit of this reading: a routine that computes an id from a base would hold the BASE and
    not the id, and nothing here would see it.
```

A routine walking a range of variables from a base reads every one of the seven and this
instrument sees nothing. **The seven are variables this game writes, no script looks at, no map
header consults and no instruction loads** — which is a sharper claim than 245's and is still not
"dead".

> **CORRECTED AT 247.** This section originally quoted "0x4000 is loaded 56 times against a
> reversed 0, so that is not an empty worry". **Fifty-six is the count WITHOUT the load
> requirement** — the very filter this milestone spent its length arguing for — and the line was
> edited to use the load without the run being repeated. The number the command prints is **1**.
> It is trap 16 committed in the milestone that quotes trap 16, and trap 21 as well: the sentence
> about what a number is keyed on was written without going and looking at the key. The
> base-relative worry is real and thinner than stated, which is the direction that flatters the
> hedge rather than the finding.

## The breaks, predicted before running

Trap 20 says to predict the count first. Seven breaks, seven predictions, seven matches:

| break | predicted | went red |
|---|---|---|
| the non-command readers are not subtracted | 2 | `AVariableOnlyAMapHeaderReadsIs…`, `TheCommandsOnlyListStillReports…` |
| a header entry that runs nothing counts as a read | 1 | `AHeaderEntryThatRunsNothingIsNotARead` |
| the word sweep drops the four-byte alignment | 2 | `AnOccurrenceOffAFourByteBoundary…`, `AScriptWritingTheVariableDoesNot…` |
| the load no longer has to reach THIS word | 1 | `ALoadThatReachesTheNextWordIsNot…` |
| `HeldByCode` ignores whether a script owns the bytes | 1 | `AWordInsideAScriptsOwnOperandIs…` |
| the raw reading is not corrected the same way | 1 | `TheRawReadingIsCorrectedTheSameWay` |
| **the reversed floor counts words rather than loaded words** | **0** | **nothing — the floor was a control nothing could fail** |

The last one is the point of predicting. A green break is normally a surprise to be diagnosed;
this one was written down as expected beforehand, so it was a known hole the moment it came back
rather than four hours of suspecting the fixture. Two fixtures were added — one laid down
*backwards* so that reversing the image is what puts the word on a boundary, and a second where
an instruction does reach it so the first cannot pass on a floor that is always nought — and the
same break re-run kills exactly one.

## Where the rules live

`WhenAMapRunsSomething.IsARead` and `.LookedAt` take entries rather than a `MapLibrary`, because a
rule that needs a whole cartridge is a rule no fixture can reach — the fault four milestones
running found by breaking a guard and getting green back. `In()` was changed to call `IsARead`
too, so the caller counting conditions and the caller counting variables cannot come apart.

3067 → 3082 tests, all green. **The six rows of the floor table did not move.**

---

## What is still owed

* **The seven.** `0x4059`, `0x405B`, `0x405C`, `0x405D`, `0x407D`, `0x4088`, `0x408B` — every one
  written with 1, once or twice, and held by nothing. Separating "dead" from "read from a base"
  needs the base-relative case, which is a real instrument and is not this one.
* **The other twenty of the twenty-seven arrival variables.** Only seven of them were on the deaf
  list; the rest were already read by a command. Nothing has asked whether any OTHER reading in
  this project is missing header reads the same way — `--who-reads`, the flag work and
  `--in-the-image` all enumerate commands.
* `0x4001`'s other two flag sites (244); whether `EverywhereInTheImage.Reads` should stop counting
  `0x1A arg2` (244) — still marked rather than moved.
* `10.6 (4,1)` (242); the 17 walls (242); why the floor's seven flags are what they are (241);
  `0x026C` and `0x0807` (240); `0x194`'s nineteen doors (236); `0x82`'s seven words (238); the
  three numbers nothing computes (231); `0x406F` (229); `9.6`'s puzzle; `3.57 sign (9,43)`.
