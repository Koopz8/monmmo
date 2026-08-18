# Milestone 258: ninety-nine, and a walk that reaches everything

257 finished with fourteen conditions that nothing in the scan can produce — six on the arrival
list, eight on the squares — and the observation that nobody had ever looked at one of them.

They are not fourteen facts. The square list's are **one idiom**, the arrival list's are six
separate things, and the test that let three more of the same idiom escape into a different bucket
**could not have said no to anything**.

---

## The counter answer had no denominator

`CanReach` walks a variable's write set by its steps and asks whether it lands on the value. 255
built it, credited three conditions to it, and 257 reported those three as the whole of what the
square list gained from 255's correction.

```
  0x4001  set of 45, steps 1/2/4  ->  reaches 100 of the 100 values in 0..99  (100%)
  0x4002  set of 18, steps 1      ->  reaches 100 of 100                      (100%)
  0x4003  set of  5, steps 1      ->  reaches 100 of 100                      (100%)
```

**Every variable the test has ever been given saturates.** A walk that reaches every value in
range has said yes before it was asked, and there was no number in the output that could have
shown it — the answer was printed without its denominator, which is trap 8 in a shape that does
not look like a byte scan.

Saturation is now its own answer, and it is an **exact predicate rather than a threshold**: the
walk either covers the range or it does not. It is not evidence anything writes the value.

```
  the square list's correction from 255:   3.7%  ->  0.0%, nought of 82
  the square list's NOTHING CAN produce:      8  ->  11
  both lists together:                     21.7% -> 20.9%
```

257's sentence was *the counter is worth nought on the arrival list and all three of what the
square list gained*. Both halves were right and the three were an artefact. **The two lists now
disagree completely: 27.0% and nought.**

## And the eleven are one idiom

Every square condition nothing can produce wants **99**:

```
    1.39  0x4064 == 99      1.40  0x4065 == 99      1.40  0x4066 == 99
    1.41  0x4067 == 99      2.35  0x4001..0x4007 == 99
```

Three things make it one fact rather than eleven.

**99 exists nowhere else.** Every value either list names is 0..8, bar a single 17 — and 99.

```
    value 0    —  28 on arrival, 125 on a square;  1880 setvar site(s) in the image write it,  1696 in the reversal
    value 1    —  78 on arrival,  32 on a square;   316 setvar site(s) write it,                  57 in the reversal
    …
    value 8    —  40 on arrival,   0 on a square;    69 setvar site(s) write it,                   7 in the reversal
    value 17   —   1 on arrival,   0 on a square;    21 setvar site(s) write it,                   9 in the reversal
    value 99   —   0 on arrival,  11 on a square;     3 setvar site(s) write it,                   2 in the reversal
```

Three sites against a reversed-image two. **Nothing in sixteen megabytes writes 99 to a variable
and nothing compares one against it.**

**The eleven scripts do the record's job themselves.**

```
0x08160F33  69                 lockall
0x08160F34  21 64 40 64 00     compare 0x4064, 100
0x08160F39  06 01 62 0F 16 08  if EQUAL goto 0x08160F62   (faceplayer ; end)
            …                  the scene …
0x08160F5B  16 64 40 64 00     setvar 0x4064, 100
```

All eleven, at **eleven distinct addresses**, open by comparing their own variable against 100 and
end by writing 100 to it. Measured against the whole population:

```
    on a square: 228 condition(s) — 142 WRITE their own variable at 85 address(es)
                 (the disarm, ordinary), 11 GUARD on it at 11 address(es)
      the control: 161 open with a compare of SOME variable, and 150 of those name a different one
      condition wants 99 and the script guards on 100 (that is +1)  x11

    on arrival:  350 condition(s) — 201 WRITE their own at 42 address(es), 3 GUARD at ONE address
      the control: 295 open with a compare of SOME variable, and 292 of those name a different one
      condition wants 1/2/3 and the script guards on 1  x1 each
```

**Writing your own variable is ordinary — 142 of 228 do it. Guarding on it is eleven scripts, and
they are exactly the eleven the reading calls impossible.** The arrival list's three are one
script address on `31.0` counted three times, which is why both columns are printed: 231 and 241
are the milestones where a count of reads was mistaken for a count of places, and this is the same
distinction deciding which of the two lists has a finding in it.

## Equality stands, so the eleven cannot fire

The obvious escape is that the condition is not an equality — that a record saying 99 fires while
the variable is *below* 100, which the script's guard then re-checks. That reading makes all
fourteen impossible conditions satisfiable at a stroke, so it is worth trying to refute rather
than adopt.

`3.42` refutes it. One map, one variable, **seven different script addresses**:

```
   3.42  0x405F -> 1@08168583, 2@08168598, 3@081685AD, 4@081685C2,
                   5@081685D7, 6@081685EC, 7@08168601
```

Under any `<=` or `<` reading those seven are simultaneously live whenever `0x405F` is small, and
a seven-stage sequence written as seven scripts becomes seven scripts that all run at once. The
same shape is at `3.41`, `3.14`, `4.3` and `3.8`. Equality is what the file is written for, and
125 of the 228 square conditions wanting nought with a script that writes 1 is the same reading
from the other side.

So under the only semantics this cartridge supports, **the eleven scenes cannot start**. Either
the engine special-cases a condition value of 99, or eleven scenes are dead. **No script can say
which** — it is the coord-event handler, which is compiled code, and this project has never read
any.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| saturation never fires — 258's correction reverted | 3 | 3 |
| the walk ignores the steps | 4 | 4 |
| a saturated counter is filed as an ordinary one | 2 | 2 |
| the guard stops checking WHICH variable it names | 1 | 1 |
| the guard is the last compare, not the first | 1 | 1 |
| **CONTROL:** the walk only counts upward | **0** | **0** |

Six of six. And the control's nought was the worst finding in the milestone: `HowManyItReaches`
had been written as **a second copy of `CanReach`'s loop**, so the one test covering the downward
arm was covering the other copy. That is 224's fault — a rule with two implementations — fixed at
220, 224 and 251, and walked back into inside the milestone that quotes the rule. Both questions
share one `Reachable(ceiling)` now, a fixture asks the count about a value only a subtraction
reaches, and the same break kills two.

## What is left

* **99, settled.** Reading the coord-event handler means reading ARM. It is the same wall
  `--buried`'s base hunt hit at 248 and the same wall the MODELLED starting nought sits behind,
  and three milestones now end at it. Whether this project ever reads compiled code is a decision.
* **The other three of the arrival list's six** — `0x400D == 17` on `2.10`, `0x4085 == 1` on `3.9`,
  `0x406E == 1` and `== 3` on `11.0` — have no self-guard and no shared shape. Four conditions,
  four maps, read individually and nothing has.
* **`2.35` and `1.39`–`1.41`.** Seven consecutive variables on one map and four on three others,
  all in the same idiom. What the scenes are is not read.
* **The 42 in the error bar** still stand where 257 left them: `0x405F` copied from `0x4001`.
