# Milestone 276: a maximum is a property of how many times you looked

275 measured both ends of the mixture bound's scale and left an obvious next question: the ends were
still taken off whole populations of hundreds while the thing being read was thirty-eight blocks.
Measuring them at thirty-eight instead kills the share outright — and on the way past it kills
273's verdict, which is the one reading in this project that the sampling band was invented for.

**Both faults are the same fault: a threshold that moves with how much you looked.**

---

## A band's top is a maximum

273's verdict is "0.601 is OUTSIDE the band a 38-block sample of the maps' own scripts lands in".
That band is eleven groups, because 435 flag sites is what there is, and its top is the largest of
eleven numbers.

The maps' own SCRIPTS are the same kind of thing — real script, reached from the map scan rather
than from a flag site — and they supply a hundred and two groups of 38. Their top climbs:

```
      its top over the first k groups: k=4 0.222, k=11 0.236, k=25 0.278, k=50 0.345, k=102 0.826
```

**0.601 is inside that.** A 38-block sample of real script does reach it, and further, given enough
samples. "Outside the band" was a verdict against a threshold set by having looked eleven times.

## And a band scored against a whole that contains the group is too tight

`SamplingBand` scores each group against the population it was cut from, which holds it. That pulls
every distance down by the share of the whole the group is — 8.7% here. `AgainstTheRest` is the
version with the group taken out, and it moves the maps' own sites' band from `0.257..0.417` to
`0.278..0.451`. Small, and in the direction that makes 273's verdict weaker rather than stronger.

## The ends at 38, four junk models, and mixtures

```
      end                                                    band at 38   groups   at least as far as the 38 (0.601)
      REAL: the maps' own sites, each against the REST       0.278..0.451       11   0/11 = 0.0 %
        the same, against a whole that CONTAINS the group    0.257..0.417       11   0/11 = 0.0 %
      REAL: the maps' own SCRIPTS, another derivation        0.213..0.826      102   6/102 = 5.9 %
      JUNK: the reversal's sites                             0.423..0.896      109   36/109 = 33.0 %
      JUNK: the maps' own sites NUDGED +4                    0.301..0.496        7   0/7 = 0.0 %
      JUNK: the maps' own sites NUDGED +16                   0.345..0.442        7   0/7 = 0.0 %
      JUNK: the maps' own sites NUDGED +64                   0.367..0.443        7   0/7 = 0.0 %
```

**The ends cross under every junk model**, so there is no scale and no share. And the mixtures say
the same thing from the other side — every row, including the pure ones, reads 0%..100%:

```
        0% the maps' own (  0 +  38)  distance   0.301..0.496  reads 0.0 %..100.0 %   from 7 group(s)
       25% the maps' own (  9 +  29)  distance   0.295..0.561  reads 0.0 %..100.0 %   from 9 group(s)
       50% the maps' own ( 19 +  19)  distance   0.256..0.544  reads 0.0 %..100.0 %   from 15 group(s)
       75% the maps' own ( 28 +  10)  distance   0.243..0.561  reads 0.0 %..100.0 %   from 15 group(s)
      100% the maps' own ( 38 +   0)  distance   0.278..0.451  reads 0.0 %..100.0 %   from 11 group(s)
```

The distance column is the interesting half. **It does not move with the share at all**: pure junk
sits `0.301..0.496` and pure real `0.278..0.451`, overlapping almost exactly. So the nudged site —
the only junk model that lives in this image, and therefore the only one a mixture group can be
built out of — **is not junk for this reading**. Its blocks are real script read from a boundary
that is not one, and at thirty-eight blocks that is indistinguishable from real script.

## What is left is a rate

A rate has a denominator and looking more does not inflate it.

> A 38-block group of real script is at least as far as the 38 in **5.9%** of cases (6/102), and one
> of the reversal's sites in **33.0%** (36/109) — **5.6 times likelier junk than real.**

That is evidence, and it is not *"these are the reversal's kind and not the maps'"*. **273's verdict
was too strong; its direction survives.** The 38 are still much more like the reversal's sites than
like the maps' own scripts, and the sixty's accounting — 21 + 1 + 38 — is unchanged as an
accounting. What changes is that the 38 are no longer settled: about one in seventeen samples of
real script looks like them.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| `AgainstTheRest` compares against the WHOLE | 2 | **2** |
| the rest is only the TAIL after the cut | 2 | **2** |
| `AgainstAnother` scores against its own whole | 1 | **1** |
| `AtLeastAsFar` uses `>` rather than `>=` | 1 | **1** |
| the band version of `BetweenTheEnds` takes only the two mins | 2 | **2** |
| **CONTROL:** the rate written `Where(...).Count()` | **0** | **0** |
| **CONTROL:** the rest written as a `Where` over indices | **0** | **0** |

Seven predictions, seven matches — the second time (246 was the first).

One of them is worth recording as a fixture limit rather than a success. The tail-only break was
predicted to kill `TheRestIsBothSidesOfTheCut` and `OneKindThroughoutIsNoughtFromTheRest`, and does,
but it does NOT kill `AGroupIsScoredAgainstTheRestAndNotAgainstAWholeThatHoldsIt` — the last group's
rest is empty there, and a group against an empty tally scores exactly the same as that group
against the other pure kind. **An empty comparand is not an obviously wrong answer; it is a
plausible one.** The fixture that catches it is the three-group one, and it is in the suite for that
reason.

## What is left

* **The rate has no error bar.** 6/102 and 36/109 are counts, and a difference of counts wants a
  denominator of its own. Nothing here says how often two populations of this size would differ this
  much by luck.
* **Two REAL populations, and they disagree.** The maps' own SITES give 0/11 and the maps' own
  SCRIPTS 6/102. Whether that is the sample count or a real difference between the two shapes has
  not been measured — subsampling the scripts to eleven groups repeatedly would say, and that needs
  a source of groups this project is willing to reproduce.
* **The same critique lands on `--the-ruler`'s own verdict** (275, one milestone old — trap 49
  exactly). Its "OVERLAPS THE KNOWN JUNK BAND" and "entirely BELOW the known real band" are
  band-extreme comparisons on two and three groups. The rate is printed beside them now; the
  verdict sentence still reads off the extremes.
* **`AsksWhoKnows`'s nudge** (272) and **the seam** (269) — still owed, still untouched.
