# Milestone 244: an operand that names a value

243 ended by owing one thing: **is any of the 27 numbers used in both namespaces a MISREAD rather
than a real double use?** `setflag 0x4001` was odd enough to ask about, and the honest answer
turned out to be about the other twenty-six.

**Twenty-six of the 27 are not double uses at all.** The answer is 1, and it is `0x4001` — the
number that raised the question.

---

## The shape says it before the reading does

Neither namespace was ever printed as a shape, only as a count. Printed:

```
  as a FLAG    : 0x0000+ 237 number(s) at 347 place(s),  0x4000+   1 at    4
  as a VARIABLE: 0x0000+ 145 number(s) at 501 place(s),  0x4000+  77 at  841,  0x8000+ 14 at 2897
```

Flags are one band and one stray. Variables are two bands — and **145 numbers in a third place
that is not a band**. Split by which operand of which command named them, the third place has
exactly one occupant:

```
    0x16 arg0: 0x4000+ 73n/410p,  0x8000+  9n/687p
    0x17 arg0: 0x4000+  4n/15p
    0x18 arg0: 0x8000+  1n/3p
    0x19 arg2: 0x4000+  5n/29p,   0x8000+  9n/150p
    0x1A arg0: 0x8000+  3n/509p
    0x1A arg2: 0x0000+ 145n/501p, 0x4000+  2n/2p,  0x8000+ 2n/6p     <- all of it
    0x21 arg0: 0x4000+ 50n/379p,  0x8000+ 10n/1518p
    0x22 arg0: 0x4000+  1n/2p,    0x8000+  3n/13p
    0x22 arg2: 0x4000+  2n/4p,    0x8000+  2n/11p
```

Every operand in this cartridge lands wholly inside the two bands except one, and that one holds
**all** of the outliers.

## The test that needs no band

Naming the bands would be asserting something from outside the file, which this project is not
allowed to do. It does not have to. **A variable something looks at is a variable something
writes:**

```
  and of what each READING operand names, how much is ever written:
    0x19 arg2:  12 of  14 —  86%
    0x1A arg2:   3 of 149 —   2%
    0x21 arg0:  57 of  60 —  95%
    0x22 arg0:   3 of   4 —  75%
    0x22 arg2:   4 of   4 — 100%
```

**2% against 75–100%, with nothing in between.** `0x1A` takes a destination and a source, and the
source is a plain number unless it happens to be a variable id — so a literal 5 handed to a routine
was being counted as a look at variable 5. Five is also a real flag. That is the whole of the
twenty-six.

The rule is written as *an operand more than half of whose numbers are never written is naming
values*, and the half is deliberately doing no work: the percentages are printed beside it so the
gap can be seen rather than trusted.

## What it costs

```
  27 named both ways  ->  1 once the value-naming operand is left out
    0x4001  4 as a flag, 325 as a variable
```

The floor of 1.71 has not moved, so the corrected finding is **at the floor**: this game does not
reuse numbers across the two namespaces, and the single exception is the one already known.

Blast radius elsewhere is small and was measured rather than assumed. `--who-reads 0x4055` — the
quoted "21 readers against a floor of 0" — is 21 `compare` sites and not one of them is this
operand. `0x4059`'s "no readers anywhere" is a nought and over-counting could only have added. But
`--who-reads 0x0001` is **244 of its 283** sites, so the command now says so out loud rather than
handing over a number that cannot say it about itself.

## Which is trap 8 in a new place

*A number printed with no denominator cannot come back empty.* This is one turn further out: **a
number with a denominator and no BREAKDOWN cannot come back mixed.** 243 printed 27 against 1.71
and both figures were right; what neither could say is that the 27 were twenty-six of one thing and
one of another. The breakdown that found it — the same numbers split by which operand named them —
cost nine lines of output.

## The breaks

Five, five catches:

| break | what went red |
|---|---|
| an operand is never called a value-namer | two tests |
| the writing set forgets one of the four writing operands | `AnOperandNamingWhatTheFileWritesIsNamingVariables` |
| the value-namers are not excluded from the corrected count | `ALiteralThatEqualsAFlagNumberIsNotANumberUsedBothWays` |
| a band counts numbers where it should count places | two tests |
| operands merged across their arguments | four tests |

The last one is the one this milestone is made of: `0x1A arg0` is a destination and lands wholly in
the top band, `0x1A arg2` is the source and holds every outlier. Summed as one command they are 91%
written and nothing is visible.

3057 → 3064 tests, all green. **The six rows of the floor table did not move.**

---

## What is still owed

* **`0x4001` itself.** Four `setflag`/`clearflag` sites and 325 variable places on one number. Two
  of the four sites were read at 243 and both are clean; the other two are not.
* **Whether `EverywhereInTheImage.Reads` should stop counting that operand.** It is a whole-image
  sweep with a different population and quoted numbers hang off it, so 244 marked the output
  rather than moving it. That decision is a decision and it is owed a re-run.
* **`0x0002`** — 23 flag sites, gating eight objects, and its variable side is now known to be
  literals. What the flag does is still unread.
* **`10.6 (4,1)`**, the one sign nothing can stand beside (242); the 17 walls (242); why the
  floor's seven flags are what they are (241).
* **`0x026C` and `0x0807`** (240), **`0x194`'s nineteen doors** (236), **`0x82`'s seven words**
  (238), the three numbers nothing computes (231), `0x406F` (229), and everything owed at 215
  onwards.
